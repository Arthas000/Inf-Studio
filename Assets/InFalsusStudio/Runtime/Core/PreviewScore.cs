using System;

namespace InFalsusStudio.Core
{
    /// <summary>
    /// Editor-only AUTO EXACT estimate, NOT a verified game score model.
    /// Counts come from ProvisionalComboTimeline (original times, not visual swipe delays).
    /// floor(100,000,000 * completed / total) + completed; no per-tick rounded accumulation.
    /// </summary>
    public static class PreviewScore
    {
        public const long BaseMaximum=100000000L;
        public static long At(long completed,long total)
        {
            if(total<0)throw new ArgumentOutOfRangeException("total");
            if(total==0)return 0;
            completed=Math.Max(0,Math.Min(total,completed));
            return checked((long)decimal.Floor((decimal)BaseMaximum*completed/total)+completed);
        }
        public static long Maximum(long total)
        {if(total<0)throw new ArgumentOutOfRangeException("total");return checked(BaseMaximum+total);}
    }
}
