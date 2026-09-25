using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    public struct SkyRange
    {
        public double Left, Right;
        public double Center { get { return (Left + Right) * 0.5; } }
        public double Width { get { return Right - Left; } }
        public SkyRange(double left, double right) { Left = left; Right = right; }
    }
    public static class ChartMath
    {
        public static double Clamp01(double x) { return Math.Max(0, Math.Min(1, x)); }
        public static double Ease(int code, double t)
        {
            t = Clamp01(t);
            switch (code) { case 0: return t; case 1: return Math.Sin(t * Math.PI * .5); case 2: return 1 - Math.Cos(t * Math.PI * .5); default: throw new ArgumentException("Unknown ease code " + code); }
        }
        public static bool ValidGround(SpcEvent e)
        {
            if (e.Kind != EventKind.Tap && e.Kind != EventKind.Hold) return false;
            double lane = e.Get(e.Kind == EventKind.Tap ? 2 : 1), w = e.GroundWidth;
            if (lane != Math.Round(lane) || w != Math.Round(w) || lane < 0 || lane > 5 || w < 1) return false;
            return lane == 0 || lane == 5 ? w == 1 : lane + w <= 5;
        }
        public static SkyRange SkyAt(SpcEvent e, double timeMs)
        {
            double ss = e.Get(2), es = e.Get(5);
            if (ss <= 0 || es <= 0) throw new ArgumentException("Sky coordinate divisor must be positive.");
            double cs = e.Get(1) / ss, ce = e.Get(4) / es;
            double ws = Math.Abs(e.Get(3) / ss), we = Math.Abs(e.Get(6) / es);
            double u = e.DurationMs > 0 ? Clamp01((timeMs - e.TimeMs) / e.DurationMs) : 0;
            double l0 = cs - ws / 2, r0 = cs + ws / 2, l1 = ce - we / 2, r1 = ce + we / 2;
            // NO 5% minimum; NO endpoint clamping; NO interpolation of raw numerators.
            return new SkyRange(l0 + (l1 - l0) * Ease((int)e.Get(7), u), r0 + (r1 - r0) * Ease((int)e.Get(8), u));
        }
        // Exact candidates for the currently supported 0/1/2 eases: width'(u)
        // is A+B*sin(pi*u/2)+C*cos(pi*u/2). Include every interior root, not just
        // a few mesh samples. This diagnoses crossed edges without rewriting source.
        public static double SkyMinimumWidth(SpcEvent e,out double progress)
        {
            if(e.Kind!=EventKind.SkyArea||InvalidReason(e)!=null)throw new ArgumentException("A valid SkyArea is required.");
            SkyRange begin=SkyAt(e,e.TimeMs),end=SkyAt(e,e.EndMs);
            progress=0;double minimum=begin.Width;
            if(e.DurationMs<=0)return minimum;
            if(end.Width<minimum){minimum=end.Width;progress=1;}
            double a=0,b=0,c=0,k=Math.PI*.5;
            int[] codes={(int)e.Get(8),(int)e.Get(7)};
            double[] deltas={end.Right-begin.Right,begin.Left-end.Left};
            for(int i=0;i<2;i++)
            {if(codes[i]==0)a+=deltas[i];else if(codes[i]==1)c+=k*deltas[i];else b+=k*deltas[i];}
            double radius=Math.Sqrt(b*b+c*c);
            if(radius<1e-14||Math.Abs(a)>radius+1e-12)return minimum;
            double phi=Math.Atan2(c,b),alpha=Math.Asin(Math.Max(-1,Math.Min(1,-a/radius)));
            for(int n=-2;n<=2;n++)foreach(double theta in new[]{alpha-phi+2*Math.PI*n,Math.PI-alpha-phi+2*Math.PI*n})
            {
                double u=theta/k;if(u<=0||u>=1)continue;
                double w=SkyAt(e,e.TimeMs+e.DurationMs*u).Width;
                if(w<minimum){minimum=w;progress=u;}
            }
            return minimum;
        }
        public static SkyRange FlickRange(SpcEvent e)
        {
            double split = e.Get(2); if (split <= 0) throw new ArgumentException("Flick divisor must be positive.");
            double c = e.Get(1) / split, w = Math.Abs(e.Get(3) / split); return new SkyRange(c - w * .5, c + w * .5);
        }
        public static string InvalidReason(SpcEvent e)
        {
            if (e.Kind == EventKind.Tap || e.Kind == EventKind.Hold)
            { if (!ValidGround(e)) return "invalid lane/width (not silently rounded)"; if (e.DurationMs < 0) return "negative duration"; }
            if (e.Kind == EventKind.Flick)
            { if (e.Get(2) <= 0) return "non-positive x_split"; if (e.Get(4) != 4 && e.Get(4) != 16) return "unknown Flick direction"; }
            if (e.Kind == EventKind.SkyArea)
            {
                if (e.Get(2) <= 0 || e.Get(5) <= 0) return "non-positive sky divisor";
                if (e.DurationMs < 0) return "negative duration";
                if (e.Get(7) < 0 || e.Get(7) > 2 || e.Get(7) != Math.Round(e.Get(7)) || e.Get(8) < 0 || e.Get(8) > 2 || e.Get(8) != Math.Round(e.Get(8))) return "unknown sky ease";
            }
            return null;
        }
    }

    // S(t) = integral of track speed in equivalent milliseconds. BPM is NOT a factor.
    public sealed class ScrollTimeline
    {
        public struct Point { public double Time, Speed, Position; }
        private readonly List<Point> points = new List<Point>();
        public IList<Point> Points { get { return points.AsReadOnly(); } }
        public ScrollTimeline(IEnumerable<SpcEvent> events)
        {
            // Coalesce equal timestamps stably: later source line wins.
            var tracks = events.Where(e => e.Kind == EventKind.Track).OrderBy(e => e.TimeMs).ThenBy(e => e.SourceId).ToList();
            points.Add(new Point { Time = 0, Speed = 1, Position = 0 });
            foreach (var e in tracks)
            {
                if (e.TimeMs <= 0) { var b = points[0]; b.Speed = e.Get(1); points[0] = b; continue; }
                int i = points.Count - 1; var last = points[i];
                if (e.TimeMs == last.Time) { last.Speed = e.Get(1); points[i] = last; }
                else points.Add(new Point { Time = e.TimeMs, Speed = e.Get(1), Position = last.Position + (e.TimeMs - last.Time) * last.Speed });
            }
        }
        public double PositionAt(double time)
        {
            int lo = 0, hi = points.Count;
            while (lo < hi) { int m = (lo + hi) / 2; if (points[m].Time <= time) lo = m + 1; else hi = m; }
            var p = points[Math.Max(0, lo - 1)]; return p.Position + (time - p.Time) * p.Speed;
        }
        public double Delta(double t, double now) { return PositionAt(t) - PositionAt(now); }
        public IEnumerable<double> Breakpoints(double begin, double end)
        { foreach (var p in points) if (p.Time > begin && p.Time < end) yield return p.Time; }
        public struct TimeCandidate { public double Start, End; public bool IsInterval; }
        public List<TimeCandidate> Inverse(double target, double minTime, double maxTime)
        {
            var result = new List<TimeCandidate>();
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i]; double a = Math.Max(minTime, p.Time), b = Math.Min(maxTime, i + 1 < points.Count ? points[i + 1].Time : maxTime);
                if (b < a) continue;
                if (Math.Abs(p.Speed) < 1e-12)
                { if (Math.Abs(p.Position - target) < 1e-8) result.Add(new TimeCandidate { Start = a, End = b, IsInterval = true }); }
                else { double t = p.Time + (target - p.Position) / p.Speed; if (t >= a - 1e-8 && t <= b + 1e-8) result.Add(new TimeCandidate { Start = t, End = t }); }
            }
            return result;
        }
    }

    // Positive BPM segments for beat-line display and optional snapping. Unknown BPM semantics stay raw.
    public sealed class BeatTimeline
    {
        public struct Point { public double Time, Beat, Bpm, Meter; }
        private readonly List<Point> p = new List<Point>();
        public IList<Point> Points { get { return p.AsReadOnly(); } }
        public BeatTimeline(IEnumerable<SpcEvent> events)
        {
            var all = events.ToList(); var h = all.FirstOrDefault(e => e.Kind == EventKind.Chart);
            p.Add(new Point { Time = 0, Beat = 0, Bpm = h != null && h.Get(0) > 0 ? h.Get(0) : 130, Meter = h != null && h.Get(1) > 0 ? h.Get(1) : 4 });
            foreach (var e in all.Where(x => x.Kind == EventKind.Bpm).OrderBy(x => x.TimeMs).ThenBy(x => x.SourceId))
            {
                if (e.Get(1) <= 0) continue; var prev = p[p.Count - 1];
                var n = new Point { Time = Math.Max(0, e.TimeMs), Beat = prev.Beat + (Math.Max(0,e.TimeMs) - prev.Time) * prev.Bpm / 60000, Bpm = e.Get(1), Meter = e.Get(2, -1) > 0 ? e.Get(2) : prev.Meter };
                if (n.Time == prev.Time) p[p.Count - 1] = n; else p.Add(n);
            }
        }
        public double BeatAt(double time) { var a = p[0]; foreach (var n in p) { if (n.Time > time) break; a = n; } return a.Beat + (time - a.Time) * a.Bpm / 60000; }
        public double TimeAt(double beat) { var a = p[0]; foreach (var n in p) { if (n.Beat > beat) break; a = n; } return a.Time + (beat - a.Beat) * 60000 / a.Bpm; }
        public double Snap(double time, int division) { return TimeAt(Math.Round(BeatAt(time) * Math.Max(1, division)) / Math.Max(1, division)); }
    }
}
