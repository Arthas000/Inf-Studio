using System;

namespace InFalsusStudio.Core
{
    public enum PlacementPhase { StartTime, StartX, EndTime, EndX, Ready }

    // Explicit click-click state, not a drag or an incomplete document transaction.
    public sealed class PointPlacement
    {
        public EventKind Kind {get;private set;}
        public PlacementPhase Phase {get;private set;}
        public double StartMs {get;private set;}
        public double EndMs {get;private set;}
        public double StartX {get;private set;}
        public double EndX {get;private set;}
        public int Lane {get;private set;}
        public int Width {get;private set;}
        public bool Pending {get{return Phase!=PlacementPhase.StartTime;}}
        public bool ChoosingX {get{return Phase==PlacementPhase.StartX||Phase==PlacementPhase.EndX;}}
        public PointPlacement(EventKind kind=EventKind.Tap){Reset(kind);}
        public void Reset(EventKind kind)
        {Kind=kind;Phase=PlacementPhase.StartTime;StartMs=EndMs=0;StartX=EndX=.5;Lane=1;Width=1;}
        public bool ChooseTime(double time,int lane,int width)
        {
            if(time<0||double.IsNaN(time)||double.IsInfinity(time))return false;
            if(Phase==PlacementPhase.StartTime)
            {
                if(Kind==EventKind.Tap||Kind==EventKind.Hold)
                {
                    int resolvedLane,resolvedWidth;
                    if(!GroundPlacement.TryResolve(lane,width,out resolvedLane,out resolvedWidth))return false;
                    lane=resolvedLane;width=resolvedWidth;
                }
                StartMs=EndMs=time;Lane=lane;Width=width;
                Phase=Kind==EventKind.Tap?PlacementPhase.Ready:Kind==EventKind.Hold?PlacementPhase.EndTime:PlacementPhase.StartX;
                return true;
            }
            if(Phase==PlacementPhase.EndTime&&time>StartMs)
            {EndMs=time;Phase=Kind==EventKind.Hold?PlacementPhase.Ready:PlacementPhase.EndX;return true;}
            return false;
        }
        public bool ChooseX(double normalized)
        {
            if(double.IsNaN(normalized)||double.IsInfinity(normalized)||normalized<0||normalized>1)return false;
            if(Phase==PlacementPhase.StartX)
            {StartX=EndX=normalized;Phase=Kind==EventKind.Flick?PlacementPhase.Ready:PlacementPhase.EndTime;return true;}
            if(Phase==PlacementPhase.EndX){EndX=normalized;Phase=PlacementPhase.Ready;return true;}
            return false;
        }
    }
}
