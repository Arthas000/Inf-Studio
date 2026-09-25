using UnityEngine;

namespace InFalsusStudio
{
    [CreateAssetMenu(menuName = "InFalsus Studio/Calibrated profile", fileName = "StudioProfile")]
    public sealed class StudioProfile : ScriptableObject
    {
        [Header("Image-calibrated model, NOT recovered original engine values")]
        public float verticalFov = 50f;
        public float cameraPitch = 23.65212f;
        public float cameraHeight = 3.086448f;
        public float cameraBack = 3.219452f;
        public bool lock16By9 = true;
        [Header("World X = lateral, Y = height, +Z = future; ground judge Z = 0")]
        public float centralHalfWidth = 2f;
        public float sideRun = .8252873f;
        public float sideRise = .7716634f;
        public float skyHeight = 1.156494f;
        public float skyJudgeHalfWidth = 2.52f;
        public float stageNear = -1.5f;
        public float stageFar = 65f;
        [Header("Timing: independent scroll multiplier; NOT playback rate")]
        public float unitsPerSecondAtSpeedOne = 15f;
        public float scrollMultiplier = 1f;
        [Header("Style parameters. These are NOT SPC fields.")]
        public float tapDepth = .48f;
        public float tapWidthRatio = .94f;
        public float flickDepth = 1.20f; // Fallback only for degenerate camera poses.
        [HideInInspector] public float flickScreenHeightRatio = .20f; // Legacy serialized setting; no longer used.
        public float flickHeightUnits = .40f; // Perspective-scaled reference height, independent of note width.
        public float flickCapSkew = 1.0f; // Mirrored diagonal leading cap in screen space.
        public float skyBorderWidth = .012f;
        public float curvePixelTolerance = .65f;
        public float fogStart = 32f;
        public float fogEnd = 76f;
        public Color background = new Color(.016f, .027f, .042f, 1);
        public Color floorColor = new Color(.055f, .092f, .125f, 1);
        public Color leftSide = new Color(.16f, .13f, .255f, 1);
        public Color rightSide = new Color(.25f, .10f, .20f, 1);
        public Color tapColor = new Color(.28f, .86f, 1f, 1);
        public Color leftFlick = new Color(1f, .87f, .20f, .94f);
        public Color rightFlick = new Color(.30f, 1f, .64f, .94f);
        public Color skyFill = new Color(.55f, .28f, 1f, .22f);
        public Color skyEdge = new Color(.83f, .57f, 1f, .98f);
        public float UnitsPerMs { get { return unitsPerSecondAtSpeedOne * scrollMultiplier * .001f; } }
        public void Validate()
        {
            verticalFov = Mathf.Clamp(verticalFov, 15, 100); cameraHeight = Mathf.Max(.1f, cameraHeight); cameraBack = Mathf.Max(.1f, cameraBack);
            centralHalfWidth = Mathf.Max(.1f, centralHalfWidth); sideRun = Mathf.Max(.01f, sideRun); sideRise = Mathf.Max(0, sideRise);
            skyHeight = Mathf.Clamp(skyHeight, .01f, cameraHeight - .05f); stageFar = Mathf.Max(10, stageFar);
            scrollMultiplier = Mathf.Clamp(scrollMultiplier, .05f, 10); unitsPerSecondAtSpeedOne = Mathf.Max(.1f, unitsPerSecondAtSpeedOne);
            curvePixelTolerance = Mathf.Clamp(curvePixelTolerance, .1f, 5); flickDepth = Mathf.Max(.01f, flickDepth);
            if(flickHeightUnits<=0)flickHeightUnits=.40f;
            flickHeightUnits = Mathf.Clamp(flickHeightUnits,.03f,.9f);flickCapSkew=Mathf.Clamp(flickCapSkew,0,2);
        }
    }
}
