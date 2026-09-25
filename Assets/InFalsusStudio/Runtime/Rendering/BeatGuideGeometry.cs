using UnityEngine;

namespace InFalsusStudio
{
    /// <summary>One centerline geometry for base grid and hover. The .012 lift matches
    /// the existing StageRenderer judgment line, so grid time == playhead meets it.
    /// LanePoint also keeps both side/central seams exactly shared.</summary>
    public static class BeatGuideGeometry
    {
        public static Vector3 Point(StudioProfile profile,int lane,float u,float z)
        {return StageSpace.LanePoint(profile,lane,u,z,.012f);}
    }
}
