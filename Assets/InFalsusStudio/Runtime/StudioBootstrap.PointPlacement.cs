using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private AuthoringGrid authoringGrid;
        private readonly AirAuthoringGrid airGrid=new AirAuthoringGrid(5m);
        private readonly PointPlacement pointPlacement=new PointPlacement();
        private bool hoverValid,guideActive,guideAir;
        private double hoverTime,hoverX=.5,guideTime,guideX=.5;
        private int hoverLane=1;
        private Vector2 hoverPointer;
        private string xStepText="20",skyWidthText="10",timingMeter="4",sourceEditText="";
        private int skyLeftEase,skyRightEase;
        private double skyWidthPercent=10;
        private const double NewFlickWidthPercent=25; // Only the direction has a radial submenu.
        private double PlacementWidthNorm {get{return (tool==Tool.Sky?skyWidthPercent:NewFlickWidthPercent)/100.0;}}
        private int GroundBrush(EventKind kind,int lane)
        {return lane==0||lane==5?1:kind==EventKind.Hold?holdWidth:tapWidth;}
        private string PlacementInstruction
        {
            get
            {
                if(tool==Tool.Select)return "No creation. Click/Shift-click select; Alt + drag edits. Press RMB for menus.";
                if(tool==Tool.Tap)return "Tap: click a highlighted time line + lane. Side lanes always width 1. Tool stays active.";
                if(tool==Tool.Hold)return pointPlacement.Pending?"Hold 2/2: choose END time (wheel browses); click to finish. Start/lane stay fixed.":"Hold 1/2: choose START time + lane; click. No left-button dragging needed.";
                if(tool==Tool.Sky)
                {
                    switch(pointPlacement.Phase)
                    {
                        case PlacementPhase.StartTime:return "Sky 1/4: choose START time on a beat line; click.";
                        case PlacementPhase.StartX:return "Sky 2/4: choose START X on the elevated 0..100 ruler; click.";
                        case PlacementPhase.EndTime:return "Sky 3/4: choose END time (wheel browses); click. Start remains locked.";
                        default:return "Sky 4/4: choose END X; click to finish. One creation = one Undo.";
                    }
                }
                return pointPlacement.Phase==PlacementPhase.StartTime?"Flick 1/2: choose TIME on a beat line; click.":"Flick 2/2: choose X on the elevated ruler; click to finish. Width is edited via handles/parameters.";
            }
        }
        private void ResetPointDraft()
        {pointPlacement.Reset(PlacementKind);hoverValid=false;guideActive=false;}
        private void SetBeatGrid(bool enabled)
        {
            if(showGrid==enabled)return;
            if(PointerEditGesture)CancelGesture();
            showGrid=enabled;ResetPointDraft();browseWheel.Reset();EnsureBrowseGridLock();
        }
        private void SetDivisionIndex(int index){SetSubdivision(SnapDivisions[Math.Max(0,Math.Min(SnapDivisions.Length-1,index))]);}
        private void SetSubdivision(int value)
        {
            if(value<1||value>1024){status=L("细分须是 1～1024 的整数。","Subdivision must be an integer from 1 to 1024.");return;}
            if(customDivision==value)return;if(PointerEditGesture)CancelGesture();customDivision=value;
            int index=Array.IndexOf(SnapDivisions,value);if(index>=0)snapIndex=index;
            divisionInput=value.ToString(CI);ResetPointDraft();browseWheel.Reset();EnsureBrowseGridLock();
        }
        private void RefreshAuthoringHover(Vector2 mouse)
        {
            hoverPointer=mouse;
            if(RefreshWorkflowHover(mouse))return;
            if(PointerEditGesture)return; // Kept at the actual accepted edit target by PreviewNoteDrag.
            guideActive=false;hoverValid=false;
            if(transport.Playing||radial.Pressed||!showUi||tool==Tool.Select||EditHeld||!AllowedCanvas(mouse))return;
            bool air=tool==Tool.Sky||tool==Tool.FlickLeft||tool==Tool.FlickRight;
            if(pointPlacement.ChoosingX)
            {
                double fixedTime=pointPlacement.Phase==PlacementPhase.EndX?pointPlacement.EndMs:pointPlacement.StartMs;
                double x;if(!XOnRuler(mouse,fixedTime,out x))return;
                hoverTime=fixedTime;hoverX=AirReachBounds.CenterWithin(airGrid.Snap(x),PlacementWidthNorm);hoverLane=pointPlacement.Lane;
            }
            else
            {
                // Time selection always starts on the real six-lane surface. The raised
                // line is a synchronized guide, NOT a second, offset timeline.
                int lane;double time;if(!PointerAuthoringTime(mouse,pointPlacement.Pending?Math.Max(pointPlacement.StartMs,CurrentMs):CurrentMs,out time,out lane))return;
                hoverTime=SnapTime(time);hoverLane=lane;hoverX=.5;
                if(!air)
                {
                    int startLane=pointPlacement.Pending?pointPlacement.Lane:lane;
                    int width=pointPlacement.Pending?pointPlacement.Width:GroundBrush(PlacementKind,lane);
                    int resolvedLane,resolvedWidth;
                    if(!GroundPlacement.TryResolve(startLane,width,out resolvedLane,out resolvedWidth))return;
                    hoverLane=resolvedLane;
                }
            }
            hoverValid=true;guideActive=true;guideTime=hoverTime;guideAir=air;guideX=hoverX;
        }
        private bool XOnRuler(Vector2 mouse,double time,out double x)
        {
            Vector3 a=ViewCamera.WorldToScreenPoint(new Vector3(StageSpace.AirX(Profile,0),Profile.skyHeight+.019f,Depth(time)));
            Vector3 b=ViewCamera.WorldToScreenPoint(new Vector3(StageSpace.AirX(Profile,1),Profile.skyHeight+.019f,Depth(time)));
            x=.5;if(a.z<=ViewCamera.nearClipPlane||b.z<=ViewCamera.nearClipPlane||Mathf.Abs(b.x-a.x)<1)return false;
            x=Math.Max(0,Math.Min(1,(mouse.x-a.x)/(b.x-a.x)));return true;
        }
        private void ClickPointPlacement(Event e)
        {
            RefreshAuthoringHover(e.mousePosition);
            if(!hoverValid){status="No valid target here. Choose a visible lane/line; central wide notes are automatically shifted left to fit.";return;}
            bool accepted=pointPlacement.ChoosingX?pointPlacement.ChooseX(hoverX):
                pointPlacement.ChooseTime(hoverTime,hoverLane,GroundBrush(PlacementKind,hoverLane));
            if(!accepted){status="End time must be strictly after start. Move to a later line; Escape cancels the draft.";return;}
            if(pointPlacement.Phase==PlacementPhase.Ready)
            {
                int id=-1;
                bool saved=Change(d=>
                {
                    id=EditOperations.Add(d,PlacementKind,pointPlacement.StartMs,pointPlacement.EndMs,pointPlacement.Lane,pointPlacement.Width,
                        pointPlacement.StartX,pointPlacement.EndX,PlacementWidthNorm,tool==Tool.FlickRight?4:16,100);
                    if(tool==Tool.Sky)
                    {EditOperations.SetEase(d,new[]{id},false,skyLeftEase);EditOperations.SetEase(d,new[]{id},true,skyRightEase);}
                });
                if(saved){SetSelection(new[]{id},id);radial.InPointMode=true;showNotes=true;ResetPointDraft();status="Placed "+PlacementKind+". Same tool remains active. "+PlacementInstruction;}
                else ResetPointDraft();
            }
            else status=PlacementInstruction;
        }
        private Rect CameraGuiRect
        {get{Rect r=ViewCamera.pixelRect;return new Rect(r.x,Screen.height-r.yMax,r.width,r.height);}}
        // Clip guides in SCREEN space. Do not discard a whole bar when only one side is off-screen.
        private void ClippedGuiLine(Vector2 a,Vector2 b,Color color,float thickness)
        {
            Rect r=CameraGuiRect;Vector2 d=b-a;float lo=0,hi=1;
            if(!ClipBound(-d.x,a.x-r.xMin,ref lo,ref hi)||!ClipBound(d.x,r.xMax-a.x,ref lo,ref hi)||
               !ClipBound(-d.y,a.y-r.yMin,ref lo,ref hi)||!ClipBound(d.y,r.yMax-a.y,ref lo,ref hi))return;
            LineGui(a+d*lo,a+d*hi,color,thickness);
        }
        private static bool ClipBound(float p,float q,ref float lo,ref float hi)
        {
            if(Mathf.Abs(p)<1e-7f)return q>=0;
            float v=q/p;if(p<0){if(v>hi)return false;if(v>lo)lo=v;}else{if(v<lo)return false;if(v<hi)hi=v;}return true;
        }
        private bool ScreenPoint(Vector3 point,out Vector2 gui)
        {
            Vector3 p=ViewCamera.WorldToScreenPoint(point);gui=new Vector2(p.x,Screen.height-p.y);
            return p.z>ViewCamera.nearClipPlane&&!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.x)&&!float.IsInfinity(p.y);
        }
        private void GuideLine(Vector3 a,Vector3 b,Color c,float width=2)
        {Vector2 x,y;if(ScreenPoint(a,out x)&&ScreenPoint(b,out y))ClippedGuiLine(x,y,c,width);}
        private void AirGuideLine(Vector3 a,Vector3 b,Color c,float width=2)
        {
            // Clip the actual segment, not merely its endpoints. Decorative Flick
            // vertices can project outside even with a valid baseline footprint.
            Vector3 delta=b-a;float lo=0,hi=1;
            if(!ClipBound(-delta.x,a.x+Profile.centralHalfWidth,ref lo,ref hi)||
               !ClipBound(delta.x,Profile.centralHalfWidth-a.x,ref lo,ref hi))return;
            GuideLine(a+delta*lo,a+delta*hi,c,width);
        }
        private void DrawTimeGuide(double time,bool air)
        {
            float z=Depth(time);Color color=new Color(1,.85f,.22f,.97f);
            for(int lane=0;lane<6;lane++)GuideLine(BeatGuideGeometry.Point(Profile,lane,0,z),BeatGuideGeometry.Point(Profile,lane,1,z),color,3);
            if(air)
            {
                GuideLine(new Vector3(-Profile.centralHalfWidth,Profile.skyHeight+.019f,z),new Vector3(Profile.centralHalfWidth,Profile.skyHeight+.019f,z),new Color(1,.72f,.2f,1),3);
                // One connector makes the time correspondence explicit, not an editable Y grid.
                GuideLine(new Vector3(0,0,z),new Vector3(0,Profile.skyHeight,z),new Color(1,.8f,.2f,.4f),1);
            }
        }
        private void DrawAirRuler(double time,double activeX,bool active=true)
        {
            float z=Depth(time),y=Profile.skyHeight+.021f;
            Color color=new Color(1,.6f,.15f,.85f);
            GuideLine(new Vector3(-Profile.centralHalfWidth,y,z),new Vector3(Profile.centralHalfWidth,y,z),color,2);
            Vector2 a,b;if(!ScreenPoint(new Vector3(-Profile.centralHalfWidth,y,z),out a)||!ScreenPoint(new Vector3(Profile.centralHalfWidth,y,z),out b))return;
            float pixels=Mathf.Abs(b.x-a.x);int labelStride=Math.Max(1,(int)Math.Ceiling(30/Math.Max(.01,pixels*(double)airGrid.StepPercent/100)));
            int count=0;
            foreach(double norm in airGrid.Ticks())
            {
                Vector2 tick=Vector2.Lerp(a,b,(float)norm);
                if(CameraGuiRect.Contains(tick))
                {
                    ClippedGuiLine(tick+new Vector2(0,-7),tick+new Vector2(0,7),color,1);
                    if(count%labelStride==0||norm==1)GUI.Label(new Rect(tick.x-18,tick.y+9,48,20),(norm*100).ToString("0.###",CI));
                }
                count++;
            }
            if(active)
            {
                Vector2 p=Vector2.Lerp(a,b,(float)activeX);
                ClippedGuiLine(p+new Vector2(0,-16),p+new Vector2(0,16),new Color(.35f,1,.65f,1),3);
                if(CameraGuiRect.Contains(p))GUI.Box(new Rect(Mathf.Clamp(p.x-61,8,Screen.width-138),p.y-43,122,25),"X "+(activeX*100).ToString("0.###",CI)+" / 100");
            }
        }
        private void DrawAuthoringOverlay()
        {
            if(!showUi||radial.Pressed||transport.Playing)return;
            if(EditHeld||tool==Tool.Sky||tool==Tool.FlickLeft||tool==Tool.FlickRight)
            {
                // Reach rails are editing aids at the same air plane, not new side lanes.
                foreach(float sign in new[]{-1f,1f})
                    AirGuideLine(new Vector3(sign*Profile.centralHalfWidth,Profile.skyHeight+.021f,0),
                        new Vector3(sign*Profile.centralHalfWidth,Profile.skyHeight+.021f,Mathf.Min(35,Profile.stageFar)),new Color(1,.55f,.18f,.48f),1.5f);
            }
            if(guideActive)
            {
                DrawTimeGuide(guideTime,guideAir);
                string info=(showGrid?"GRID ":"FREE ")+guideTime.ToString("0.##########",CI)+" ms";
                if(!pointPlacement.ChoosingX&&gesture==Gesture.None)GUI.Box(new Rect(Mathf.Clamp(hoverPointer.x+15,8,Screen.width-210),Mathf.Clamp(hoverPointer.y+20,160,Screen.height-95),200,24),info);
                if(guideAir&&(pointPlacement.ChoosingX||PointerEditGesture))DrawAirRuler(guideTime,guideX);
            }
            if(EditHeld&&tool==Tool.Select&&!PointerEditGesture)
            {
                var note=events.FirstOrDefault(n=>n.SourceId==selectedId&&n.IsNote&&!IsGround(n));
                if(note!=null&&ChartMath.InvalidReason(note)==null)
                {DrawAirRuler(note.TimeMs,EditOperations.Center(note));if(note.Kind==EventKind.SkyArea)DrawAirRuler(note.EndMs,EditOperations.Center(note,true));}
            }
            if(tool!=Tool.Select&&!EditHeld)
            {
                GUI.Box(new Rect(Mathf.Max(12,Screen.width*.5f-360),bottomRect.y-34,Mathf.Min(720,Screen.width-24),28),PlacementInstruction);
                if(hoverValid||pointPlacement.Pending)DrawPointPreview();
            }
        }
        private void DrawPointPreview()
        {
            bool pending=pointPlacement.Pending;
            double start=pending?pointPlacement.StartMs:hoverTime,end=start;
            double cx=pending?pointPlacement.StartX:.5,ex=cx;
            int lane=pending?pointPlacement.Lane:hoverLane,width=pending?pointPlacement.Width:GroundBrush(PlacementKind,hoverLane);
            if(tool==Tool.Tap||tool==Tool.Hold)
            {
                int resolvedLane,resolvedWidth;
                if(!GroundPlacement.TryResolve(lane,width,out resolvedLane,out resolvedWidth))return;
                lane=resolvedLane;width=resolvedWidth;
            }
            if(pointPlacement.Phase==PlacementPhase.StartX)cx=ex=hoverX;
            if(pointPlacement.Phase==PlacementPhase.EndTime){end=hoverValid?Math.Max(start,hoverTime):pointPlacement.EndMs;}
            if(pointPlacement.Phase==PlacementPhase.EndX){end=pointPlacement.EndMs;ex=hoverX;}
            Color c=new Color(.45f,1,.72f,.94f);float z0=Depth(start),z1=Depth(end);
            if(tool==Tool.Tap||tool==Tool.Hold)
            {
                if(tool==Tool.Tap||end<=start)z1=z0+Profile.tapDepth;
                Vector3 a=StageSpace.FloorBound(Profile,lane,width,false,z0),b=StageSpace.FloorBound(Profile,lane,width,true,z0),cc=StageSpace.FloorBound(Profile,lane,width,true,z1),d=StageSpace.FloorBound(Profile,lane,width,false,z1);
                GuideLine(a,b,c);GuideLine(b,cc,c);GuideLine(cc,d,c);GuideLine(d,a,c);return;
            }
            if(!pending)return; // X has not yet been chosen; show time guide only.
            double half=PlacementWidthNorm*.5;cx=AirReachBounds.CenterWithin(cx,half*2);ex=AirReachBounds.CenterWithin(ex,half*2);float y=Profile.skyHeight+.019f;
            if(tool==Tool.Sky)
            {
                var temporary=new SpcEvent{Kind=EventKind.SkyArea,Args=new[]{start,cx,1.0,half*2,ex,1.0,half*2,(double)skyLeftEase,(double)skyRightEase,Math.Max(0,end-start)}};
                Vector3 prevL=Vector3.zero,prevR=Vector3.zero;
                for(int i=0;i<=48;i++)
                {
                    double t=start+(end-start)*i/48;var range=ChartMath.SkyAt(temporary,t);
                    Vector3 l=new Vector3(StageSpace.AirX(Profile,range.Left),y,Depth(t)),rr=new Vector3(StageSpace.AirX(Profile,range.Right),y,Depth(t));
                    if(i>0){AirGuideLine(prevL,l,c);AirGuideLine(prevR,rr,c);}if(i==0||i==48)AirGuideLine(l,rr,c);prevL=l;prevR=rr;
                }
            }
            else
            {
                float xl=StageSpace.AirX(Profile,cx-half),xr=StageSpace.AirX(Profile,cx+half);bool right=tool==Tool.FlickRight;
                Vector3 prev=new Vector3(right?xr:xl,y,z0),last=prev;
                for(int i=0;i<=32;i++){last=FlickGeometry.BackVertex(Profile,ViewCamera,xl,xr,y,z0,right,i/32f);AirGuideLine(prev,last,c);prev=last;}
                Vector3 tip=new Vector3(right?xl:xr,y,z0),high=new Vector3(right?xr:xl,y,z0);AirGuideLine(last,tip,c);AirGuideLine(tip,high,c);
            }
        }
    }
}
