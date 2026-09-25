using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    public enum HitSoundKind { FloorHit, SideHit, SkyHit, Flick, FloorHold, SkyHold }
    public enum SkyAttackPolicy { EverySegment, ContinuousRegionStart, Off }
    public sealed class HitSoundCue
    {
        public int SourceId;
        public HitSoundKind Kind;
        public double Start,End;
        public bool Loop {get{return Kind==HitSoundKind.FloorHold||Kind==HitSoundKind.SkyHold;}}
    }
    /// <summary>
    /// Chart-driven audition, no keyboard simulation and no combo tick-based stutter.
    /// Ground and Sky hold beds are TWO independent buses. Each bus uses the union of
    /// touching/overlapping spans to avoid layering identical loops for every source segment.
    /// This routing is an explicit editor policy, not a recovered game sound implementation.
    /// </summary>
    public sealed class HitSoundPlan
    {
        public readonly List<HitSoundCue> Shots=new List<HitSoundCue>();
        public readonly List<HitSoundCue> Holds=new List<HitSoundCue>();
        public int SkippedInvalid {get;private set;}
        public HitSoundPlan(IEnumerable<SpcEvent> source,SkyAttackPolicy skyAttacks=SkyAttackPolicy.EverySegment,bool groundHoldAttacks=false)
        {
            var all=source.ToArray();var connections=new SkyConnections(all);
            var floor=new List<HitSoundCue>();var sky=new List<HitSoundCue>();
            foreach(var e in all)
            {
                if(!e.IsNote)continue;
                if(ChartMath.InvalidReason(e)!=null||double.IsNaN(e.TimeMs)||double.IsInfinity(e.TimeMs)||e.TimeMs<0)
                {SkippedInvalid++;continue;}
                if(e.Kind==EventKind.Tap || e.Kind==EventKind.Hold&&groundHoldAttacks)
                    Shot(e,(e.Lane==0||e.Lane==5)?HitSoundKind.SideHit:HitSoundKind.FloorHit);
                if(e.Kind==EventKind.Flick)Shot(e,HitSoundKind.Flick);
                if(e.Kind==EventKind.SkyArea&&skyAttacks==SkyAttackPolicy.EverySegment)Shot(e,HitSoundKind.SkyHit);
                if(e.DurationMs<=0)continue;
                if(e.Kind==EventKind.Hold)floor.Add(new HitSoundCue{SourceId=e.SourceId,Kind=HitSoundKind.FloorHold,Start=e.TimeMs,End=e.EndMs});
                if(e.Kind==EventKind.SkyArea)sky.Add(new HitSoundCue{SourceId=e.SourceId,Kind=HitSoundKind.SkyHold,Start=e.TimeMs,End=e.EndMs});
            }
            var skyUnion=Union(sky);Holds.AddRange(Union(floor));Holds.AddRange(skyUnion);
            if(skyAttacks==SkyAttackPolicy.ContinuousRegionStart)
                foreach(var note in all)if(connections.Starts.Contains(note.SourceId))Shot(note,HitSoundKind.SkyHit);
            Shots.Sort(Compare);Holds.Sort(Compare);
        }
        private static int Compare(HitSoundCue a,HitSoundCue b)
        {int t=a.Start.CompareTo(b.Start);if(t!=0)return t;int k=a.Kind.CompareTo(b.Kind);return k!=0?k:a.SourceId.CompareTo(b.SourceId);}
        private void Shot(SpcEvent e,HitSoundKind kind)
        {Shots.Add(new HitSoundCue{SourceId=e.SourceId,Kind=kind,Start=e.TimeMs,End=e.TimeMs});}
        private static List<HitSoundCue> Union(IEnumerable<HitSoundCue> cues)
        {
            var result=new List<HitSoundCue>();
            foreach(var cue in cues.OrderBy(c=>c.Start).ThenBy(c=>c.End))
            {
                var last=result.Count>0?result[result.Count-1]:null;
                if(last!=null&&cue.Start<=last.End+1e-7)last.End=Math.Max(last.End,cue.End);
                else result.Add(new HitSoundCue{SourceId=cue.SourceId,Kind=cue.Kind,Start=cue.Start,End=cue.End});
            }
            return result;
        }
        public static int LowerBound(IList<HitSoundCue> cues,double time)
        {int lo=0,hi=cues.Count;while(lo<hi){int m=(lo+hi)/2;if(cues[m].Start<time)lo=m+1;else hi=m;}return lo;}
    }
}
