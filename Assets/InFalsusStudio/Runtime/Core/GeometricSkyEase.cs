using System;

namespace InFalsusStudio.Core
{
    /// <summary>
    /// A left/right screen gesture chooses the actual boundary midpoint displacement.
    /// SineOut is to the right of Linear when endX > startX, to its left otherwise.
    /// This does not add Bezier/control-point fields to SPC.
    /// </summary>
    public struct SkyEaseGeometry
    {
        public double Start,End;
        public bool RightEdge;
        public double Delta {get{return End-Start;}}
        public bool Stationary {get{return Math.Abs(Delta)<1e-9;}}
        public bool Pinned {get{return Stationary&&(Math.Abs(Start)<1e-9||Math.Abs(Start-1)<1e-9);}}
        public bool CanCurve {get{return !Stationary;}}
        public double Position(int code)
        {return Stationary?0:(code==1?1:code==2?-1:0)*Math.Sign(Delta);}
        public int CodeAtPosition(double position)
        {
            if(Stationary)return 0;
            if(Math.Abs(position)<=.38)return 0;
            return Math.Sign(position)==Math.Sign(Delta)?1:2;
        }
        public int FromDrag(int initialCode,double deltaScreenX)
        {return CodeAtPosition(Position(initialCode)+deltaScreenX/SkyEaseHandle.NotchPixels);}
        public double Midpoint(int code)
        {return Start+Delta*(code==1?Math.Sqrt(.5):code==2?1-Math.Sqrt(.5):.5);}
        public string StraightReason
        {get{return Pinned?"This edge is on the hard wall: its shape is Linear. The OTHER edge is independent.":"This edge has identical start/end X. Move ONE endpoint (H/T or L/TL/R/TR) to curve it. Translating the whole Sky preserves equal X.";}}
    }
    public static class GeometricSkyEase
    {
        public static SkyEaseGeometry Describe(SpcEvent e,bool right)
        {
            if(e==null||e.Kind!=EventKind.SkyArea)throw new ArgumentException("Select a SkyArea.");
            SkyRange a=ChartMath.SkyAt(e,e.TimeMs),b=ChartMath.SkyAt(e,e.EndMs);
            return new SkyEaseGeometry{Start=right?a.Right:a.Left,End=right?b.Right:b.Left,RightEdge=right};
        }
    }
}
