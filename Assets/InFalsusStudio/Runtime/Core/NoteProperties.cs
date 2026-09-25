using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InFalsusStudio.Core
{
    /// <summary>One note's form snapshot; does not write until Apply is explicitly invoked.</summary>
    public sealed class NoteProperties
    {
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;
        public int SourceId {get;private set;}
        public EventKind Kind {get;private set;}
        public string OriginalSource {get;private set;}
        public readonly Dictionary<string,string> Values=new Dictionary<string,string>();
        private readonly Dictionary<string,string> initial=new Dictionary<string,string>();
        public bool Dirty {get{return Values.Any(p=>!initial.ContainsKey(p.Key)||p.Value!=initial[p.Key]);}}
        public static NoteProperties Capture(SpcDocument d,SpcEvent note)
        {
            if(note==null||!note.IsNote)throw new ArgumentException("A note is required.");
            var f=new NoteProperties{SourceId=note.SourceId,Kind=note.Kind,OriginalSource=d.SourceLine(note.SourceId)};
            f.Put("start",note.TimeMs);
            if(note.Kind==EventKind.Hold||note.Kind==EventKind.SkyArea)f.Put("end",note.EndMs);
            if(note.Kind==EventKind.Tap||note.Kind==EventKind.Hold)
            {f.Put("lane",note.Lane);f.Put("width",note.GroundWidth);}
            else if(note.Kind==EventKind.Flick)
            {
                f.Put("x",note.Get(1)/note.Get(2)*100);f.Put("width",note.Get(3)/note.Get(2)*100);
                f.Put("direction",note.Get(4));f.Put("split",note.Get(2));
            }
            else
            {
                f.Put("sx",note.Get(1)/note.Get(2)*100);f.Put("sw",note.Get(3)/note.Get(2)*100);
                f.Put("ex",note.Get(4)/note.Get(5)*100);f.Put("ew",note.Get(6)/note.Get(5)*100);
                f.Put("leftEase",note.Get(7));f.Put("rightEase",note.Get(8));
                f.Put("group",note.Get(10,-1));f.Put("startSplit",note.Get(2));f.Put("endSplit",note.Get(5));
            }
            return f;
        }
        private void Put(string name,double v){string text=v.ToString("0.##########",CI);Values[name]=text;initial[name]=text;}
        public NoteProperties Copy()
        {
            var c=new NoteProperties{SourceId=SourceId,Kind=Kind,OriginalSource=OriginalSource};
            foreach(var p in Values)c.Values[p.Key]=p.Value;foreach(var p in initial)c.initial[p.Key]=p.Value;return c;
        }
        private double Read(string key)
        {
            double v;string s;
            if(!Values.TryGetValue(key,out s)||!double.TryParse(s,NumberStyles.Float,CI,out v)||double.IsNaN(v)||double.IsInfinity(v))
                throw new ArgumentException("Invalid finite number: "+key);
            return v;
        }
        private bool Changed(string key){return Values[key]!=initial[key];}
        private void Numeric(SpcDocument d,string key,int arg)
        {if(Changed(key))d.SetNumber(SourceId,arg,Read(key));}
        private void Percent(SpcDocument d,string key,int arg,double split)
        {
            if(!Changed(key))return;
            double n=Read(key);if(key=="width"||key=="sw"||key=="ew")if(n<=0||n>100)throw new ArgumentException("Width must be >0 and at most 100 percent.");
            decimal raw=(decimal)n*(decimal)split/100m;
            AuthoredNumber.Set(d,SourceId,arg,(double)raw);
        }
        private void SetSkyEase(SpcDocument d,bool right,string key)
        {
            double value=Read(key);
            if(value<0||value>2||value!=Math.Truncate(value))throw new ArgumentException("Ease must be 0, 1 or 2.");
            EditOperations.SetEase(d,new[]{SourceId},right,(int)value);
        }
        public void Apply(SpcDocument d)
        {
            if(!d.Contains(SourceId)||d.SourceLine(SourceId)!=OriginalSource)
                throw new InvalidOperationException("This note changed while the form was open. Reload the form before applying.");
            var original=d.ReadEvents().FirstOrDefault(e=>e.SourceId==SourceId);
            if(original==null||original.Kind!=Kind)throw new InvalidOperationException("Selected note no longer matches this form.");
            double start=Read("start");if(start<0)throw new ArgumentException("Start time must be nonnegative.");
            Numeric(d,"start",0);
            if(Kind==EventKind.Hold||Kind==EventKind.SkyArea)
            {
                double end=Read("end");if(end<=start)throw new ArgumentException("End time must be after start.");
                // Absolute start/end fields are independent: moving one leaves the OTHER
                // endpoint fixed. Untouched source times remain byte-identical.
                if(Changed("start")||Changed("end"))d.SetNumber(SourceId,Kind==EventKind.Hold?3:9,end-start);
            }
            if(Kind==EventKind.Tap||Kind==EventKind.Hold)
            {Numeric(d,"lane",Kind==EventKind.Tap?2:1);Numeric(d,"width",Kind==EventKind.Tap?1:2);}
            else if(Kind==EventKind.Flick)
            {Percent(d,"x",1,original.Get(2));Percent(d,"width",3,original.Get(2));Numeric(d,"direction",4);}
            else
            {
                Percent(d,"sx",1,original.Get(2));Percent(d,"sw",3,original.Get(2));
                Percent(d,"ex",4,original.Get(5));Percent(d,"ew",6,original.Get(5));
                if(Changed("leftEase"))SetSkyEase(d,false,"leftEase");
                if(Changed("rightEase"))SetSkyEase(d,true,"rightEase");
                if(Changed("group"))
                {
                    double value=Read("group");if(value< -1||value!=Math.Truncate(value))throw new ArgumentException("Group must be -1 or a nonnegative integer.");
                    if(d.ArgumentTokens(SourceId).Length>10)d.SetNumber(SourceId,10,value);
                    else if(value>=0)d.SetOptionalNumber(SourceId,10,value);
                }
            }
            var result=d.ReadEvents().FirstOrDefault(e=>e.SourceId==SourceId);
            string reason=result==null?"Invalid note":ChartMath.InvalidReason(result);
            if(reason==null&&(Kind==EventKind.SkyArea||Kind==EventKind.Flick))reason=AirReachBounds.Violation(result);
            if(reason!=null)throw new ArgumentException(reason+" Use the explicit raw-source editor to retain nonstandard data.");
        }
    }
}
