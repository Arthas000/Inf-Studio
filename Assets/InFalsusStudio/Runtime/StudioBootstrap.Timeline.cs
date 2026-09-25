using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        [Serializable] private sealed class SessionSettings
        {
            public int version=5;
            public double wheelUnitsPerStep=3;
            public bool beatGrid=true;
            public int airGridDivision=20;
            public double airGridStep=5,skyDefaultWidth=10;
            public int tapDefaultWidth=1,holdDefaultWidth=1,skyDefaultLeftEase,skyDefaultRightEase;
            public string chartFile,audioPath;
            public double audioOffsetMs,playheadMs,loopA,loopB;
            public bool loopEnabled,timeLayout;
            public float scroll=1;
            public int snapDivision=4;
            public double[] bookmarks;
        }
        private string workingCopyPath="",loadedAudioPath="";
        private bool showDetailTimeline,loopEnabled;
        private double loopA,loopB=8000,timelineSpanMs=8000;
        private readonly List<double> bookmarks=new List<double>();
        private float[] waveform;
        private AudioClip waveformClip;
        private int waveformGeneration;
        private string waveformStatus="No audio loaded. Timeline still edits note times.";
        private Rect detailRect;
        private double detailStart,detailEnd,detailDragSpan;
        private struct TimelineHit {public Rect Rect;public int Id;}
        private readonly List<TimelineHit> timelineHits=new List<TimelineHit>();
        private void BeginWaveform(AudioClip clip)
        {waveformGeneration++;waveform=null;waveformClip=clip;if(clip!=null)StartCoroutine(BuildWaveform(clip,waveformGeneration));}
        private IEnumerator BuildWaveform(AudioClip clip,int generation)
        {
            waveformStatus="Building waveform...";
            const int bins=4096;var peaks=new float[bins];int frames=clip.samples,channels=clip.channels;
            if(frames<=0||channels<=0){waveformStatus="Audio has no readable samples.";yield break;}
            // Yield every chunk so long songs do not stall all editing while the waveform is built.
            for(int offset=0;offset<frames;)
            {
                if(generation!=waveformGeneration||clip==null)yield break;
                int n=Math.Min(16384,frames-offset);var data=new float[n*channels];
                if(!clip.GetData(data,offset)){waveformStatus="Waveform unavailable: set audio import to Decompress On Load.";yield break;}
                for(int j=0;j<n;j++)
                {
                    int bin=Math.Min(bins-1,(int)((long)(offset+j)*bins/frames));float peak=0;
                    for(int c=0;c<channels;c++)peak=Mathf.Max(peak,Mathf.Abs(data[j*channels+c]));
                    peaks[bin]=Mathf.Max(peaks[bin],peak);
                }
                offset+=n;yield return null;
            }
            if(generation==waveformGeneration){waveform=peaks;waveformStatus="Waveform ready. Click blank timeline to seek; Alt + drag a note horizontally to change time.";}
        }
        private void InstallAudioClip(AudioClip clip,string path,bool owned)
        {
            if(songProject!=null&&(string.IsNullOrEmpty(path)||!Path.GetFullPath(path).Equals(songProject.AudioPath,StringComparison.OrdinalIgnoreCase)))
            {if(owned&&clip!=null)Destroy(clip);status="Project main audio is locked to the verified folder file; choose another song folder to replace it.";return;}
            transport.SetClip(clip);if(ownedAudioClip!=null&&ownedAudioClip!=clip)Destroy(ownedAudioClip);
            ownedAudioClip=owned?clip:null;loadedAudioPath=path??"";BeginWaveform(clip);RefreshDocument();
            status="Audio ready. Scroll speed and playback rate remain independent.";
        }
        private void TickSession()
        {
            if(!initialized)return;
            if(transport.Clip!=waveformClip)BeginWaveform(transport.Clip);
            if(loopEnabled&&transport.Playing&&loopB>loopA+.001&&CurrentMs>=loopB)
                transport.Seek(loopA+(CurrentMs-loopA)%(loopB-loopA));
        }
        private void SaveWorkingCopy(bool saveAs)
        {
            if(!CommitAllInputFieldsNow())return;if(!CommitPendingNoteNow())return;if(WorkflowActive||gesture!=Gesture.None)CancelGesture();if(songProject!=null){if(saveAs)ExportSongChart();else SaveSongDifficulty();return;}if(saveAs||workingCopyPath.Length==0)ChooseSave();else SaveCopyTo(workingCopyPath);}
        private void SaveCopyTo(string path)
        {
            try
            {
                string full=Path.GetFullPath(path);
                if(loadedPath.Length>0&&string.Equals(full,loadedPath,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a different file. The imported original is protected.");
                if(songProject!=null&&(
                    songProject.Difficulties.Any(d=>string.Equals(full,d.SourcePath,StringComparison.OrdinalIgnoreCase)||string.Equals(full,d.WorkingPath,StringComparison.OrdinalIgnoreCase))||
                    File.Exists(full)&&full.StartsWith(songProject.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("Export to a NEW file, not an existing song file or any difficulty workspace. Original binary/JSON and song assets are protected. Use Save project to commit all changed difficulties.");
                History.Document.Save(full);
                if(!File.ReadAllBytes(full).SequenceEqual(History.Document.ToBytes()))throw new IOException("Saved bytes did not match the document.");
                if(songProject==null){workingCopyPath=full;savedBytes=History.Document.ToBytes();documentDirty=false;SavePresentationPreferences();}
                // A project export is a separate file. It does NOT commit drafts to the project.
                var s=new SessionSettings{wheelUnitsPerStep=browseWheel.UnitsPerStep,chartFile=Path.GetFileName(full),audioPath=loadedAudioPath,audioOffsetMs=transport.AudioOffsetMs,playheadMs=CurrentMs,loopA=loopA,loopB=loopB,loopEnabled=loopEnabled,timeLayout=linearTimeLayout,scroll=Profile.scrollMultiplier,snapDivision=Division,beatGrid=showGrid,airGridDivision=airGrid.Division,airGridStep=(double)airGrid.StepPercent,skyDefaultWidth=skyWidthPercent,tapDefaultWidth=tapWidth,holdDefaultWidth=holdWidth,skyDefaultLeftEase=skyLeftEase,skyDefaultRightEase=skyRightEase,bookmarks=bookmarks.ToArray()};
                try{File.WriteAllText(full+".studio.json",JsonUtility.ToJson(s,true));status=(songProject==null?"Saved SPC + session sidecar: ":"Exported copy only; project drafts still require Save: ")+full;}
                catch(Exception ex){status="SPC saved and verified; session sidecar failed: "+ex.Message;}
            }
            catch(Exception ex){status="Save failed (previous file retained): "+ex.Message;}
        }
        private void LoadSessionSidecar(string chartPath)
        {
            workingCopyPath="";bookmarks.Clear();loopEnabled=false;
            string sidecar=chartPath+".studio.json";if(!File.Exists(sidecar))return;
            try
            {
                var s=JsonUtility.FromJson<SessionSettings>(File.ReadAllText(sidecar));if(s==null||s.version!=2&&s.version!=3&&s.version!=4&&s.version!=5)return;
                if(!double.IsNaN(s.audioOffsetMs)&&!double.IsInfinity(s.audioOffsetMs))transport.SetAudioOffset(s.audioOffsetMs);
                offsetText=transport.AudioOffsetMs.ToString("0.###",CI);
                if(!double.IsNaN(s.playheadMs)&&!double.IsInfinity(s.playheadMs))transport.Seek(Math.Max(0,s.playheadMs));
                loopA=double.IsNaN(s.loopA)||double.IsInfinity(s.loopA)?0:Math.Max(0,s.loopA);
                loopB=double.IsNaN(s.loopB)||double.IsInfinity(s.loopB)?loopA+8000:Math.Max(loopA+.001,s.loopB);
                loopEnabled=s.loopEnabled;linearTimeLayout=s.timeLayout;
                if(!float.IsNaN(s.scroll)&&!float.IsInfinity(s.scroll))Profile.scrollMultiplier=SnapScroll(s.scroll);
                int index=Array.IndexOf(SnapDivisions,s.snapDivision);if(index>=0)snapIndex=index;customDivision=Math.Max(1,Math.Min(1024,s.snapDivision));
                if(s.version>=4 && s.wheelUnitsPerStep>=.1&&s.wheelUnitsPerStep<=10)browseWheel.Configure(s.wheelUnitsPerStep);
                if(s.version>=3)
                {
                    SetBeatGrid(s.beatGrid);
                    if(s.version>=5&&s.airGridDivision>0)airGrid.SetDivision(s.airGridDivision);else if(s.airGridStep>=.1&&s.airGridStep<=100)airGrid.SetStep((decimal)s.airGridStep);
                    if(s.skyDefaultWidth>0&&s.skyDefaultWidth<=100)skyWidthPercent=s.skyDefaultWidth;
                    tapWidth=Math.Max(1,Math.Min(4,s.tapDefaultWidth));holdWidth=Math.Max(1,Math.Min(4,s.holdDefaultWidth));
                    skyLeftEase=Math.Max(0,Math.Min(2,s.skyDefaultLeftEase));skyRightEase=Math.Max(0,Math.Min(2,s.skyDefaultRightEase));
                    xStepText=airGrid.Division.ToString(CI);skyWidthText=skyWidthPercent.ToString("0.###",CI);
                }
                if(s.bookmarks!=null)bookmarks.AddRange(s.bookmarks.Where(x=>x>=0&&!double.IsNaN(x)&&!double.IsInfinity(x)));
                if(!string.IsNullOrEmpty(s.audioPath)&&File.Exists(s.audioPath))StartCoroutine(LoadAudio(s.audioPath));
            }
            catch(Exception ex){status="SPC loaded; ignored invalid session sidecar: "+ex.Message;}
        }
        private void NewChart()
        {
            CancelGesture();if(!LeaveCurrentDocumentSafely())return;DetachSongProject();
            transport.Pause();LoadDocument(SpcDocument.Parse("chart(100,4)\n"));loadedPath="";workingCopyPath="";
            transport.Seek(0);showNotes=true;UseTool(Tool.Select);radial.InPointMode=false;bookmarks.Clear();loopEnabled=false;status="New chart. Press RMB for Quick Place; audio kept.";
        }
        private void DrawLoopControls()
        {
            if(GUILayout.Button("A",GUILayout.Width(26))){loopA=CurrentMs;if(loopB<=loopA)loopB=DefaultEnd(loopA);}
            if(GUILayout.Button("B",GUILayout.Width(26))){loopB=CurrentMs;if(loopB<=loopA){loopEnabled=false;status="Loop B must be after A.";}}
            bool requested=GUILayout.Toggle(loopEnabled,"Loop",GUILayout.Width(56));loopEnabled=requested&&loopB>loopA+.001;
            if(GUILayout.Button("Mark",GUILayout.Width(43))){if(!bookmarks.Any(x=>Math.Abs(x-CurrentMs)<.1)){bookmarks.Add(CurrentMs);bookmarks.Sort();}}
            if(GUILayout.Button("Next",GUILayout.Width(43))){double t=bookmarks.FirstOrDefault(x=>x>CurrentMs+.1);if(t==0&&bookmarks.Count>0)t=bookmarks[0];transport.Seek(t);}
        }
        private float TimelineX(double t){return detailRect.x+62+(float)((t-detailStart)/(detailEnd-detailStart))*(detailRect.width-70);}
        private double TimelineTime(float x){return detailStart+(x-detailRect.x-62)/(detailRect.width-70)*(detailEnd-detailStart);}
        private void DrawDetailTimeline()
        {
            if(!guiShowDetail)return;
            detailRect=new Rect(bottomRect.x+5,bottomRect.y+28,bottomRect.width-10,bottomRect.height-56);
            detailStart=Math.Max(0,CurrentMs-timelineSpanMs*.25);detailEnd=detailStart+timelineSpanMs;timelineHits.Clear();
            GUI.Box(detailRect,GUIContent.none);
            Rect plot=new Rect(detailRect.x+62,detailRect.y+18,detailRect.width-70,detailRect.height-23);
            GUI.Label(new Rect(detailRect.x+5,detailRect.y+1,detailRect.width-10,20),"TIME VIEW  "+detailStart.ToString("0",CI)+" - "+detailEnd.ToString("0",CI)+" ms   |  Ctrl+wheel zoom  |  A "+loopA.ToString("0",CI)+" / B "+loopB.ToString("0",CI));
            if(Event.current.type==EventType.Repaint)
            {
                if(showGrid)foreach(var line in authoringGrid.Between(detailStart,detailEnd,Division))
                {
                    float x=TimelineX(line.TimeMs);Color c=line.IsBar?new Color(.16f,.55f,1,1):line.IsBeat?new Color(.4f,1,.65f,.95f):new Color(.65f,.88f,1,.65f);
                    LineGui(new Vector2(x,plot.y),new Vector2(x,plot.yMax),c,line.IsBar?2:1);
                }
                if(waveform!=null&&transport.Clip!=null)
                {
                    int columns=Math.Min(800,(int)plot.width);double length=transport.Clip.length*1000.0;
                    for(int i=0;i<columns;i++)
                    {
                        double time=detailStart+(detailEnd-detailStart)*i/columns+transport.AudioOffsetMs;
                        if(time<0||time>=length)continue;int bin=Math.Min(waveform.Length-1,(int)(time/length*waveform.Length));
                        float x=plot.x+i*plot.width/columns,h=waveform[bin]*12;
                        LineGui(new Vector2(x,plot.y+13-h),new Vector2(x,plot.y+13+h),new Color(.34f,.82f,.73f,.6f));
                    }
                }
            }
            GUI.Label(new Rect(detailRect.x+3,plot.y+3,58,20),"Audio");
            if(waveform==null)GUI.Label(new Rect(plot.x+5,plot.y+3,plot.width-10,20),waveformStatus);
            float laneTop=plot.y+30,row=Mathf.Max(8,(plot.height-30)/7);
            for(int lane=0;lane<7;lane++)GUI.Label(new Rect(detailRect.x+3,laneTop+lane*row,58,row+3),lane==6?"Sky/Flick":new[]{"Shift","A","S","D","F","Space"}[lane]);
            foreach(var note in events.Where(x=>x.IsNote&&x.EndMs>=detailStart&&x.TimeMs<=detailEnd&&!(floatingHidden!=null&&floatingHidden.Contains(x.SourceId))))
            {
                int lane=IsGround(note)?Mathf.Clamp(note.Lane,0,5):6;float x0=Mathf.Clamp(TimelineX(note.TimeMs),plot.x,plot.xMax),x1=Mathf.Clamp(TimelineX(note.EndMs),plot.x,plot.xMax);
                Rect r=new Rect(x0,laneTop+lane*row+1,Mathf.Min(Mathf.Max(5,x1-x0),Mathf.Max(1,plot.xMax-x0)),Mathf.Max(5,row-2));
                timelineHits.Add(new TimelineHit{Rect=r,Id=note.SourceId});
                Color old=GUI.color;GUI.color=selection.Contains(note.SourceId)?Color.white:note.Kind==EventKind.Flick?(note.Get(4)==16?Profile.leftFlick:Profile.rightFlick):note.Kind==EventKind.SkyArea?Profile.skyEdge:Profile.tapColor;
                GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;
            }
            if(floating!=null&&floatingValid)foreach(var n in ghostEvents)
            {
                if(n.EndMs<detailStart||n.TimeMs>detailEnd)continue;
                int lane=IsGround(n)?Mathf.Clamp(n.Lane,0,5):6;
                float x0=Mathf.Clamp(TimelineX(n.TimeMs),plot.x,plot.xMax),x1=Mathf.Clamp(TimelineX(n.EndMs),plot.x,plot.xMax);
                HudFill(new Rect(x0,laneTop+lane*row+1,Mathf.Max(4,x1-x0),Mathf.Max(5,row-2)),new Color(.6f,1,1,.27f));
            }
            foreach(double t in bookmarks)if(t>=detailStart&&t<=detailEnd)LineGui(new Vector2(TimelineX(t),plot.y),new Vector2(TimelineX(t),plot.yMax),new Color(1,.7f,.2f,.7f));
            LineGui(new Vector2(TimelineX(CurrentMs),plot.y),new Vector2(TimelineX(CurrentMs),plot.yMax),Color.white,2);
        }
        private void HandleDetailTimeline(Event e)
        {
            if(!guiShowDetail||!guiShowUi||e.type==EventType.Used||radial.Pressed)return;
            if(e.type==EventType.ScrollWheel&&detailRect.Contains(e.mousePosition)&&gesture==Gesture.None)
            {
                if(e.control||e.command)timelineSpanMs=Math.Max(500,Math.Min(120000,timelineSpanMs*Math.Pow(1.2,e.delta.y)));
                else BrowseWheel(e.delta.y);
                e.Use();return;
            }
            if(e.type!=EventType.MouseDown||e.button!=0||!detailRect.Contains(e.mousePosition)||GUIUtility.hotControl!=0)return;
            if(WorkflowActive){transport.Pause();transport.Seek(SnapTime(TimelineTime(e.mousePosition.x)));e.Use();return;}
            var ids=timelineHits.Where(h=>h.Rect.Contains(e.mousePosition)).Select(h=>h.Id).Reverse().ToList();transport.Pause();GUI.FocusControl(null);
            if(ids.Count==0){transport.Seek(SnapTime(TimelineTime(e.mousePosition.x)));e.Use();return;}
            lastPick=ids;cycleIndex=0;int id=ids[0];
            if(e.control||e.command||e.shift){if(!selection.Remove(id))selection.Add(id);selectedId=id;RefreshSelectionFields();FollowSelectedSkyGroup();}
            else
            {
                if(!selection.Contains(id))Select(id);else{selectedId=id;RefreshSelectionFields();FollowSelectedSkyGroup();}
                if(EditHeld)
                {
                    ResetPointDraft();dragIds=selection.ToArray();dragReferenceTime=events.First(n=>n.SourceId==id).TimeMs;detailDragSpan=timelineSpanMs;
                    History.Begin();Capture(e,Gesture.TimelineMove);
                }
            }
            e.Use();
        }
        private void PreviewTimelineDrag(Event e)
        {
            double delta=(e.mousePosition.x-pointerDown.x)/(detailRect.width-70)*detailDragSpan;
            double t=SnapTime(dragReferenceTime+delta,e.alt);
            try{History.Preview(d=>EditOperations.PointerMove(d,dragIds,t-dragReferenceTime,0,0,showGrid?authoringGrid:null,Division));AfterDocumentEdit();guideActive=true;guideAir=false;guideTime=t;}
            catch(Exception ex){status=ex.Message;}
        }
    }
}
