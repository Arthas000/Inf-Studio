using System;
namespace InFalsusStudio.Core
{
    public struct HudBox
    {public double X,Y,W,H;public HudBox(double x,double y,double w,double h){X=x;Y=y;W=w;H=h;}}
    public static class ScoreHudLayout
    {
        public static HudBox[] Digits(double width,double height)
        {
            if(width<=0||height<=0)throw new ArgumentException("HUD size must be positive.");
            double pad=width*.012,gap=width*.010,sep=width*.012;
            double digit=(width-2*pad-8*gap-2*sep)/9,x=pad;
            var result=new HudBox[9];
            for(int i=0;i<9;i++){result[i]=new HudBox(x,height*.04,digit,height*.92);x+=digit;if(i<8)x+=gap;if(i==2||i==5)x+=sep;}
            return result;
        }
        public static HudBox Separator(double width,double height,int after)
        {
            if(after!=2&&after!=5)throw new ArgumentOutOfRangeException("after");
            var b=Digits(width,height)[after];double gap=width*.010,sep=width*.012;
            return new HudBox(b.X+b.W+gap*.5+sep*.30,height*.16,sep*.35,height*.12);
        }
    }
}
