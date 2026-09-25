using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    public sealed class ComboAtTime
    {
        public int Tap,Hold,SkyArea,Flick;
        public int Total {get{return checked(Tap+Hold+SkyArea+Flick);}}
        public void Add(EventKind kind,int value)
        {
            checked{switch(kind){case EventKind.Tap:Tap+=value;break;case EventKind.Hold:Hold+=value;break;case EventKind.SkyArea:SkyArea+=value;break;case EventKind.Flick:Flick+=value;break;}}
        }
    }
    public sealed class ProvisionalComboNote
    {
        public int Id,Total;
        public EventKind Kind;
        public double Start,End,StartBpm,IntervalMs;
        public bool CrossesBpm;
        // Hypothesis A: Hold = head + interior half-beat-ms ticks + tail;
        // Sky = start + periodic ticks, with count's end-minus-1ms convention.
        // These timestamps have NOT been certified by total-count agreement.
        public int At(double time)
        {
            if(time<Start||Total==0)return 0;
            if(Kind==EventKind.Tap||Kind==EventKind.Flick)return 1;
            if(time>=End)return Total;
            int limit=Kind==EventKind.Hold?Total-1:Total;
            return Math.Min(limit,1+(int)Math.Floor((time-Start)/IntervalMs));
        }
        public double TickTime(int index)
        {
            if(index<0||index>=Total)throw new ArgumentOutOfRangeException("index");
            if(Kind==EventKind.Tap||Kind==EventKind.Flick)return Start;
            if(Kind==EventKind.Hold&&index==Total-1)return End;
            return Start+index*IntervalMs;
        }
        public double? NextAfter(double time)
        {int done=At(time);return done<Total?(double?)TickTime(done):null;}
    }
    /// <summary>
    /// Absolute-time, random-access preview of hypothesis A. track never enters count math.
    /// No smoothing, deltaTime accumulation, score inference or visual-Flick delays.
    /// </summary>
    public sealed class ProvisionalComboTimeline
    {
        private readonly List<ProvisionalComboNote> notes=new List<ProvisionalComboNote>();
        public IList<ProvisionalComboNote> Notes {get{return notes.AsReadOnly();}}
        public bool Supported {get;private set;}
        public string Warning {get;private set;}
        public ComboAtTime Totals {get;private set;}
        public string Profile {get{return "A/start-BPM/ceil-halfbeat-ms; judgement TIMES provisional";}}
        public ProvisionalComboTimeline(IEnumerable<SpcEvent> source)
        {
            var all=source.ToArray();var summary=TickCountResearch.Analyze(all);
            Supported=summary.Supported;Warning=summary.Warning;Totals=new ComboAtTime();
            if(!Supported)return;
            double initial=all.First(e=>e.Kind==EventKind.Chart).Get(0);
            var changes=all.Where(e=>e.Kind==EventKind.Bpm).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId).ToArray();
            foreach(var e in all.Where(e=>e.IsNote).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId))
            {
                double bpm=initial;foreach(var b in changes){if(b.TimeMs>e.TimeMs)break;bpm=b.Get(1);}
                int n=TickCountResearch.Count(e.Kind,e.DurationMs,bpm);
                var item=new ProvisionalComboNote{Id=e.SourceId,Kind=e.Kind,Start=e.TimeMs,End=e.EndMs,StartBpm=bpm,IntervalMs=Math.Ceiling(30000.0/bpm),Total=n,
                    CrossesBpm=changes.Any(b=>b.TimeMs>e.TimeMs&&b.TimeMs<e.EndMs&&Math.Abs(b.Get(1)-bpm)>1e-9)};
                notes.Add(item);Totals.Add(item.Kind,n);
            }
        }
        public ComboAtTime At(double time)
        {
            var value=new ComboAtTime();if(!Supported||double.IsNaN(time))return value;
            foreach(var n in notes){if(n.Start>time)break;value.Add(n.Kind,n.At(time));}return value;
        }
        public ProvisionalComboNote Find(int id){return notes.FirstOrDefault(n=>n.Id==id);}
        public double? NextAfter(double time)
        {
            double? next=null;foreach(var n in notes){double? t=n.NextAfter(time);if(t.HasValue&&(!next.HasValue||t.Value<next.Value))next=t;}return next;
        }
        public string Report(double time,string chartHash)
        {
            var current=At(time);var rows=new List<object>();
            foreach(var n in notes)rows.Add(new Dictionary<string,object>{{"sourceId",n.Id},{"kind",n.Kind.ToString()},{"startMs",n.Start},{"endMs",n.End},
                {"startBpm",n.StartBpm},{"tickIntervalMs",n.IntervalMs},{"totalCandidate",n.Total},{"candidateAtPlayhead",n.At(time)},
                {"nextTickMs",n.NextAfter(time)},{"crossesBpmChange",n.CrossesBpm}});
            return ProjectJson.Write(new Dictionary<string,object>{{"format","InFalsusProvisionalComboReport"},{"version",1},{"profile",Profile},{"provisional",true},
                {"scoreCalculated",false},{"supported",Supported},{"warning",Warning},{"chartSha256",chartHash},{"chartTimeMs",time},
                {"totalCandidate",Totals.Total},{"candidateAtPlayhead",current.Total},{"tapAtPlayhead",current.Tap},{"holdAtPlayhead",current.Hold},
                {"skyAtPlayhead",current.SkyArea},{"flickAtPlayhead",current.Flick},{"notes",rows}});
        }
    }
}
