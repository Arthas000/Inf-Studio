using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        // GUILayout is a Layout -> input -> Repaint protocol, NOT a one-shot draw API.
        // Freeze the CONTROL TREE (including time-window lists) at Layout. Do not let
        // playback/selection or a preceding button change the number of controls halfway
        // through a group. Commands from widgets run BEFORE the next Layout is built.
        private readonly Queue<Action> guiCommands=new Queue<Action>();
        private bool guiFrameReady,drainingGuiCommands,guiWorkflowActive;
        private bool guiShowUi,guiShowInspector,guiShowCalibration,guiShowHelp,guiShowDetail;
        private SpcEvent guiPrimary;
        private int guiPrimaryId=-1,guiPrimaryLine;
        private string[] guiFieldValues=new string[0],guiDiagnostics=new string[0];
        private string guiSourceText="";
        private string[] guiOpaqueDescriptions=new string[0];
        private string[] guiSkyInfo=new string[0];
        private AudioClip guiAudioClip;
        private SpcEvent[] guiNearNotes=new SpcEvent[0],guiTimingEvents=new SpcEvent[0];

        private void QueueGui(Action action)
        {
            if(action!=null)guiCommands.Enqueue(action);
        }
        private void BeginGuiFrame()
        {
            if(drainingGuiCommands)return;
            drainingGuiCommands=true;
            try
            {
                // Modal Editor dialogs may end/re-enter an IMGUI loop. No BeginArea or
                // BeginScrollView has been opened at this point. Never swallow ExitGUI.
                int count=guiCommands.Count;
                for(int i=0;i<count;i++)
                {
                    Action command=guiCommands.Dequeue();
                    try{command();}
                    catch(ExitGUIException){throw;}
                    catch(Exception ex){status="UI command failed: "+ex.Message;Debug.LogException(ex,this);}
                }
            }
            finally{drainingGuiCommands=false;}
            EnsureBrowseGridLock();
            guiWorkflowActive=WorkflowActive;guiExtraParserNotices=extraParserNotices;
            guiShowUi=showUi&&!transport.Playing;guiShowInspector=showInspector&&guiShowUi;guiShowCalibration=showCalibration&&guiShowUi;
            guiShowHelp=showHelp&&guiShowUi;guiShowDetail=showDetailTimeline&&guiShowUi;
            guiPrimaryId=selectedId;guiPrimary=events.FirstOrDefault(n=>n.SourceId==guiPrimaryId);
            guiPrimaryLine=guiPrimary!=null?History.Document.SourceIndex(guiPrimaryId)+1:0;
            guiFieldValues=fieldValues; // Hold THIS array, even if a later input refreshes fieldValues.
            guiSourceText=sourceEditText;guiAudioClip=transport.Clip;
            var skyInfo=new List<string>();
            if(guiPrimary!=null&&guiPrimary.Kind==EventKind.SkyArea)
            {
                // Compute diagnostics here, NEVER catch a GUILayout/ExitGUI exception
                // and then try to draw a different number of controls inside its group.
                try
                {
                    var range=ChartMath.SkyAt(guiPrimary,(guiPrimary.TimeMs+guiPrimary.EndMs)*.5);
                    skyInfo.Add("Midpoint L/R: "+range.Left.ToString("0.######",CI)+" / "+range.Right.ToString("0.######",CI));
                    skyInfo.Add("Midpoint width: "+range.Width.ToString("0.######",CI));
                    double atMin;double min=ChartMath.SkyMinimumWidth(guiPrimary,out atMin);
                    skyInfo.Add("Minimum width: "+min.ToString("0.######",CI)+" at "+(atMin*100).ToString("0.##",CI)+"%");
                    if(min<0)skyInfo.Add("WARNING: left/right edges cross internally. Source retained; adjust easing or endpoints.");
                }
                catch(Exception ex){skyInfo.Add("Sky diagnostic: "+ex.Message);}
            }
            guiSkyInfo=skyInfo.ToArray();
            double at=CurrentMs;
            guiNearNotes=events.Where(n=>n.IsNote&&n.TimeMs>=at-2000&&n.TimeMs<=at+6000).OrderBy(n=>n.TimeMs).Take(50).ToArray();
            guiTimingEvents=events.Where(n=>n.Kind==EventKind.Chart||n.Kind==EventKind.Bpm||n.Kind==EventKind.Track).OrderBy(n=>Math.Abs(n.TimeMs-at)).Take(12).ToArray();
            guiDiagnostics=History.Document.Diagnostics.Concat(events.Where(n=>n.IsNote).Select(n=>AirReachBounds.Violation(n)==null?null:"AIR BOUNDS ID "+n.SourceId+": "+AirReachBounds.Violation(n)).Where(x=>x!=null)).Take(12).ToArray();
            guiOpaqueDescriptions=events.Where(n=>n.Name=="icp_event").Take(8).Select(n=>TimingReferenceMerge.DescribeOpaque(History.Document,n)).ToArray();
            CaptureSongGuiFrame();CaptureWorkspaceFrame();
            guiFrameReady=true;
        }
    }
}
