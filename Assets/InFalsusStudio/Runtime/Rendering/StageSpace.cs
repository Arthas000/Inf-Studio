using UnityEngine;

namespace InFalsusStudio
{
    public static class StageSpace
    {
        public static float AirX(StudioProfile p, double normalized) { return (float)((normalized * 2 - 1) * p.centralHalfWidth); }
        public static Vector3 Surface(StudioProfile p, float x, float z, float lift = 0)
        {
            float excess = Mathf.Max(0, Mathf.Abs(x) - p.centralHalfWidth);
            float slope = p.sideRise / p.sideRun;
            Vector3 n = excess > .000001f ? new Vector3(-Mathf.Sign(x) * slope, 1, 0).normalized : Vector3.up;
            return new Vector3(x, excess * slope, z) + n * lift;
        }
        public static Vector3 LanePoint(StudioProfile p, int lane, float u, float z, float lift = 0)
        {
            float x;
            if (lane == 0) x = -p.centralHalfWidth - p.sideRun * (1 - u);
            else if (lane == 5) x = p.centralHalfWidth + p.sideRun * u;
            else x = -p.centralHalfWidth + (lane - 1 + u) * p.centralHalfWidth * .5f;
            return Surface(p, x, z, lift);
        }
        public static Vector3 FloorBound(StudioProfile p, int lane, float width, bool right, float z, float inset = 0)
        {
            // Multi-width central note: one continuous footprint, never shift by a half-lane.
            if (lane == 0 || lane == 5) return LanePoint(p, lane, right ? 1 - inset : inset, z, .018f);
            float unit = p.centralHalfWidth * .5f;
            float x = -p.centralHalfWidth + (lane - 1 + (right ? width - inset : inset)) * unit;
            return Surface(p, x, z, .018f);
        }
        public static void ApplyCamera(Camera camera, StudioProfile p)
        {
            camera.transform.SetPositionAndRotation(new Vector3(0, p.cameraHeight, -p.cameraBack), Quaternion.Euler(p.cameraPitch, 0, 0));
            camera.orthographic = false; camera.usePhysicalProperties = false;
            camera.ResetProjectionMatrix(); camera.fieldOfView = p.verticalFov;
            camera.nearClipPlane = .03f; camera.farClipPlane = Mathf.Max(120, p.stageFar + 30);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = p.background;
            camera.allowHDR = true; camera.allowMSAA = true;
            if (p.lock16By9)
            {
                float screenAspect = (float)Mathf.Max(1, Screen.width) / Mathf.Max(1, Screen.height), target = 16f / 9;
                if (screenAspect > target) { float w = target / screenAspect; camera.rect = new Rect((1 - w) * .5f, 0, w, 1); }
                else { float h = screenAspect / target; camera.rect = new Rect(0, (1 - h) * .5f, 1, h); }
                camera.aspect = target;
            }
            else { camera.rect = new Rect(0, 0, 1, 1); camera.ResetAspect(); }
        }
    }
}
