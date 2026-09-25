using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    public sealed class ComboCountSummary
    {
        public int Tap,Hold,SkyArea,Flick;
        public bool BpmChanges,Supported=true,Verified;
        public string Evidence="",Warning="";
        public int Total {get{return checked(Tap+Hold+SkyArea+Flick);}}
    }
    // A fitted, explicitly provisional rule. All five supplied per-type totals match.
    // Other formulas ALSO match those totals. Do NOT silently promote this to a verified
    // judgement schedule or score formula. Genuine start-BPM changes are absent in samples.
    public static class TickCountResearch
    {
        public const string Profile="ceil-halfbeat-ms / hold endpoints / sky end-minus-1ms (provisional)";
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;
        public static int Count(EventKind kind,double duration,double bpm)
        {
            if(kind==EventKind.Tap||kind==EventKind.Flick)return 1;
            if(kind!=EventKind.Hold&&kind!=EventKind.SkyArea)return 0;
            if(double.IsNaN(duration)||double.IsInfinity(duration)||duration<0||double.IsNaN(bpm)||double.IsInfinity(bpm)||bpm<=0)
                throw new ArgumentException("Counting requires finite nonnegative duration and positive BPM.");
            double interval=Math.Ceiling(30000.0/bpm);
            double n=kind==EventKind.Hold?Math.Max(2,Math.Ceiling(duration/interval)+1):Math.Max(1,Math.Ceiling((duration-1)/interval));
            if(n>10000000)throw new ArgumentException("Candidate count exceeds safety limit.");return (int)n;
        }
        public static ComboCountSummary Analyze(IEnumerable<SpcEvent> source,string referenceJson=null)
        {
            var all=source.ToArray();var summary=new ComboCountSummary();
            var header=all.FirstOrDefault(e=>e.Kind==EventKind.Chart);
            if(header==null||header.Get(0)<=0){summary.Supported=false;summary.Warning="No positive initial BPM.";return summary;}
            var bpmEvents=all.Where(e=>e.Kind==EventKind.Bpm).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId).ToArray();
            double previous=header.Get(0);
            foreach(var b in bpmEvents)
            {
                if(b.Get(1)<=0){summary.Supported=false;summary.Warning="Nonpositive BPM semantics are unverified.";return summary;}
                if(Math.Abs(b.Get(1)-previous)>1e-9)summary.BpmChanges=true;previous=b.Get(1);
            }
            foreach(var e in all.Where(e=>e.IsNote))
            {
                double bpm=header.Get(0);foreach(var b in bpmEvents){if(b.TimeMs>e.TimeMs)break;bpm=b.Get(1);}
                int n=Count(e.Kind,e.DurationMs,bpm);
                checked{switch(e.Kind){case EventKind.Tap:summary.Tap+=n;break;case EventKind.Hold:summary.Hold+=n;break;case EventKind.SkyArea:summary.SkyArea+=n;break;case EventKind.Flick:summary.Flick+=n;break;}}
            }
            summary.Warning=summary.BpmChanges?"EXPERIMENTAL: start-BPM versus dynamic-BPM behavior not resolved by supplied data.":"Fitted totals; tick timestamps and endpoint policy are not uniquely determined.";
            bool unknown=all.Any(e=>e.Kind==EventKind.Unknown&&(e.Name!="icp_event"||e.Get(1)!=3))||all.Any(e=>e.IsNote&&ChartMath.InvalidReason(e)!=null);
            if(unknown){summary.Supported=false;summary.Warning="Unknown/invalid note or timing data: partial model count is not verified.";}
            if(summary.Supported&&!string.IsNullOrEmpty(referenceJson))
            {
                string signature=SemanticSignature(all);
                var records=ProjectJson.Array(ProjectJson.Get(ProjectJson.Object(ProjectJson.Parse(referenceJson)),"references"));
                foreach(var entry in records)
                {
                    var r=ProjectJson.Object(entry);if(ProjectJson.Text(r,"signature")!=signature)continue;
                    if(summary.Tap!=ProjectJson.Integer(r,"tap")||summary.Hold!=ProjectJson.Integer(r,"hold")||summary.SkyArea!=ProjectJson.Integer(r,"skyarea")||summary.Flick!=ProjectJson.Integer(r,"flick"))
                        throw new InvalidOperationException("Fitted model disagrees with this measured reference. Do not display a verified count.");
                    summary.Verified=true;summary.Evidence=ProjectJson.Text(r,"name");break;
                }
            }
            return summary;
        }
        private static string N(double x){return x.ToString("0.##########",CI);}
        // Mathematical-equivalent rational scales compare equal. All note geometry and
        // known BPM data are included; editing a note invalidates the matched reference.
        // Track/lane/opaque side-visibility do not enter this note/count evidence identity.
        public static string SemanticSignature(IEnumerable<SpcEvent> source)
        {
            var lines=new List<string>();
            foreach(var e in source)
            {
                string line=null;
                switch(e.Kind)
                {
                    case EventKind.Chart:line="chart|"+N(e.Get(0))+"|"+N(e.Get(1));break;
                    case EventKind.Bpm:line="bpm|"+N(e.TimeMs)+"|"+N(e.Get(1))+"|"+N(e.Get(2,-1));break;
                    case EventKind.Tap:line="tap|"+N(e.TimeMs)+"|"+N(e.GroundWidth)+"|"+e.Lane;break;
                    case EventKind.Hold:line="hold|"+N(e.TimeMs)+"|"+e.Lane+"|"+N(e.GroundWidth)+"|"+N(e.DurationMs);break;
                    case EventKind.Flick:line="flick|"+N(e.TimeMs)+"|"+N(e.Get(1)/e.Get(2))+"|"+N(e.Get(3)/e.Get(2))+"|"+N(e.Get(4));break;
                    case EventKind.SkyArea:line="skyarea|"+N(e.TimeMs)+"|"+N(e.Get(1)/e.Get(2))+"|"+N(e.Get(3)/e.Get(2))+"|"+N(e.Get(4)/e.Get(5))+"|"+N(e.Get(6)/e.Get(5))+"|"+N(e.Get(7))+"|"+N(e.Get(8))+"|"+N(e.DurationMs)+"|"+N(e.Get(10,-1));break;
                }
                if(line!=null)lines.Add(line);
            }
            lines.Sort(StringComparer.Ordinal);return SongFolderProject.Sha256(Encoding.UTF8.GetBytes(string.Join("\n",lines.ToArray())+"\n"));
        }
    }
}
