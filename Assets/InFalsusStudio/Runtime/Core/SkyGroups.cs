using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    /// <summary>0.8: explicit group IDs determine heads, independently of gaps.
    /// Geometry only changes group membership in an authoring transaction, never on load.
    /// Negative / absent IDs are independent ungrouped objects, not one global group.</summary>
    public static class SkyGroups
    {
        public const double TimeTolerance=.001, SpaceTolerance=1e-9;
        public static int GroupOf(SpcEvent note)
        {
            double g=note.Get(10,-1);
            return !double.IsNaN(g)&&!double.IsInfinity(g)&&g>=0&&g<=int.MaxValue&&g==Math.Truncate(g)?(int)g:-1;
        }
        public static HashSet<int> Heads(IEnumerable<SpcEvent> source)
        {
            var groups=new HashSet<int>();var heads=new HashSet<int>();
            foreach(var n in source.Where(n=>n.Kind==EventKind.SkyArea&&ChartMath.InvalidReason(n)==null)
                .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId))
            {int group=GroupOf(n);if(group<0||groups.Add(group))heads.Add(n.SourceId);}
            return heads;
        }
        public static bool Joins(SpcEvent earlier,SpcEvent later)
        {
            if(earlier.SourceId==later.SourceId||earlier.TimeMs>=later.TimeMs||
               Math.Abs(earlier.EndMs-later.TimeMs)>TimeTolerance)return false;
            var tail=ChartMath.SkyAt(earlier,earlier.EndMs);var head=ChartMath.SkyAt(later,later.TimeMs);
            return Math.Min(tail.Right,head.Right)+SpaceTolerance>=Math.Max(tail.Left,head.Left);
        }
        private static bool GeometryChanged(SpcEvent a,SpcEvent b)
        {
            if(a==null)return true;
            // Editing only an explicit group is an intentional override, not geometry editing.
            for(int i=0;i<10;i++)if(a.Get(i)!=b.Get(i))return true;
            return false;
        }
        /// <summary>Called on the cloned document BEFORE it enters undo history.
        /// A changed predecessor also propagates its group into unchanged touching successors.
        /// Breaking a connection does NOT split or renumber the existing group.</summary>
        public static void NormalizeAuthoredChanges(SpcDocument before,SpcDocument after)
        {
            var previous=before.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea).ToDictionary(n=>n.SourceId);
            var notes=after.ReadEvents().Where(n=>n.Kind==EventKind.SkyArea&&ChartMath.InvalidReason(n)==null)
                .OrderBy(n=>n.TimeMs).ThenBy(n=>n.SourceId).ToArray();
            var affected=new HashSet<int>();
            foreach(var n in notes)
            {
                SpcEvent old;previous.TryGetValue(n.SourceId,out old);
                if(GeometryChanged(old,n))affected.Add(n.SourceId);
                if(old==null&&GroupOf(n)<0)
                {
                    int g=EditOperations.NextGroup(after);after.SetOptionalNumber(n.SourceId,10,g);
                    n.Args=after.ReadEvents().First(e=>e.SourceId==n.SourceId).Args;
                }
            }
            foreach(var n in notes)
            {
                var candidates=notes.Where(p=>Joins(p,n));
                if(!affected.Contains(n.SourceId)&&!candidates.Any(p=>affected.Contains(p.SourceId)))continue;
                // Prefer the most recent predecessor, then the first source row. This is
                // deterministic when two predecessors overlap; do not union unrelated groups.
                var prior=candidates.OrderByDescending(p=>p.TimeMs).ThenBy(p=>p.SourceId).FirstOrDefault();
                if(prior==null)continue;
                int g=GroupOf(prior);
                if(g<0)
                {
                    g=EditOperations.NextGroup(after);after.SetOptionalNumber(prior.SourceId,10,g);
                    prior.Args=after.ReadEvents().First(e=>e.SourceId==prior.SourceId).Args;affected.Add(prior.SourceId);
                }
                if(GroupOf(n)!=g)
                {
                    after.SetOptionalNumber(n.SourceId,10,g);
                    n.Args=after.ReadEvents().First(e=>e.SourceId==n.SourceId).Args;
                }
                affected.Add(n.SourceId);
            }
        }
    }
}
