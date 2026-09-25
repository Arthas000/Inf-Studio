using System;

namespace InFalsusStudio.Core
{
    public struct ProjectedPoint { public double X,Y,Depth; }
    public sealed class CameraCalibration
    {
        public double Width=2048,Height=1152,FovDeg=50,HorizonY=35,GroundJudgeY=1029,GroundHalfPixels=590,AirJudgeY=734,SideOuterY=840,SideOuterHalfPixels=900;
        public double Focal,PitchDeg,CameraHeight,CameraBack,AirHeight,SideRise,SideOuterX;
        public static CameraCalibration Reference()
        {
            var p=new CameraCalibration();p.Solve();return p;
        }
        public void Solve()
        {
            Focal=Height*.5/Math.Tan(FovDeg*Math.PI/360);
            double a=Math.Atan((Height*.5-HorizonY)/Focal),c=Math.Cos(a),s=Math.Sin(a);
            PitchDeg=a*180/Math.PI;
            double zc=Focal*2/GroundHalfPixels,yc=(Height*.5-GroundJudgeY)/Focal*zc;
            CameraHeight=-yc*c+zc*s;CameraBack=yc*s+zc*c;
            AirHeight=HeightAt(AirJudgeY,yc,zc,c,s);SideRise=HeightAt(SideOuterY,yc,zc,c,s);
            SideOuterX=SideOuterHalfPixels*(zc-SideRise*s)/Focal;
        }
        private double HeightAt(double py,double yc,double zc,double c,double s)
        {double r=(Height*.5-py)/Focal;return (r*zc-yc)/(c+r*s);}
        public ProjectedPoint Project(double x,double y,double z)
        {
            double a=PitchDeg*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a),yy=(y-CameraHeight)*c+(z+CameraBack)*s,zz=-(y-CameraHeight)*s+(z+CameraBack)*c;
            return new ProjectedPoint{X=Width*.5+Focal*x/zz,Y=Height*.5-Focal*yy/zz,Depth=zz};
        }
    }
}
