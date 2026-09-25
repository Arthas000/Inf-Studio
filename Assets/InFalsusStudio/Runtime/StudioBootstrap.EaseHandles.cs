using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private Vector2 easeSelectorCenter;
        private int easeInitialCode;
        private SkyEaseGeometry easeGeometry;
        private static bool IsEaseHandle(NoteHandle handle)
        {return handle==NoteHandle.LeftEase||handle==NoteHandle.RightEase;}
        private void BeginEaseSelector()
        {
            bool right=dragHandle==NoteHandle.RightEase;
            // Re-evaluate the current endpoints every time; no sticky wall/straight flags.
            easeGeometry=GeometricSkyEase.Describe(dragEvent,right);
            easeInitialCode=(int)dragEvent.Get(right?8:7);
            float offset=(float)(easeGeometry.Position(easeInitialCode)*SkyEaseHandle.NotchPixels);
            easeSelectorCenter=pointerDown-new Vector2(offset,0);guideActive=false;
        }
        private void PreviewEaseSelector()
        {
            if(dragEvent==null||dragEvent.Kind!=EventKind.SkyArea)return;
            bool right=dragHandle==NoteHandle.RightEase;
            if(!easeGeometry.CanCurve){status=easeGeometry.StraightReason;return;}
            int code=easeGeometry.FromDrag(easeInitialCode,pointerNow.x-pointerDown.x);
            try
            {
                History.Preview(d=>EditOperations.SetEase(d,new[]{dragPrimary},right,code));
                AfterDocumentEdit();guideActive=false;
                status=(right?"Right":"Left")+" curve follows the mouse: "+SkyEaseHandle.Name(code)+
                    ". Endpoint direction determines SineOut/SineIn; opposite edge and all endpoints unchanged.";
            }
            catch(Exception ex){status=ex.Message+" Last valid easing preview retained.";}
        }
        private void DrawEaseSelector()
        {
            if(gesture!=Gesture.Resize||!IsEaseHandle(dragHandle)||dragEvent==null||!EditHeld||!easeGeometry.CanCurve)return;
            bool right=dragHandle==NoteHandle.RightEase;
            var current=events.FirstOrDefault(n=>n.SourceId==dragPrimary);
            int code=current!=null?(int)current.Get(right?8:7):easeInitialCode;
            float span=(float)SkyEaseHandle.NotchPixels;Color track=new Color(.55f,.92f,1,.95f);
            ClippedGuiLine(easeSelectorCenter-new Vector2(span+8,0),easeSelectorCenter+new Vector2(span+8,0),track,2);
            for(int value=0;value<3;value++)
            {
                Vector2 tick=easeSelectorCenter+new Vector2((float)easeGeometry.Position(value)*span,0);
                Color color=code==value?new Color(1,.85f,.15f,1):track;
                ClippedGuiLine(tick-new Vector2(0,7),tick+new Vector2(0,7),color,code==value?3:1);
                GUI.Label(new Rect(tick.x-38,tick.y+(value==0?-31:13),88,22),SkyEaseHandle.Name(value));
            }
            GUI.Box(new Rect(Mathf.Clamp(easeSelectorCenter.x-143,8,Screen.width-304),
                Mathf.Clamp(easeSelectorCenter.y+41,WorkspaceTop+4,Screen.height-96),290,25),
                (right?"Right":"Left")+" curve  |  "+SkyEaseHandle.Name(code)+"  |  release to commit");
        }
    }
}
