using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InFalsusStudio.Core
{
    public struct BeatGridPoint
    {
        public double TimeMs, ExactTimeMs, LocalBeat, GlobalBeat;
        public int Segment;
        public long Index;
        public bool IsBar, IsBeat;
        public int Priority { get { return IsBar ? 3 : IsBeat ? 2 : 1; } }
    }

    // A single source of truth for visible lines, hover highlights and authored times.
    // Always calculate origin + INDEX * exact period, then round ONCE. Never add a
    // rounded period to a previous line. Each positive timing segment starts a local bar.
    public sealed class AuthoringGrid
    {
        private readonly BeatTimeline timeline;
        public AuthoringGrid(BeatTimeline source) { timeline = source ?? throw new ArgumentNullException("source"); }
        public static double RoundMs(double t)
        {
            if (double.IsNaN(t) || double.IsInfinity(t) || Math.Abs(t) > 9e12)
                throw new ArgumentOutOfRangeException("t", "Finite authoring time below 9e12 ms required.");
            return Math.Max(0, Math.Round(t, MidpointRounding.AwayFromZero));
        }
        private static bool Integer(double x) { return Math.Abs(x - Math.Round(x)) < 1e-7; }
        private BeatGridPoint Make(int segment, long index, int division, bool bar)
        {
            var p = timeline.Points[segment];
            double beat = bar ? index * p.Meter : index / (double)division;
            double exact = p.Time + beat * 60000.0 / p.Bpm;
            return new BeatGridPoint { Segment=segment, Index=index, ExactTimeMs=exact, TimeMs=RoundMs(exact),
                LocalBeat=beat, GlobalBeat=p.Beat+beat, IsBar=bar || Integer(beat/p.Meter), IsBeat=Integer(beat) };
        }
        private bool InSegment(BeatGridPoint point)
        {
            var ps=timeline.Points;
            return point.Index>=0 && point.ExactTimeMs>=ps[point.Segment].Time-1e-8 &&
                (point.Segment+1==ps.Count || point.ExactTimeMs<ps[point.Segment+1].Time-1e-8);
        }
        public BeatGridPoint Nearest(double time, int division)
        {
            if(double.IsNaN(time)||double.IsInfinity(time)||Math.Abs(time)>9e12)throw new ArgumentOutOfRangeException("time");
            division=Math.Max(1,division); time=Math.Max(0,time);
            BeatGridPoint best=Make(0,0,division,false); double distance=double.PositiveInfinity;
            for(int s=0;s<timeline.Points.Count;s++)
            {
                var p=timeline.Points[s]; double local=(time-p.Time)*p.Bpm/60000;
                for(int type=0;type<2;type++)
                {
                    bool bar=type==1;double units=bar?p.Meter:1.0/division;
                    long center=(long)Math.Floor(local/units);
                    for(int d=-1;d<=2;d++)
                    {
                        long n=Math.Max(0,center+d); var candidate=Make(s,n,division,bar);
                        if(!InSegment(candidate))continue;
                        // Selection is against the actual integer line that is rendered.
                        double error=Math.Abs(candidate.TimeMs-time);
                        if(error<distance-1e-9 || Math.Abs(error-distance)<1e-9 &&
                            (candidate.TimeMs>best.TimeMs || candidate.TimeMs==best.TimeMs && (candidate.Priority>best.Priority || candidate.Priority==best.Priority && candidate.Segment>best.Segment)))
                        { best=candidate;distance=error; }
                    }
                }
            }
            return best;
        }
        public List<BeatGridPoint> Between(double from, double to, int division, int limit=12000)
        {
            division=Math.Max(1,division);from=Math.Max(0,from);to=Math.Max(from,to);
            Dictionary<double,BeatGridPoint> lines=new Dictionary<double,BeatGridPoint>();
            for(int s=0;s<timeline.Points.Count;s++)
            {
                var p=timeline.Points[s];
                double end=s+1<timeline.Points.Count?Math.Min(to+.51,timeline.Points[s+1].Time):to+.51;
                if(p.Time>end || end<from-.51)continue;
                for(int type=0;type<2;type++)
                {
                    bool bar=type==1;double period=60000.0/p.Bpm*(bar?p.Meter:1.0/division);
                    long first=Math.Max(0,(long)Math.Floor((from-.51-p.Time)/period));
                    long last=(long)Math.Ceiling((end-p.Time)/period);
                    // Bound work as well as output, including extreme sub-millisecond grids.
                    if(last-first>limit*4L)last=first+limit*4L;
                    for(long n=first;n<=last;n++)
                    {
                        var q=Make(s,n,division,bar);
                        if(!InSegment(q)||q.TimeMs<from||q.TimeMs>to)continue;
                        BeatGridPoint previous;
                        if(!lines.TryGetValue(q.TimeMs,out previous)||q.Priority>previous.Priority||q.Priority==previous.Priority&&q.Segment>previous.Segment)lines[q.TimeMs]=q;
                        if(lines.Count>=limit)break;
                    }
                }
                if(lines.Count>=limit)break;
            }
            return lines.Values.OrderBy(q=>q.TimeMs).ToList();
        }
        // Persistent playback measure lines are independent of the editable snap grid.
        // Look through every timing segment in the visible TIME interval, including future
        // BPM/meter changes. origin + absolute bar index, rounded once, never accumulated.
        public List<BeatGridPoint> BarsBetween(double from,double to,int limit=12000)
        {
            if(double.IsNaN(from)||double.IsNaN(to)||double.IsInfinity(from)||double.IsInfinity(to))
                throw new ArgumentOutOfRangeException("from", "Finite time range required.");
            from=Math.Max(0,from);to=Math.Max(from,to);limit=Math.Max(1,limit);
            var lines=new Dictionary<double,BeatGridPoint>();
            for(int s=0;s<timeline.Points.Count;s++)
            {
                var p=timeline.Points[s];
                double end=s+1<timeline.Points.Count?Math.Min(to+.51,timeline.Points[s+1].Time):to+.51;
                if(p.Time>end||end<from-.51)continue;
                double period=60000.0/p.Bpm*p.Meter;
                long first=Math.Max(0,(long)Math.Floor((from-.51-p.Time)/period));
                long last=(long)Math.Ceiling((end-p.Time)/period);
                if(last-first>limit*4L)last=first+limit*4L;
                for(long n=first;n<=last;n++)
                {
                    var q=Make(s,n,1,true);
                    if(!InSegment(q)||q.TimeMs<from||q.TimeMs>to)continue;
                    BeatGridPoint old;
                    if(!lines.TryGetValue(q.TimeMs,out old)||q.Segment>old.Segment)lines[q.TimeMs]=q;
                    if(lines.Count>=limit)break;
                }
                if(lines.Count>=limit)break;
            }
            return lines.Values.OrderBy(q=>q.TimeMs).ToList();
        }
        // One physical wheel detent means one adjacent AUTHORED line. This deliberately
        // does not add a rounded duration, or multiply an input delta by half a beat.
        public double Step(double time,int steps,int division)
        {
            if(double.IsNaN(time)||double.IsInfinity(time)||Math.Abs(time)>9e12)throw new ArgumentOutOfRangeException("time");
            double result=Math.Max(0,time);int sign=steps<0?-1:1;
            int count=(int)Math.Min(4096,Math.Abs((long)steps));
            for(int i=0;i<count;i++)
            {
                double next=Adjacent(result,sign,division);
                if(next==result)break;
                result=next;
            }
            return result;
        }
        public double Adjacent(double time, int sign, int division)
        {
            sign=sign<0?-1:1;double period=60000.0/timeline.Points[Nearest(time,division).Segment].Bpm;
            // Widen until there is a distinct ms line (or hit chart zero).
            double span=Math.Max(10,period*2);
            for(int i=0;i<12;i++,span*=2)
            {
                var lines=Between(Math.Max(0,time-span),time+span,division);
                if(sign>0){foreach(var line in lines)if(line.TimeMs>time+.001)return line.TimeMs;}
                else for(int j=lines.Count-1;j>=0;j--)if(lines[j].TimeMs<time-.001)return lines[j].TimeMs;
                if(sign<0&&time-span<=0)return 0;
            }
            return RoundMs(time);
        }
    }

    // Display grid uses 0..100 for the four CENTRAL lanes. It is independent of
    // a source note's split. Decimal arithmetic avoids binary tails in edited coordinates.
    public sealed class AirAuthoringGrid
    {
        public decimal StepPercent {get;private set;}
        public int Division {get;private set;}
        public AirAuthoringGrid(decimal step=5m) {SetStep(step);}
        public void SetDivision(int divisions)
        {
            if(divisions<1||divisions>4096)throw new ArgumentOutOfRangeException("divisions","Air grid N must be 1..4096.");
            Division=divisions;StepPercent=100m/divisions;
        }
        public void SetStep(decimal step)
        {
            if(step<.1m||step>100m)throw new ArgumentOutOfRangeException("step");
            decimal n=100m/step;
            if(n==decimal.Truncate(n)&&n<=4096){SetDivision((int)n);return;}
            Division=0;StepPercent=step; // preserved legacy session, explicitly shown in UI
        }
        public double Snap(double normalized)
        {
            if(double.IsNaN(normalized)||double.IsInfinity(normalized))throw new ArgumentOutOfRangeException("normalized");
            normalized=Math.Max(0,Math.Min(1,normalized));
            if(Division>0)return Math.Round(normalized*Division,MidpointRounding.AwayFromZero)/Division;
            decimal value=(decimal)normalized*100m;
            decimal snapped=decimal.Round(value/StepPercent,0,MidpointRounding.AwayFromZero)*StepPercent;
            snapped=Math.Max(0m,Math.Min(100m,snapped));if(100m-value<Math.Abs(value-snapped))snapped=100m;
            return (double)(snapped/100m);
        }
        public IEnumerable<double> Ticks()
        {
            if(Division>0){for(int i=0;i<=Division;i++)yield return i/(double)Division;yield break;}
            int n=(int)decimal.Floor(100m/StepPercent);
            for(int i=0;i<=n;i++)yield return (double)(i*StepPercent/100m);
            if(n*StepPercent<100m)yield return 1;
        }
    }

    public static class AuthoredNumber
    {
        // Only for values created by editing operations. SetToken / imported text stay exact.
        // 10 decimal places of a raw chart unit is finer than the allowed authoring grid.
        public static string Format(double number)
        {
            if(double.IsNaN(number)||double.IsInfinity(number))throw new ArgumentException("Non-finite authored value.");
            return number.ToString("0.##########",CultureInfo.InvariantCulture);
        }
        public static void Set(SpcDocument d,int id,int index,double number)
        {
            string value=Format(number);string[] old=d.ArgumentTokens(id);double original;
            if(index<old.Length && double.TryParse(old[index],NumberStyles.Float,CultureInfo.InvariantCulture,out original) &&
                (original==number || original==double.Parse(value,CultureInfo.InvariantCulture)))return;
            d.SetToken(id,index,value);
        }
        public static void AirEdge(SpcDocument d,SpcEvent e,bool tail,bool right,double boundaryNorm)
        {
            int c=tail&&e.Kind==EventKind.SkyArea?4:1,split=c+1,w=c+2;
            decimal s=(decimal)e.Get(split),mid=(decimal)e.Get(c),half=Math.Abs((decimal)e.Get(w))/2m;
            decimal left=right?mid-half:(decimal)boundaryNorm*s;
            decimal r=right?(decimal)boundaryNorm*s:mid+half;
            if(s<=0||r-left<=0)throw new ArgumentException("Width must be positive; the opposite edge stays fixed.");
            RationalAirCoordinates.WriteEndpoint(d,e,tail,(double)((left+r)/(2m*s)),(double)((r-left)/s));
        }
    }
}
