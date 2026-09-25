using System;

namespace InFalsusStudio.Core
{
    /// <summary>Mouse-authored ratios are stored as integer numerator/divisor pairs,
    /// just like the supplied charts. 1/3 never becomes 0.333 followed by accumulated drift.
    /// Only the edited endpoint is re-encoded; raw text edits are not normalized.</summary>
    public static class RationalAirCoordinates
    {
        private const long Limit=100000000;
        private static long Gcd(long a,long b){while(b!=0){long t=a%b;a=b;b=t;}return Math.Abs(a);}
        private static bool Fraction(double value,out long numerator,out long denominator)
        {
            numerator=0;denominator=1;
            if(double.IsNaN(value)||double.IsInfinity(value)||Math.Abs(value)>1000)return false;
            long sign=value<0?-1:1;double target=Math.Abs(value),x=target;
            long h0=0,h1=1,k0=1,k1=0;
            for(int i=0;i<40;i++)
            {
                double whole=Math.Floor(x);if(whole>Limit)return false;long a=(long)whole;
                if(a>0&&(h1>(Limit-h0)/a||k1>(Limit-k0)/a))return false;
                long h=a*h1+h0,k=a*k1+k0;if(k<=0||k>Limit)return false;
                if(Math.Abs(target-h/(double)k)<=1e-11){numerator=sign*h;denominator=k;return true;}
                h0=h1;h1=h;k0=k1;k1=k;double rem=x-whole;if(rem<=1e-15)return false;x=1/rem;
            }
            return false;
        }
        public static bool Encode(double center,double width,double preferredSplit,out double x,out double split,out double w)
        {
            x=center*preferredSplit;w=width*preferredSplit;split=preferredSplit;
            if(preferredSplit>0&&preferredSplit<=Limit&&preferredSplit==Math.Truncate(preferredSplit)&&
               Math.Abs(x-Math.Round(x))<1e-10&&Math.Abs(w-Math.Round(w))<1e-10)
            {x=Math.Round(x);w=Math.Round(w);return true;}
            long cn,cd,wn,wd;
            if(!Fraction(center,out cn,out cd)||!Fraction(width,out wn,out wd))return false;
            long a=cd/Gcd(cd,wd);if(a>Limit/wd)return false;long common=a*wd;
            x=cn*(common/cd);w=wn*(common/wd);split=common;return true;
        }
        private static bool Terminates(double value)
        {
            long n,d;if(!Fraction(value,out n,out d))return false;
            int twos=0,fives=0;
            while(d%2==0){d/=2;twos++;}while(d%5==0){d/=5;fives++;}
            return d==1&&Math.Max(twos,fives)<=10;
        }
        public static void WriteEndpoint(SpcDocument doc,SpcEvent e,bool tail,double center,double width)
        {
            int ci=e.Kind==EventKind.SkyArea&&tail?4:1;
            double x,split,w;
            double oldSplit=e.Get(ci+1);
            // Preserve an old divisor when the edited scalars have an exact terminating
            // decimal representation. Only recurring fractions require a new divisor.
            // E.g. old split 200 may legitimately retain center 104.5; 100/3 must not
            // become a repeatedly rounded 33.333... token.
            if(Terminates(center*oldSplit)&&Terminates(width*oldSplit))
            {x=center*oldSplit;w=width*oldSplit;split=oldSplit;}
            else if(!Encode(center,width,oldSplit,out x,out split,out w))
                throw new ArgumentException("坐标分母超过安全范围，操作未提交。 Coordinate ratio exceeds the supported denominator; use explicit source editing instead.");
            AuthoredNumber.Set(doc,e.SourceId,ci,x);AuthoredNumber.Set(doc,e.SourceId,ci+1,split);AuthoredNumber.Set(doc,e.SourceId,ci+2,w);
        }
    }
}
