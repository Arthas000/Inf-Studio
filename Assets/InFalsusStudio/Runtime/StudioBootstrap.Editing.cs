using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    // Runtime IMGUI editing. No UnityEditor.HandleUtility, physics colliders or Input System.
    // The calibrated gameplay camera stays untouched. All edits target original SPC tokens.
    public sealed partial class StudioBootstrap
    {
        private enum Tool { Select, Tap, Hold, Sky, FlickLeft, FlickRight }
        private enum Gesture { None, Move, Resize, Place, Box, TimelineMove }
        private struct HandlePoint { public NoteHandle Kind; public Vector2 Gui; public string Label; public bool Locked; }
        private readonly HashSet<int> selection = new HashSet<int>();
        private readonly NoteClipboard clipboard = new NoteClipboard();
        private readonly List<HandlePoint> handles = new List<HandlePoint>();
        private Tool tool;
        private Gesture gesture;
        private NoteHandle dragHandle;
        private int[] dragIds = new int[0];
        private int dragPrimary, dragLane, controlId, capturedControl;
        private Vector2 pointerDown, pointerNow;
        private Vector3 pointDown;
        private SpcEvent dragEvent;
        private double dragReferenceTime, lastResolvedTime;
        private bool moved, boxAdd, boxToggle, linearTimeLayout;
        private HashSet<int> boxInitial;
        private int snapIndex=3;
        private int customDivision=4;
        private static readonly int[] SnapDivisions={1,2,3,4,6,8,12,16,24,32};
        private int airSplit=100;
        private float defaultDurationBeats=2;
        private bool newPasteGroups=true, showHelp;
        private readonly ScrollTimeline linearScroll=new ScrollTimeline(new SpcEvent[0]);
        private List<int> lastPick=new List<int>();
        private int cycleIndex;
        private string timingBpm="130",timingSpeed="1";
        private byte[] savedBytes;
        private bool releaseCapture;
        private ScrollTimeline EffectiveScroll {get{return linearTimeLayout?linearScroll:scroll;}}
        private int Division {get{return customDivision;}}
        private bool IsGround(SpcEvent e){return e.Kind==EventKind.Tap||e.Kind==EventKind.Hold;}
        private float Depth(double t){return (float)(EffectiveScroll.Delta(t,CurrentMs)*Profile.UnitsPerMs);}
        private double SnapTime(double t,bool bypass=false){return showGrid&&authoringGrid!=null?authoringGrid.Nearest(t,Division).TimeMs:AuthoringGrid.RoundMs(t);}
        private double SnapX(double x,double divisor,bool boundary,bool bypass)
        {return airGrid.Snap(x);}
        private void ResetEditingForDocument()
        {
            CancelWorkflow(false);gesture=Gesture.None;selection.Clear();lastPick.Clear();capturedControl=0;releaseCapture=true;
            savedBytes=History.Document.ToBytes();ResetPointDraft();radial.Cancel();ResetGroupBrowser();
        }
        private void RefreshSelectionFields()
        {
            selection.RemoveWhere(id=>!History.Document.Contains(id));
            if(!selection.Contains(selectedId))selectedId=selection.Count>0?selection.OrderBy(x=>History.Document.SourceIndex(x)).First():-1;
            fieldValues=selectedId>=0?History.Document.ArgumentTokens(selectedId).Select(x=>x.Trim()).ToArray():new string[0];
            sourceEditText=selectedId>=0?History.Document.SourceLine(selectedId):"";
        }
        private void SetSelection(IEnumerable<int> ids,int primary=-1)
        {
            if(pendingNoteKey!=null&&!History.InTransaction&&!CommitPendingNoteNow())return;
            selection.Clear();foreach(int id in ids)if(History.Document.Contains(id))selection.Add(id);
            selectedId=primary;RefreshSelectionFields();FollowSelectedSkyGroup();
        }
        private void AfterDocumentEdit()
        {
            RefreshDocument();RefreshSelectionFields();UpdateFollowedGroupAfterEdit();
            documentDirty=savedBytes==null||!History.Document.ToBytes().SequenceEqual(savedBytes);
            SyncActiveSongDraft();RefreshProjectDirty();
        }
        private void CancelGesture()
        {
            CancelWorkflow();
            if(History!=null&&History.InTransaction){History.Cancel();AfterDocumentEdit();}
            gesture=Gesture.None;dragEvent=null;ResetPointDraft();releaseCapture=true;capturedControl=0;
        }
        private void ReleaseGesture()
        {
            if(History.InTransaction){bool changed=History.Commit();AfterDocumentEdit();if(changed)status="Drag committed as ONE undo step. Ctrl+Z undoes it.";}
            gesture=Gesture.None;dragEvent=null;guideActive=false;
            if(GUIUtility.hotControl==capturedControl)GUIUtility.hotControl=0;capturedControl=0;
        }
        private void Capture(Event e,Gesture kind)
        {
            gesture=kind;pointerDown=pointerNow=e.mousePosition;moved=false;
            capturedControl=controlId;GUIUtility.hotControl=controlId;GUIUtility.keyboardControl=0;
        }
        private void OnApplicationFocus(bool focus){if(!focus&&initialized){editModifier.Reset();radial.Cancel();progressDragging=false;releaseCapture=true;CancelGesture();}}
        private void TickEditing()
        {
            // Manual-save policy: no timer writes source, working charts or recovery copies.
            if(songProject!=null&&transport!=null&&songProject.AudioOffsetMs!=transport.AudioOffsetMs)
            {songProject.AudioOffsetMs=transport.AudioOffsetMs;RefreshProjectDirty();}
        }
        private void DeleteSelected()
        {
            int[] ids=selection.ToArray();if(ids.Length==0)return;
            if(Change(d=>{foreach(int id in ids){var ev=events.FirstOrDefault(x=>x.SourceId==id);if(ev!=null&&ev.Kind!=EventKind.Chart)d.Delete(id);}}))
                status="Deleted selection. Ctrl+Z restores the exact source lines.";
        }
        private void CopySelection(bool cut)
        {
            var ids=events.Where(n=>n.IsNote&&selection.Contains(n.SourceId)).Select(n=>n.SourceId).ToArray();
            if(ids.Length==0||transport.Playing||!CommitPendingNoteNow())return;
            clipboard.Copy(History.Document,ids);copySnapshot=History.Document.Clone();copyIds=ids;
            BeginFloating(cut?History.Document:copySnapshot,ids,cut);
        }
        private void PasteSelection(bool duplicate)
        {
            if(duplicate){CopySelection(false);return;}
            if(copySnapshot==null||copyIds.Length==0){status=L("请先选中并复制音符。","Copy notes first.");return;}
            BeginFloating(copySnapshot,copyIds,false);
        }
        private void MirrorSelection()
        {if(selection.Count>0&&Change(d=>EditOperations.Mirror(d,selection.ToArray())))status="Mirrored geometry, Flick direction, and Sky left/right easing together.";}
        private void Nudge(KeyCode key,bool large,bool fine)
        {
            if(selection.Count==0)return;
            int sign=(key==KeyCode.LeftArrow||key==KeyCode.DownArrow)?-1:1;
            if(key==KeyCode.LeftArrow||key==KeyCode.RightArrow)
            {
                double airStep=events.Any(n=>selection.Contains(n.SourceId)&&IsGround(n))?.25:(double)airGrid.StepPercent/100;
                Change(d=>EditOperations.Move(d,selection,0,sign,sign*airStep));
            }
            else
            {
                var anchor=events.FirstOrDefault(x=>x.SourceId==selectedId);if(anchor==null)return;
                double target=showGrid?(large?SnapTime(beats.TimeAt(beats.BeatAt(anchor.TimeMs)+sign)):authoringGrid.Adjacent(anchor.TimeMs,sign,Division)):
                    AuthoringGrid.RoundMs(anchor.TimeMs+(fine?sign:sign*60000.0/beats.Points[authoringGrid.Nearest(anchor.TimeMs,Division).Segment].Bpm/Division));
                double dt=target-anchor.TimeMs;
                Change(d=>EditOperations.PointerMove(d,selection,dt,0,0,showGrid?authoringGrid:null,Division));
            }
        }
        private void HandleKeyboard(Event e,bool textFocused)
        {
            if(e.type!=EventType.KeyDown)return;
            if(e.keyCode==KeyCode.Escape)
            {
                if(pendingNoteKey!=null){pendingNoteKey=null;noteForm=null;GUI.FocusControl(null);e.Use();return;}
                if(gesture!=Gesture.None||pointPlacement.Pending||WorkflowActive){CancelGesture();status="Draft/gesture cancelled; no changes kept. Placement tool remains active.";}
                else {GUI.FocusControl(null);SetSelection(new int[0]);showInspector=false;UseTool(Tool.Select);}
                e.Use();return;
            }
            bool mod=e.control||e.command;
            if(mod&&e.keyCode==KeyCode.S){bool saveAs=e.shift;QueueGui(()=>SaveWorkingCopy(saveAs));e.Use();return;}
            if(textFocused)return;
            if(e.keyCode==KeyCode.F1){CancelGesture();showUi=!showUi;e.Use();return;}
            if(e.keyCode==KeyCode.Space){TogglePlayback07();e.Use();return;}
            if(gesture!=Gesture.None)return;
            if(transport.Playing)return;
            if(WorkflowActive)return;
            if(mod&&e.keyCode==KeyCode.O){ChooseChart();e.Use();return;}
            if(mod&&e.keyCode==KeyCode.Z){Undo(e.shift);e.Use();return;}
            if(mod&&e.keyCode==KeyCode.Y){Undo(true);e.Use();return;}
            if(!showUi)return;
            if(mod&&e.keyCode==KeyCode.A){SetSelection(events.Where(x=>x.IsNote&&(pickFilter==0||pickFilter==1&&IsGround(x)||pickFilter==2&&!IsGround(x))).Select(x=>x.SourceId));status="Selected "+selection.Count+" notes in current Ground/Air filter.";e.Use();}
            else if(mod&&e.keyCode==KeyCode.C){CopySelection(false);e.Use();}
            else if(mod&&e.keyCode==KeyCode.X){CopySelection(true);e.Use();}
            else if(mod&&e.keyCode==KeyCode.V){PasteSelection(false);e.Use();}
            else if(mod&&e.keyCode==KeyCode.D){PasteSelection(true);e.Use();}
            else if(e.keyCode==KeyCode.Delete||e.keyCode==KeyCode.Backspace){DeleteSelected();e.Use();}
            else if(e.keyCode==KeyCode.M){MirrorSelection();e.Use();}
            else if(e.keyCode==KeyCode.Tab&&lastPick.Count>0)
            {cycleIndex=(cycleIndex+1)%lastPick.Count;Select(lastPick[cycleIndex]);status="Overlap candidate "+(cycleIndex+1)+" / "+lastPick.Count;e.Use();}
            else if(e.keyCode==KeyCode.Q){UseTool(Tool.Select);e.Use();}
            else if((int)e.keyCode>=(int)KeyCode.Alpha1&&(int)e.keyCode<=(int)KeyCode.Alpha5){UseTool((Tool)((int)e.keyCode-(int)KeyCode.Alpha1+1));e.Use();}
            else if(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.DownArrow)
            {
                if(selection.Count>0)Nudge(e.keyCode,e.shift,e.alt);
                else BrowseSteps(e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.DownArrow?-1:1);
                e.Use();
            }
        }
        private bool AllowedCanvas(Vector2 gui)
        {
            if(!showUi||transport.Playing||HudBlocksInput(gui)||WorkspaceBlocksInput(gui))return false;
            if(topRect.Contains(gui)||bottomRect.Contains(gui)||guiShowInspector&&inspectorRect.Contains(gui)||guiShowCalibration&&calibrationRect.Contains(gui)||guiShowHelp&&HelpRect.Contains(gui))return false;
            return ViewCamera.pixelRect.Contains(new Vector2(gui.x,Screen.height-gui.y));
        }
        private Ray PointerRay(Vector2 gui){return ViewCamera.ScreenPointToRay(new Vector3(gui.x,Screen.height-gui.y,0));}
        private bool PlanePoint(Vector2 gui,bool air,int lane,out Vector3 point)
        {
            Ray ray=PointerRay(gui);Vector3 normal=Vector3.up,origin=new Vector3(0,air?Profile.skyHeight+.019f:.018f,0);
            if(!air&&(lane==0||lane==5))
            {
                float sign=lane==0?-1:1,slope=Profile.sideRise/Profile.sideRun;
                normal=new Vector3(-sign*slope,1,0).normalized;
                origin=new Vector3(sign*Profile.centralHalfWidth,0,0)+normal*.018f;
            }
            float dot=Vector3.Dot(normal,ray.direction);point=Vector3.zero;
            if(Mathf.Abs(dot)<1e-7f)return false;
            float t=Vector3.Dot(normal,origin-ray.origin)/dot;if(t<0)return false;point=ray.GetPoint(t);
            return !float.IsNaN(point.z)&&Mathf.Abs(point.z)<Profile.stageFar*4;
        }
        private bool FloorPoint(Vector2 gui,out Vector3 point,out int lane)
        {
            point=Vector3.zero;lane=-1;float best=float.PositiveInfinity;
            foreach(int test in new[]{1,0,5})
            {
                Vector3 q;if(!PlanePoint(gui,false,test,out q))continue;float h=Profile.centralHalfWidth;
                bool inside=test==1?q.x>=-h&&q.x<=h:test==0?q.x>=-h-Profile.sideRun-.02f&&q.x<=-h+.02f:q.x>=h-.02f&&q.x<=h+Profile.sideRun+.02f;
                if(!inside)continue;float distance=(q-ViewCamera.transform.position).sqrMagnitude;
                if(distance<best){best=distance;point=q;lane=test==1?Mathf.Clamp(Mathf.FloorToInt((q.x+h)/(h*.5f))+1,1,4):test;}
            }
            return lane>=0;
        }
        private double Norm(float worldX){return (worldX/Profile.centralHalfWidth+1)*.5;}
        private bool TimeFromDepth(float z,double preferred,out double time)
        {
            int count;bool interval;
            bool ok=EditTimeResolver.Resolve(EffectiveScroll,EffectiveScroll.PositionAt(CurrentMs)+z/Profile.UnitsPerMs,preferred,Math.Max(durationMs+60000,CurrentMs+60000),out time,out count,out interval);
            if(!ok){status="No time at that depth. Use Time layout for stop/reverse sections.";return false;}
            if(count>1||interval)status="Depth has "+count+" time candidates"+(interval?" (stop interval)":"")+"; nearest selected time retained. Time layout is unambiguous.";
            return true;
        }
        private bool ProjectGui(Vector3 world,out Vector2 gui)
        {
            Vector3 screen=ViewCamera.WorldToScreenPoint(world);gui=new Vector2(screen.x,Screen.height-screen.y);
            return screen.z>ViewCamera.nearClipPlane&&ViewCamera.pixelRect.Contains(new Vector2(screen.x,screen.y));
        }
        private Vector3 NoteAnchor(SpcEvent e,NoteHandle handle)
        {
            bool tail=handle==NoteHandle.Tail||handle==NoteHandle.TailLeft||handle==NoteHandle.TailRight;
            double time=tail?e.EndMs:e.TimeMs;
            if(IsEaseHandle(handle))time=(e.TimeMs+e.EndMs)*.5;
            if(handle==NoteHandle.Body&&e.DurationMs>0)time=(Math.Max(hidePast?CurrentMs:e.TimeMs,e.TimeMs)+e.EndMs)*.5;
            bool right=handle==NoteHandle.HeadRight||handle==NoteHandle.TailRight||handle==NoteHandle.RightEase,left=handle==NoteHandle.HeadLeft||handle==NoteHandle.TailLeft||handle==NoteHandle.LeftEase;
            if(IsGround(e))
            {
                Vector3 a=StageSpace.FloorBound(Profile,e.Lane,(float)e.GroundWidth,false,Depth(time)),b=StageSpace.FloorBound(Profile,e.Lane,(float)e.GroundWidth,true,Depth(time));
                return left?a:right?b:(a+b)*.5f;
            }
            SkyRange r=e.Kind==EventKind.Flick?ChartMath.FlickRange(e):ChartMath.SkyAt(e,time);
            return new Vector3(StageSpace.AirX(Profile,left?r.Left:right?r.Right:r.Center),Profile.skyHeight+(e.Kind==EventKind.Flick?.019f:.006f),Depth(time));
        }
        private void BuildHandles()
        {
            handles.Clear();if(!showUi||!showNotes||!EditHeld||transport.Playing||WorkflowActive)return;
            var e=events.FirstOrDefault(x=>x.SourceId==selectedId&&x.IsNote);if(e==null||ChartMath.InvalidReason(e)!=null)return;
            AddHandle(e,NoteHandle.Head,e.DurationMs>0?"H":"MOVE");
            if(e.DurationMs>0){AddHandle(e,NoteHandle.Tail,"T");AddHandle(e,NoteHandle.Body,"MOVE");}
            if(!IsGround(e)||e.Lane>0&&e.Lane<5)
            {
                AddHandle(e,NoteHandle.HeadLeft,"L");AddHandle(e,NoteHandle.HeadRight,"R");
                if(e.Kind==EventKind.SkyArea)
                {
                    AddHandle(e,NoteHandle.TailLeft,"TL");AddHandle(e,NoteHandle.TailRight,"TR");
                    if(e.DurationMs>0){AddHandle(e,NoteHandle.LeftEase,"L~");AddHandle(e,NoteHandle.RightEase,"R~");}
                }
            }
        }
        private void AddHandle(SpcEvent e,NoteHandle kind,string label)
        {
            bool locked=false;
            if(IsEaseHandle(kind))
            {
                var shape=GeometricSkyEase.Describe(e,kind==NoteHandle.RightEase);locked=!shape.CanCurve;
                if(locked)label+=shape.Pinned?" [wall]":" [=X]";
            }
            Vector2 q;if(ProjectGui(NoteAnchor(e,kind),out q))handles.Add(new HandlePoint{Kind=kind,Gui=q,Label=label,Locked=locked});
        }
        private void HandleSceneEvent(Event e)
        {
            if(radial.Pressed||e.type==EventType.Used||e.type==EventType.Layout||e.type==EventType.Repaint)return;
            if(releaseCapture){if(GUIUtility.hotControl==controlId||GUIUtility.hotControl==radialControlId)GUIUtility.hotControl=0;releaseCapture=false;}
            if(e.type==EventType.MouseDown&&e.button==0&&AllowedCanvas(e.mousePosition))
            {
                if(GUIUtility.hotControl!=0)return;
                if(!CommitPendingNoteNow()){e.Use();return;}GUI.FocusControl(null);
                if(transport.Playing)
                {
                    lastPick=notes.PickAll(PointerRay(e.mousePosition),pickFilter);cycleIndex=0;
                    if(lastPick.Count>0)Select(lastPick[0]);
                    status="Playback continues. Canvas clicks only select; pause with Play/Space before editing.";
                    e.Use();return;
                }
                if(ClickWorkflow(e)){e.Use();return;}
                if(tool!=Tool.Select&&!EditHeld&&!e.control&&!e.command){ClickPointPlacement(e);e.Use();return;}
                BuildHandles();HandlePoint? hit=null;float best=100;
                foreach(var h in handles){float d=(h.Gui-e.mousePosition).sqrMagnitude;if(d<best){best=d;hit=h;}}
                if(EditHeld&&hit.HasValue&&selectedId>=0&&!e.shift&&!e.control&&!e.command)
                {
                    if(hit.Value.Locked)
                    {
                        var current=events.First(n=>n.SourceId==selectedId);
                        status=GeometricSkyEase.Describe(current,hit.Value.Kind==NoteHandle.RightEase).StraightReason;
                    }
                    else BeginNoteDrag(e,selectedId,hit.Value.Kind);
                    e.Use();return;
                }
                lastPick=notes.PickAll(PointerRay(e.mousePosition),pickFilter);cycleIndex=0;
                if(lastPick.Count>0)
                {
                    int id=lastPick[0];
                    if(e.control||e.command||e.shift)
                    {if(!selection.Remove(id))selection.Add(id);selectedId=id;RefreshSelectionFields();FollowSelectedSkyGroup();}
                    else
                    {
                        if(!selection.Contains(id))Select(id);else{selectedId=id;RefreshSelectionFields();FollowSelectedSkyGroup();}
                        if(EditHeld)BeginNoteDrag(e,id,NoteHandle.Body);
                    }
                    status="Selected "+selection.Count+". Alt + drag edits; Ctrl toggles; Tab cycles "+lastPick.Count+" candidates.";
                }
                else if(!EditHeld)
                {boxInitial=new HashSet<int>(selection);boxAdd=e.shift;boxToggle=e.control||e.command;Capture(e,Gesture.Box);}
                e.Use();
            }
            else if(e.type==EventType.MouseDrag&&e.button==0&&gesture!=Gesture.None&&GUIUtility.hotControl==capturedControl)
            {
                pointerNow=e.mousePosition;if((pointerNow-pointerDown).sqrMagnitude>9)moved=true;
                if(moved)
                {
                    if((gesture==Gesture.Move||gesture==Gesture.Resize)&&EditHeld)PreviewNoteDrag(e);
                    else if(gesture==Gesture.TimelineMove&&EditHeld)PreviewTimelineDrag(e);
                }
                e.Use();
            }
            else if(e.rawType==EventType.MouseUp&&e.button==0&&gesture!=Gesture.None)
            {
                pointerNow=e.mousePosition;
                if(gesture==Gesture.Box)
                {
                    var picked=moved?notes.BoxPick(RectFrom(pointerDown,pointerNow),pickFilter):new HashSet<int>();
                    if(boxToggle){var toggled=new HashSet<int>(boxInitial);foreach(int id in picked)if(!toggled.Remove(id))toggled.Add(id);picked=toggled;}else if(boxAdd)picked.UnionWith(boxInitial);
                    SetSelection(picked);status="Selected "+selection.Count+" notes.";
                }
                ReleaseGesture();e.Use();
            }
            else if(e.type==EventType.ScrollWheel&&AllowedCanvas(e.mousePosition)&&gesture==Gesture.None)
            {
                BrowseWheel(e.delta.y);RefreshAuthoringHover(e.mousePosition);e.Use();
            }
        }
        private void BeginNoteDrag(Event e,int id,NoteHandle handle)
        {
            if(!EditHeld||transport.Playing||WorkflowActive||!CommitPendingNoteNow())return;ResetPointDraft();
            dragEvent=events.FirstOrDefault(x=>x.SourceId==id);if(dragEvent==null||!dragEvent.IsNote)return;
            if(ChartMath.InvalidReason(dragEvent)!=null){status="Fix the invalid note in Inspector first.";return;}
            if(IsEaseHandle(handle)&&!GeometricSkyEase.Describe(dragEvent,handle==NoteHandle.RightEase).CanCurve)
            {status=GeometricSkyEase.Describe(dragEvent,handle==NoteHandle.RightEase).StraightReason;return;}
            if(handle==NoteHandle.Head&&(dragEvent.Kind==EventKind.Tap||dragEvent.Kind==EventKind.Flick))handle=NoteHandle.Body;
            dragLane=dragEvent.Lane;Vector3 point;
            if(!PlanePoint(e.mousePosition,!IsGround(dragEvent),dragLane,out point))return;
            dragPrimary=id;dragHandle=handle;dragIds=selection.ToArray();pointDown=point;
            bool tail=handle==NoteHandle.Tail||handle==NoteHandle.TailLeft||handle==NoteHandle.TailRight;
            dragReferenceTime=IsEaseHandle(handle)?(dragEvent.TimeMs+dragEvent.EndMs)*.5:tail?dragEvent.EndMs:dragEvent.TimeMs;lastResolvedTime=dragReferenceTime;
            History.Begin();Capture(e,handle==NoteHandle.Body?Gesture.Move:Gesture.Resize);
            if(IsEaseHandle(handle))BeginEaseSelector();
        }
        private void PreviewNoteDrag(Event e)
        {
            if(IsEaseHandle(dragHandle)){PreviewEaseSelector();return;}
            Vector3 q;bool ground=IsGround(dragEvent);int laneAt=dragLane;
            if(ground&&gesture==Gesture.Move)
            {if(!FloorPoint(pointerNow,out q,out laneAt))return;}
            else if(!PlanePoint(pointerNow,!ground,dragLane,out q))return;
            double deltaNorm=(q.x-pointDown.x)/(2*Profile.centralHalfWidth);
            bool tail=dragHandle==NoteHandle.Tail||dragHandle==NoteHandle.TailLeft||dragHandle==NoteHandle.TailRight;
            bool edge=dragHandle==NoteHandle.HeadLeft||dragHandle==NoteHandle.HeadRight||dragHandle==NoteHandle.TailLeft||dragHandle==NoteHandle.TailRight;
            bool right=dragHandle==NoteHandle.HeadRight||dragHandle==NoteHandle.TailRight;
            try
            {
                if(edge)
                {
                    if(ground)
                    {
                        int original=dragEvent.Lane-1+(right?(int)dragEvent.GroundWidth:0);
                        int boundary=original+Mathf.RoundToInt((q.x-pointDown.x)/(Profile.centralHalfWidth*.5f));
                        History.Preview(d=>EditOperations.GroundEdge(d,dragPrimary,right,boundary));
                        guideTime=dragReferenceTime;guideAir=false;guideActive=true;
                    }
                    else
                    {
                        var range=dragEvent.Kind==EventKind.Flick?ChartMath.FlickRange(dragEvent):ChartMath.SkyAt(dragEvent,tail?dragEvent.EndMs:dragEvent.TimeMs);
                        double split=dragEvent.Get(dragEvent.Kind==EventKind.SkyArea&&tail?5:2);
                        double boundary=SnapX((right?range.Right:range.Left)+deltaNorm,split,true,e.alt);
                        History.Preview(d=>EditOperations.AirEdge(d,dragPrimary,tail,right,boundary));
                        guideTime=dragReferenceTime;guideX=AirReachBounds.Clamp(boundary);guideAir=true;guideActive=true;
                    }
                }
                else
                {
                    double t;float targetZ=Depth(dragReferenceTime)+q.z-pointDown.z;
                    if(!TimeFromDepth(targetZ,lastResolvedTime,out t))return;t=SnapTime(t,e.alt);lastResolvedTime=t;
                    double split=ground?airSplit:dragEvent.Get(dragEvent.Kind==EventKind.SkyArea&&tail?5:2);
                    double center=EditOperations.Center(dragEvent,tail);
                    double nextCenter=SnapX(center+deltaNorm,split,false,e.alt);
                    int laneDelta=ground?laneAt-dragLane:0;
                    if(ground&&gesture==Gesture.Move)
                    {
                        // Preserve pointer-to-note offset, including a press on the right of a wide note.
                        Vector3 a;int clickedLane;if(FloorPoint(pointerDown,out a,out clickedLane))laneDelta=laneAt-clickedLane;
                    }
                    if(gesture==Gesture.Move)
                    {
                        double shift=ground?laneDelta/4.0:nextCenter-center;
                        if(!ground&&events.Any(n=>dragIds.Contains(n.SourceId)&&IsGround(n)))
                        {laneDelta=(int)Math.Round(shift*4);shift=laneDelta/4.0;}
                        History.Preview(d=>EditOperations.PointerMove(d,dragIds,t-dragReferenceTime,laneDelta,shift,showGrid?authoringGrid:null,Division));
                    }
                    else History.Preview(d=>EditOperations.Endpoint(d,dragPrimary,tail,t,nextCenter,dragLane));
                    guideTime=t;guideX=nextCenter;guideAir=!ground;guideActive=true;
                }
                AfterDocumentEdit();
                status="Position/width preview updated. Curve handles are recomputed from current endpoints; no persistent wall lock.";
                if(guideAir&&!edge)
                {
                    var accepted=events.FirstOrDefault(n=>n.SourceId==dragPrimary);
                    if(accepted!=null)guideX=EditOperations.Center(accepted,tail);
                }
            }
            catch(Exception ex){status=ex.Message+" Last valid preview retained.";}
        }
        private EventKind PlacementKind {get{return tool==Tool.Tap?EventKind.Tap:tool==Tool.Hold?EventKind.Hold:tool==Tool.Sky?EventKind.SkyArea:EventKind.Flick;}}
        private double DefaultEnd(double start){return SnapTime(beats.TimeAt(beats.BeatAt(start)+defaultDurationBeats));}
        private static Rect RectFrom(Vector2 a,Vector2 b){return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));}
        private void LineGui(Vector2 a,Vector2 b,Color color,float thickness=1)
        {
            if(Event.current.type!=EventType.Repaint)return;Matrix4x4 matrix=GUI.matrix;Color old=GUI.color;
            GUI.color=color;Vector2 v=b-a;GUIUtility.RotateAroundPivot(Mathf.Atan2(v.y,v.x)*Mathf.Rad2Deg,a);
            GUI.DrawTexture(new Rect(a.x,a.y-thickness*.5f,v.magnitude,thickness),Texture2D.whiteTexture);GUI.matrix=matrix;GUI.color=old;
        }
        private void BoxGui(Rect rect,Color color)
        {Vector2 a=new Vector2(rect.xMin,rect.yMin),b=new Vector2(rect.xMax,rect.yMin),c=new Vector2(rect.xMax,rect.yMax),d=new Vector2(rect.xMin,rect.yMax);LineGui(a,b,color);LineGui(b,c,color);LineGui(c,d,color);LineGui(d,a,color);}
        private void WorldLineGui(Vector3 a,Vector3 b,Color c)
        {Vector2 aa,bb;if(ProjectGui(a,out aa)&&ProjectGui(b,out bb))LineGui(aa,bb,c,2);}
        private void DrawEditingOverlay()
        {
            if(!showUi)return;BuildHandles();
            if(gesture==Gesture.Box&&moved)BoxGui(RectFrom(pointerDown,pointerNow),new Color(.45f,.95f,1,.9f));
            foreach(var h in handles)
            {
                if(!AllowedCanvas(h.Gui))continue;
                Color old=GUI.color;GUI.color=h.Locked?new Color(.6f,.6f,.65f,.8f):new Color(.3f,.95f,1,.95f);GUI.DrawTexture(new Rect(h.Gui.x-4,h.Gui.y-4,8,8),Texture2D.whiteTexture);GUI.color=old;
                GUI.Label(new Rect(h.Gui.x+7,h.Gui.y-10,100,20),h.Label);
            }
            DrawAuthoringOverlay();DrawEaseSelector();
        }
        private Rect HelpRect {get{return new Rect(Mathf.Max(10,(Screen.width-620)*.5f),180,620,370);}}
        private void DrawEditingHelp(){DrawHelp07();}
        private void AddTimingFromFields(bool header)
        {
            double bpm,meter;
            if(!double.TryParse(timingBpm,NumberStyles.Float,CI,out bpm)||!double.TryParse(timingMeter,NumberStyles.Float,CI,out meter)||bpm<=0||meter<=0||double.IsNaN(bpm)||double.IsNaN(meter)||double.IsInfinity(bpm)||double.IsInfinity(meter))
            {status="Positive finite BPM and beats-per-bar are required.";return;}
            CancelGesture();
            if(header)
            {
                var h=events.FirstOrDefault(n=>n.Kind==EventKind.Chart);if(h==null)return;
                Change(d=>{AuthoredNumber.Set(d,h.SourceId,0,bpm);AuthoredNumber.Set(d,h.SourceId,1,meter);});
            }
            else Change(d=>d.Append("bpm("+EditOperations.Number(SnapTime(CurrentMs))+","+EditOperations.Number(bpm)+","+EditOperations.Number(meter)+")"));
        }
    }
}
