using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    // The tested left edge x=1/12,w=2/12 is exactly 0; its mirror is 1.
    // These are normalized chart-space limits, not screen-pixel limits or all six lanes.
    public static class AirReachBounds
    {
        public const double Left=0, Right=1, Tolerance=1e-9;
        public static double Clamp(double x){return Math.Max(Left,Math.Min(Right,x));}
        public static SkyRange Clip(SkyRange r){return new SkyRange(Clamp(r.Left),Clamp(r.Right));}
        public static bool Inside(SkyRange r)
        {return r.Left>=Left-Tolerance&&r.Right<=Right+Tolerance&&r.Left<=r.Right+Tolerance;}
        public static bool IsAir(SpcEvent e){return e.Kind==EventKind.SkyArea||e.Kind==EventKind.Flick;}
        public static string Violation(SpcEvent e)
        {
            if(!IsAir(e)||ChartMath.InvalidReason(e)!=null)return null;
            SkyRange first=e.Kind==EventKind.Flick?ChartMath.FlickRange(e):ChartMath.SkyAt(e,e.TimeMs);
            SkyRange last=e.Kind==EventKind.Flick?first:ChartMath.SkyAt(e,e.EndMs);
            // Native 0/1/2 eases are monotonic on each boundary. Endpoint extrema suffice
            // for REACH bounds; left/right crossing has its separate continuous diagnostic.
            if(!Inside(first)||!Inside(last))return "Air range exceeds the playable 0..100 boundary. Source retained; preview is clipped.";
            return null;
        }
        public static void Require(SkyRange r)
        {if(!Inside(r))throw new ArgumentException("Air edges must remain inside 0..100. Width is not silently reduced.");}
        public static double CenterWithin(double center,double width)
        {
            if(double.IsNaN(center)||double.IsInfinity(center)||double.IsNaN(width)||double.IsInfinity(width)||width<0||width>1+Tolerance)
                throw new ArgumentException("Air width must be in 0..100 percent.");
            double half=Math.Min(1,width)*.5;return Math.Max(half,Math.Min(1-half,center));
        }
        public static double ClampDelta(IEnumerable<SpcEvent> notes,double requested)
        {
            double lo=double.NegativeInfinity,hi=double.PositiveInfinity;
            foreach(var e in notes.Where(IsAir))
            {
                if(ChartMath.InvalidReason(e)!=null)continue;
                var a=e.Kind==EventKind.Flick?ChartMath.FlickRange(e):ChartMath.SkyAt(e,e.TimeMs);
                var b=e.Kind==EventKind.Flick?a:ChartMath.SkyAt(e,e.EndMs);
                lo=Math.Max(lo,-Math.Min(a.Left,b.Left));hi=Math.Min(hi,1-Math.Max(a.Right,b.Right));
            }
            if(lo>hi+Tolerance)throw new ArgumentException("This air selection cannot fit inside 0..100 without resizing. Edit its source or individual edges first.");
            return Math.Max(lo,Math.Min(hi,requested));
        }
        public static void RequireChanged(SpcDocument doc,IEnumerable<int> ids)
        {
            var set=new HashSet<int>(ids);
            foreach(var e in doc.ReadEvents().Where(e=>set.Contains(e.SourceId)))
                if(Violation(e)!=null)throw new ArgumentException("Edit refused: air edges would exceed 0..100. The original source is unchanged.");
        }
    }
}
