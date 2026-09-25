using UnityEngine;

namespace InFalsusStudio
{
    // View-shaped decoration on a real, flat XZ air plane. This is a fitted STYLE model,
    // not a claim that the game stores a camera-dependent shape or a world-Y height in SPC.
    // v0.2: user-confirmed width-independent height. A unit reference span at the
    // same depth supplies perspective scale; the event width NEVER enters height.
    // Default .40 reference units preserves the previous width=12/24 reference appearance.
    public static class FlickGeometry
    {
        public static float ScreenHeight(StudioProfile p, Camera camera, float y, float z)
        {
            // Fixed reference span, NOT xr-xl. Same depth => same height for all widths.
            Vector3 a=camera.WorldToScreenPoint(new Vector3(-.5f,y,z));
            Vector3 b=camera.WorldToScreenPoint(new Vector3(.5f,y,z));
            return Mathf.Abs(b.x-a.x)*p.flickHeightUnits;
        }
        public static Vector3 BackVertex(StudioProfile p,Camera camera,float xl,float xr,float y,float z,bool right,float q)
        {
            float high=right?xr:xl,tip=right?xl:xr;
            Vector3 highScreen=camera.WorldToScreenPoint(new Vector3(high,y,z));
            Vector3 tipScreen=camera.WorldToScreenPoint(new Vector3(tip,y,z));
            float taper=1-Mathf.Sin(q*Mathf.PI*.5f);
            float height=ScreenHeight(p,camera,y,z);
            float h=height*taper;
            // Narrow notes keep their height. Limit ONLY the lateral cap skew so
            // the upper curve cannot double back when its width is very small.
            float capShift=Mathf.Min(height*p.flickCapSkew,Mathf.Abs(tipScreen.x-highScreen.x)*.5f);
            Vector3 upper=Vector3.Lerp(highScreen,tipScreen,q);
            upper.x+=(right?-1:1)*capShift*taper;upper.y+=h;
            Ray ray=camera.ScreenPointToRay(upper);
            if(Mathf.Abs(ray.direction.y)>1e-7f)
            {
                float t=(y-ray.origin.y)/ray.direction.y;
                if(t>=0)
                {
                    Vector3 point=ray.GetPoint(t);point.y=y;
                    // Recedes in Z for the calibrated downward-facing camera.
                    if(point.z>=z-.0001f)return point;
                }
            }
            // Degenerate camera poses do not turn the note into an infinite mesh.
            return new Vector3(Mathf.Lerp(high,tip,q),y,z+p.flickDepth*(1-Mathf.Sin(q*Mathf.PI*.5f)));
        }
    }
}
