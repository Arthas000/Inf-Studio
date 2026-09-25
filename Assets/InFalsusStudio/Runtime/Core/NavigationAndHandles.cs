using System;

namespace InFalsusStudio.Core
{
    /// <summary>Resolve a creation brush, not an imported note. Width never shrinks in central lanes.</summary>
    public static class GroundPlacement
    {
        public static bool TryResolve(int hitLane, int requestedWidth, out int startLane, out int width)
        {
            startLane=hitLane;width=requestedWidth;
            if(hitLane<0||hitLane>5)return false;
            if(hitLane==0||hitLane==5){width=1;return true;}
            if(width<1||width>4)return false;
            startLane=Math.Max(1,Math.Min(hitLane,5-width));
            return true;
        }
    }

    /// <summary>
    /// IMGUI scroll units are not an OS wheel-notch count. Windows typically reports 3
    /// units per notch; the runtime exposes this scale (1 or 3) for other input backends.
    /// Partial deltas are accumulated, not converted to a full step on every event.
    /// Negative IMGUI Y = wheel up = positive chart-time step.
    /// </summary>
    public sealed class WheelStepAccumulator
    {
        private double remainder;
        public double UnitsPerStep {get;private set;}
        public WheelStepAccumulator(double unitsPerStep=3){Configure(unitsPerStep);}
        public void Configure(double unitsPerStep)
        {
            if(double.IsNaN(unitsPerStep)||double.IsInfinity(unitsPerStep)||unitsPerStep<=0)
                throw new ArgumentOutOfRangeException("unitsPerStep");
            UnitsPerStep=unitsPerStep;Reset();
        }
        public void Reset(){remainder=0;}
        public int Consume(double deltaY)
        {
            if(double.IsNaN(deltaY)||double.IsInfinity(deltaY))return 0;
            remainder-=deltaY/UnitsPerStep;
            // Bound pathological device events, not normal accelerated scrolling.
            remainder=Math.Max(-1024,Math.Min(1024,remainder));
            int n=remainder>=0?(int)Math.Floor(remainder+1e-7):(int)Math.Ceiling(remainder-1e-7);
            remainder-=n;return n;
        }
    }

    /// <summary>A discrete editing selector. This is NOT a new SPC curvature parameter.</summary>
    public static class SkyEaseHandle
    {
        public const double NotchPixels=32;
        // Positive means outward. Each edge has an independent selector; only arg7/8 changes.
        public static double Position(int code){return code==1?1:code==2?-1:0;}
        public static int FromDrag(int initialCode, bool rightEdge, double deltaScreenX)
        {
            double outward=(rightEdge?1:-1)*deltaScreenX;
            double position=Position(initialCode)+outward/NotchPixels;
            if(position>.38)return 1; // SineOut
            if(position<-.38)return 2; // SineIn
            return 0;                 // Linear dead band
        }
        public static string Name(int code){return code==1?"SineOut":code==2?"SineIn":"Linear";}
    }
}
