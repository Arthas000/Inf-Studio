using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private FloatingNotes floating;
        private EditHistory floatingHistory;
        private SpcDocument copySnapshot;
        private int[] copyIds=new int[0],workflowSelection=new int[0];
        private int workflowPrimary=-1;
        private HashSet<int> floatingHidden;
        private IList<SpcEvent> ghostEvents=new SpcEvent[0];
        private double floatingTarget,rangeStart;
        private bool floatingValid,rangeActive,rangeHasStart;
        private NoteRenderer ghostRenderer;
        private bool WorkflowActive {get{return floating!=null||rangeActive;}}
        private void CancelWorkflow(bool restore=true)
        {
            bool was=WorkflowActive;floating=null;floatingHistory=null;floatingHidden=null;ghostEvents=new SpcEvent[0];floatingValid=false;rangeActive=rangeHasStart=false;
            if(was&&restore)SetSelection(workflowSelection,workflowPrimary);
        }
        private void BeginRangeSelection()
        {
            if(transport.Playing||!CommitPendingNoteNow())return;CancelGesture();tool=Tool.Select;
            workflowSelection=selection.ToArray();workflowPrimary=selectedId;rangeActive=true;rangeHasStart=false;
            status=L("段选：点击开始时间，再点击结束时间；长条按结束时间选取。右键取消。","Range: start then end; long notes use their END. RMB cancels.");
        }
        private void BeginFloating(SpcDocument source,IEnumerable<int> ids,bool cut)
        {
            if(transport.Playing||!CommitPendingNoteNow())return;CancelGesture();tool=Tool.Select;
            workflowSelection=selection.ToArray();workflowPrimary=selectedId;
            try
            {
                floating=new FloatingNotes(source,ids,cut);floatingHistory=History;
                floatingHidden=cut?new HashSet<int>(floating.SourceIds):null;floatingTarget=SnapTime(CurrentMs);floatingValid=true;
                ghostEvents=floating.Preview(floatingTarget);if(ghostRenderer!=null)ghostRenderer.UpdateConnections(ghostEvents);showNotes=true;
                status=cut?L("剪贴预览：原音符仅暂时隐藏；左键落下，右键恢复原位。","Cut preview: originals only HIDDEN; LMB places, RMB restores."):
                    L("复制预览：滚轮或进度条浏览，左键落下，右键取消。","Copy preview: browse with wheel/progress, LMB places, RMB cancels.");
            }
            catch(Exception ex){CancelWorkflow();status=ex.Message;}
        }
        // Candidate selection compares projected guide distances, not only time distances.
        private bool PointerAuthoringTime(Vector2 mouse,double preferred,out double time,out int lane)
        {
            Vector3 q;time=0;if(!FloorPoint(mouse,out q,out lane))return false;
            if(!TimeFromDepth(q.z,preferred,out time))return false;
            if(!showGrid){time=AuthoringGrid.RoundMs(time);return true;}
            double nearest=authoringGrid.Nearest(time,Division).TimeMs;
            double[] ts={nearest,authoringGrid.Adjacent(nearest,-1,Division),authoringGrid.Adjacent(nearest,1,Division)};
            double best=double.PositiveInfinity,chosen=nearest;
            foreach(double t in ts)
            {
                Vector2 a,b;float z=Depth(t);
                if(!ScreenPoint(BeatGuideGeometry.Point(Profile,lane,0,z),out a)||!ScreenPoint(BeatGuideGeometry.Point(Profile,lane,1,z),out b))continue;
                Vector2 d=b-a;float f=d.sqrMagnitude<.001f?0:Mathf.Clamp01(Vector2.Dot(mouse-a,d)/d.sqrMagnitude);
                double dist=(mouse-(a+d*f)).sqrMagnitude;
                if(dist<best){best=dist;chosen=t;}
            }
            time=chosen;return true;
        }
        private bool RefreshWorkflowHover(Vector2 mouse)
        {
            if(!WorkflowActive)return false;guideActive=false;hoverValid=false;
            if(transport.Playing||radial.Pressed||!showUi||!AllowedCanvas(mouse))return true;
            double t;int lane;if(!PointerAuthoringTime(mouse,CurrentMs,out t,out lane))return true;
            hoverValid=true;hoverTime=t;guideActive=true;guideAir=false;guideTime=t;
            if(floating!=null)
            {
                if(History!=floatingHistory){CancelWorkflow();return true;}
                if(!floatingValid||floatingTarget!=t)
                {
                    try{ghostEvents=floating.Preview(t);if(ghostRenderer!=null)ghostRenderer.UpdateConnections(ghostEvents);floatingTarget=t;floatingValid=true;}
                    catch(Exception ex){floatingValid=false;ghostEvents=new SpcEvent[0];status=ex.Message;}
                }
            }
            return true;
        }
        private bool ClickWorkflow(Event e)
        {
            if(!WorkflowActive)return false;RefreshWorkflowHover(e.mousePosition);
            if(!hoverValid)return true;
            if(rangeActive)
            {
                if(!rangeHasStart){rangeStart=hoverTime;rangeHasStart=true;status=L("段选：请选择结束时间，右键取消。","Range: select end time; RMB cancels.");}
                else
                {
                    var ids=SelectionAuthoring.InRange(events,rangeStart,hoverTime);rangeActive=rangeHasStart=false;
                    SetSelection(ids);status=L("段选完成：","Range selected: ")+ids.Length+L(" 个音符；Ctrl 点击可加选或取消。"," notes. Ctrl-click refines selection.");
                }
                return true;
            }
            if(!floatingValid)return true;
            var pending=floating;double target=floatingTarget;int[] added=new int[0];
            if(Change(d=>added=pending.Place(d,target,newPasteGroups)))
            {
                floating=null;floatingHidden=null;floatingHistory=null;ghostEvents=new SpcEvent[0];floatingValid=false;
                SetSelection(added,added.Length>0?added[0]:-1);
                status=L("已落下到内存，一次撤销；Ctrl+S 才写盘。","Placed in memory (one undo). Ctrl+S writes disk.");
            }
            return true;
        }
        private void DrawWorkflowOverlay()
        {
            if(!WorkflowActive||!guiShowUi)return;
            if(rangeActive&&rangeHasStart)DrawTimeGuide(rangeStart,false);
            if(hoverValid)DrawTimeGuide(hoverTime,false);
            string text=rangeActive?(rangeHasStart?L("段选：结束时间","Range: END"):L("段选：开始时间","Range: START")):
                (floating.Cut?L("剪贴","CUT"):L("复制","COPY"))+" ×"+floating.Count;
            GUI.Label(new Rect(hoverPointer.x+18,hoverPointer.y+18,420,24),text+"  "+(hoverValid?hoverTime.ToString("0",CI)+" ms":"")+L(" · 右键取消"," · RMB cancels"),HudStyle(14,new Color(1,.93f,.5f)));
        }
        private void AlignSelection()
        {
            if(!showGrid||selection.Count==0||transport.Playing)return;
            var ids=selection.ToArray();
            if(Change(d=>SelectionAuthoring.Align(d,ids,authoringGrid,Division)))status=L("已将所选音符分别对齐网格（长条头尾独立），一次撤销。","Selection aligned independently; long-note heads AND tails, one undo.");
        }
    }
}
