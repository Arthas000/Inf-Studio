using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    public sealed class ChartImportResult
    {
        public SpcDocument Document;
        public int SourceNotes,ImportedNotes,SourceEvents;
        public readonly List<string> Warnings=new List<string>();
    }
    // Converts the supplied decoded ICP1 JSON, NOT the encrypted/obfuscated ICP1 binary.
    // Original binary/JSON stays untouched. Enum mapping lives here, never in the renderer.
    // Easing names are now verified against 565 paired Enigma plaintext Sky segments
    // (Minimal 88, Forbidden 477), 1,130 independent boundary codes. Not a binary exporter.
    public static class IcpJsonImport
    {
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;
        private static string N(double n){if(double.IsNaN(n)||double.IsInfinity(n))throw new InvalidDataException("Nonfinite chart number.");return n.ToString("G17",CI);}
        private static string Call(string name,params double[] args){return name+"("+string.Join(",",args.Select(N).ToArray())+")";}
        public static bool IsBinary(byte[] b){return b!=null&&b.Length>=4&&b[0]=='I'&&b[1]=='C'&&b[2]=='P'&&b[3]=='1';}
        public static ChartImportResult Load(string file)
        {return Convert(ProjectJson.ReadFile(file));}
        private struct Rational
        {
            public long Num,Den;
            public double Value {get{return (double)Num/Den;}}
        }
        private static Rational Rat(Dictionary<string,object> note,string key)
        {
            var obj=ProjectJson.Object(ProjectJson.Get(note,key));long num=ProjectJson.Integer(obj,"numerator"),den=ProjectJson.Integer(obj,"denominator");
            if(den<=0||den>100000000)throw new InvalidDataException("Invalid denominator for "+key);
            return new Rational{Num=num,Den=den};
        }
        private static long Gcd(long a,long b){while(b!=0){long r=a%b;a=b;b=r;}return Math.Abs(a);}
        private static double[] Endpoint(Dictionary<string,object> note,string prefix)
        {
            var x=Rat(note,prefix+"_x");var w=Rat(note,prefix+"_width");long den=checked(x.Den/Gcd(x.Den,w.Den)*w.Den);
            if(den>1000000000)throw new InvalidDataException("Common coordinate denominator is too large.");
            return new[]{(double)(x.Num*(den/x.Den)),(double)den,(double)(w.Num*(den/w.Den))};
        }
        // Each side is a one-hot three-bit field. In this import profile:
        // 4/32 Linear, 8/64 SineOut, 16/128 SineIn. Not codes 1/2/4 in plaintext SPC.
        public static bool TryEase(int flags,out int left,out int right)
        {
            int l=flags&28,r=(flags>>3)&28;left=l==4?0:l==8?1:l==16?2:-1;right=r==4?0:r==8?1:r==16?2:-1;
            return left>=0&&right>=0&&(flags&~252)==0;
        }
        public static ChartImportResult Convert(Dictionary<string,object> chart)
        {
            if(ProjectJson.Text(chart,"format")!="ICP1"||ProjectJson.Integer(chart,"version")!=1)
                throw new InvalidDataException("Expected decoded ICP1 version 1 JSON. Binary SPC is not plaintext.");
            double bpm=ProjectJson.Number(chart,"bpm"),meter=ProjectJson.Number(chart,"beats_per_bar");
            if(bpm<=0||meter<=0)throw new InvalidDataException("Imported BPM and beats_per_bar must be positive.");
            var notes=ProjectJson.Array(ProjectJson.Get(chart,"notes"));var events=ProjectJson.Array(ProjectJson.Get(chart,"events"));
            var result=new ChartImportResult{SourceNotes=notes.Count,SourceEvents=events.Count};
            var text=new StringBuilder();text.AppendLine(Call("chart",bpm,meter));
            text.AppendLine("// Imported from decoded ICP1 JSON. Source original retained; do not overwrite binary SPC.");
            text.AppendLine("// ICP easing profile: one-hot 4/32=Linear, 8/64=SineOut, 16/128=SineIn; paired Enigma plaintext validation: 565 Sky segments, 1130 boundary codes.");
            var ids=new HashSet<int>();
            foreach(var item in notes)
            {
                var note=ProjectJson.Object(item);int id=ProjectJson.Integer(note,"id");if(!ids.Add(id))throw new InvalidDataException("Duplicate ICP note id "+id);
                try
                {
                    int type=ProjectJson.Integer(note,"type"),side=ProjectJson.Integer(note,"side"),extra=ProjectJson.Integer(note,"extra_flags");
                    double start=ProjectJson.Number(note,"start_ms"),end=ProjectJson.Number(note,"end_ms");
                    if(start<0||end<start)throw new InvalidDataException("Invalid note time interval");
                    if(ProjectJson.Integer(note,"relay")!=0)throw new InvalidDataException("Unverified nonzero relay");
                    if(ProjectJson.Integer(note,"packed_flags")!=((type<<8)|side))throw new InvalidDataException("Packed flags do not agree with type and side");
                    string line;
                    if(type==1||type==2)
                    {
                        if(extra!=0)throw new InvalidDataException("Unverified ground flags");
                        Rational x=Rat(note,"start_x"),w=Rat(note,"start_width");int lane=side==2?0:side==3?5:side==1?checked((int)Math.Round(x.Value*4)):-1;
                        double width=w.Value*4;
                        if(lane<0||lane>5||width<1||width>4||Math.Abs(width-Math.Round(width))>1e-8||side==1&&Math.Abs(x.Value*4-lane)>1e-8)
                            throw new InvalidDataException("Unsupported ground lane/width mapping");
                        if((lane==0||lane==5)&&width!=1||lane>=1&&lane<=4&&lane+width>5)throw new InvalidDataException("Ground coverage outside playable lanes");
                        if(Rat(note,"end_x").Value!=x.Value||Rat(note,"end_width").Value!=w.Value)throw new InvalidDataException("Unverified moving/resizing ground note");
                        if(type==1&&end!=start)throw new InvalidDataException("Tap with duration");
                        line=type==1?Call("tap",start,width,lane):Call("hold",start,lane,width,end-start);
                    }
                    else if(type==4&&side==4)
                    {
                        if(extra!=1024&&extra!=4096||end!=start)throw new InvalidDataException("Unverified Flick flags/duration");
                        var ep=Endpoint(note,"start");line=Call("flick",start,ep[0],ep[1],ep[2],extra>>8);
                    }
                    else if(type==5&&side==4)
                    {
                        int l,r;if(!TryEase(extra,out l,out r))throw new InvalidDataException("Unverified Sky easing flags");
                        var s=Endpoint(note,"start");var e=Endpoint(note,"end");
                        line=Call("skyarea",start,s[0],s[1],s[2],e[0],e[1],e[2],l,r,end-start,ProjectJson.Integer(note,"group_id",-1));
                    }
                    else throw new InvalidDataException("Unverified ICP note type/side "+type+"/"+side);
                    text.AppendLine("// source_note_id="+id+" source_group_id="+ProjectJson.Integer(note,"group_id",-1));text.AppendLine(line);result.ImportedNotes++;
                }
                catch(Exception ex) when(ex is InvalidDataException||ex is OverflowException)
                {
                    // Store the entire decoded record as inert base64, instead of deleting it
                    // or silently inventing a supported note. Original JSON remains alongside.
                    text.AppendLine("icp_unknown_note("+System.Convert.ToBase64String(Encoding.UTF8.GetBytes(ProjectJson.Write(note)))+")");
                    result.Warnings.Add("Note "+id+": "+ex.Message+"; raw record retained, not rendered.");
                }
            }
            int opaqueEvents=0;
            foreach(var item in events)
            {
                var e=ProjectJson.Object(item);int type=ProjectJson.Integer(e,"type");double t=ProjectJson.Number(e,"timestamp_ms");
                if(type==0&&ProjectJson.Get(e,"value") is double)text.AppendLine(Call("track",t,ProjectJson.Number(e,"value")));
                else if(ProjectJson.Get(e,"bpm") is double && ProjectJson.Get(e,"beats_per_bar") is double && ProjectJson.Number(e,"bpm")>0 && ProjectJson.Number(e,"beats_per_bar")>0)
                    text.AppendLine(Call("bpm",t,ProjectJson.Number(e,"bpm"),ProjectJson.Number(e,"beats_per_bar")));
                else
                {
                    text.AppendLine("icp_event("+N(t)+","+type+","+System.Convert.ToBase64String(Encoding.UTF8.GetBytes(ProjectJson.Write(e)))+")");opaqueEvents++;
                }
            }
            if(opaqueEvents>0)result.Warnings.Add(opaqueEvents+" ICP events retained as opaque records (including side visibility); not treated as lane indices.");
            result.Warnings.Add("ICP1 easing mapping validated against 565 paired Enigma plaintext Sky segments (1130 boundary codes). No binary export is implemented.");
            if(ProjectJson.Integer(chart,"note_count",notes.Count)!=notes.Count)result.Warnings.Add("Metadata note_count differs from the records array; using actual array length, never treating it as combo.");
            result.Document=SpcDocument.Parse(text.ToString());return result;
        }
    }
}
