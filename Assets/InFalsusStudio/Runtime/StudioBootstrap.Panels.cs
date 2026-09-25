using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private bool toolsExpanded,guiToolsExpanded;
        private bool showMeasureLines=true,showCenterCombo=true,showAirShadows=true;
        private bool noteSourceExpanded,guiNoteSourceExpanded;
        private Rect dockRect,notePanelRect;
        private Vector2 toolsScroll,noteScroll;
        private StudioKeySounds keySounds;
        private SkyAttackPolicy skyAttackPolicy=SkyAttackPolicy.ContinuousRegionStart;
        private bool groundHoldAttacks;
        private NoteProperties noteForm,guiNoteForm;
        private EditHistory noteFormHistory;
        private string noteRawText="";
        private float WorkspaceTop {get{return guiShowSongHud?Mathf.Max(8,hudOrigin.y+160*hudScale):8;}}
        private bool NotePanelVisible {get{return guiShowUi&&!guiWorkflowActive&&!guiToolsExpanded&&!guiShowCalibration&&!guiShowInspector&&!guiShowSongPanel&&guiNoteForm!=null;}}
        private void CaptureWorkspaceFrame()
        {
            guiToolsExpanded=toolsExpanded;guiNoteSourceExpanded=noteSourceExpanded;
            CaptureBatchProperties();
        }
        private void LayoutWorkspace(float bottomHeight)
        {
            float s=Mathf.Clamp(hudScale,.55f,1.5f),y=WorkspaceTop;
            bottomRect=new Rect(8,Screen.height-bottomHeight-8,Screen.width-16,bottomHeight);
            dockRect=new Rect(hudOrigin.x+8*s,y,110*s,Mathf.Max(150,bottomRect.y-y-6));
            float left=dockRect.xMax+8*s,available=Mathf.Max(140,bottomRect.y-y-8);
            topRect=guiToolsExpanded?new Rect(left,y,Mathf.Min(448*s,Screen.width-left-140*s),available):new Rect(-10,-10,0,0);
            float formHeight=guiNoteForm!=null&&guiNoteForm.Kind==EventKind.SkyArea?690:510;if(guiNoteSourceExpanded)formHeight+=180;
            notePanelRect=new Rect(left,y,Mathf.Min(380*s,Screen.width-left-140*s),Mathf.Min(available,formHeight*s));
            inspectorRect=new Rect(left,y,Mathf.Min(440*s,Screen.width-left-140*s),available);
            calibrationRect=new Rect(left,y,Mathf.Min(440*s,Screen.width-left-140*s),available);
            songPanelRect=new Rect(left,y,Mathf.Min(440*s,Screen.width-left-140*s),available);
        }
        private bool WorkspaceBlocksInput(Vector2 point)
        {return ChromeBlocks(point)||guiShowUi&&(dockRect.Contains(point)||guiToolsExpanded&&topRect.Contains(point)||NotePanelVisible&&notePanelRect.Contains(point));}
        private void CloseLeftPanels(){toolsExpanded=false;showInspector=false;showSongPanel=false;showCalibration=false;}
        private void DrawSideDock()
        {
            float s=Mathf.Clamp(hudScale,.55f,1.5f),x=dockRect.x+3*s,w=dockRect.width-6*s,y=dockRect.y;
            float h=Mathf.Min(35*s,Mathf.Max(21,(dockRect.height-104*s)/14)),gap=4*s;
            Action<string,string,Action,bool> button=(icon,text,action,active)=>{if(IconButton(new Rect(x,y,w,h),icon,text,active))QueueGui(action);y+=h+gap;};
            button("settings",L("设置","Settings"),()=>{bool open=!toolsExpanded;CloseLeftPanels();toolsExpanded=open;},guiToolsExpanded);
            button("grid",L("时序事件","Timing"),()=>{bool open=!showInspector;CloseLeftPanels();showInspector=open;},guiShowInspector);
            button("auto",L(autoCursor?"自动: 开":"自动: 关",autoCursor?"Auto: ON":"Auto: OFF"),()=>autoCursor=!autoCursor,autoCursor);
            button("range",L("段落选择","Range"),BeginRangeSelection,rangeActive);
            button("save",L("保存","Save")+(HasUnsavedEdits?" *":""),()=>SaveWorkingCopy(false),HasUnsavedEdits);
            button("open",L("打开歌曲","Open song"),ChooseSongFolder,false);
            button("undo",L("撤销","Undo"),()=>Undo(false),false);button("redo",L("重做","Redo"),()=>Undo(true),false);
            button("wave",L("波形","Wave"),()=>showDetailTimeline=!showDetailTimeline,guiShowDetail);
            button("settings",L("歌曲信息","Song"),()=>{bool open=!showSongPanel;CloseLeftPanels();showSongPanel=open;},guiShowSongPanel);
            button("settings",L("相机","Camera"),()=>{bool open=!showCalibration;CloseLeftPanels();showCalibration=open;},guiShowCalibration);
            button("help",L("帮助 / EN","Help / 中文"),()=>showHelp=!showHelp,guiShowHelp);
            button("exit",Time.realtimeSinceStartupAsDouble<=exitDeadline?L("再次退出!","Confirm exit!"):L("退出","Exit"),RequestExit07,false);
            HudLabel(new Rect(x,y,w,18*s),L("节拍细分 1/N","Subdivision 1/N"),11,new Color(.68f,.85f,.9f));y+=19*s;
            ChromeField(new Rect(x,y,w,25*s),"field_division07",ref divisionInput,Division.ToString(CI),v=>{int n;if(!int.TryParse(v,out n)||n<1||n>1024)throw new FormatException("N must be an integer in 1..1024");SetSubdivision(n);});y+=28*s;
            HudLabel(new Rect(x,y,w,18*s),L("播放速度 %","Playback %"),11,new Color(.68f,.85f,.9f));y+=19*s;
            if(GUI.Button(new Rect(x,y,w,25*s),(transport.Rate*100).ToString("0",CI)+"%  ▸"))QueueGui(CyclePlaybackRate);y+=27*s;
            HudLabel(new Rect(x,y,w,18*s),"v0.9 · RAM → Save",10,new Color(.55f,.78f,.83f));
        }
        private void DrawToolDrawer()
        {
            GUILayout.BeginArea(topRect,GUI.skin.box);BeginPanelContent(ref toolsScroll,topRect);
            GUILayout.BeginHorizontal();GUILayout.Label("IN FALSUS STUDIO 0.9 / TOOLS");
            if(GUILayout.Button("Close",GUILayout.Width(62)))QueueGui(()=>toolsExpanded=false);GUILayout.EndHorizontal();
            GUILayout.Label("RMB: point menu. Alt: handles · Ctrl: multi-select · Ctrl+S: save.");
            GUILayout.BeginHorizontal();if(GUILayout.Button("New"))QueueGui(NewChart);if(GUILayout.Button("Load SPC"))QueueGui(ChooseChart);
            if(GUILayout.Button("Save As"))QueueGui(()=>SaveWorkingCopy(true));GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Audio..."))QueueGui(ChooseAudio);
            if(GUILayout.Button("Copy"))QueueGui(()=>CopySelection(false));if(GUILayout.Button("Paste"))QueueGui(()=>PasteSelection(false));GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("Duplicate"))QueueGui(()=>PasteSelection(true));if(GUILayout.Button("Mirror"))QueueGui(MirrorSelection);if(GUILayout.Button("Delete"))QueueGui(DeleteSelected);GUILayout.EndHorizontal();
            GUILayout.Space(6);GUILayout.Label(L("放置工具（右键点立得亦可）","Placement tool"));
            GUILayout.BeginHorizontal();DrawerTool("Select",Tool.Select);DrawerTool("Tap",Tool.Tap);DrawerTool("Hold",Tool.Hold);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();DrawerTool("SkyArea",Tool.Sky);DrawerTool("Flick L",Tool.FlickLeft);DrawerTool("Flick R",Tool.FlickRight);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUILayout.Label("Tap width",GUILayout.Width(84));tapWidth=GUILayout.Toolbar(tapWidth-1,new[]{"1","2","3","4"})+1;GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUILayout.Label("Hold width",GUILayout.Width(84));holdWidth=GUILayout.Toolbar(holdWidth-1,new[]{"1","2","3","4"})+1;GUILayout.EndHorizontal();
            GUILayout.Label("Side lanes 0 / 5 always use width 1.");
            skyWidthText=CommittedRow(L("新建 Sky 宽度 %","New Sky width %"),"field_sky_default_width",skyWidthPercent.ToString("0.##########",CI),value=>
            {double width=Finite(value);if(width<=0||width>100)throw new ArgumentOutOfRangeException("width","0 < width <= 100");skyWidthPercent=width;});
            xStepText=CommittedRow(L("空域坐标网格 1/N（Sky / Flick）","Air coordinate grid 1/N (Sky / Flick)"),"field_air_grid_division",airGrid.Division>0?airGrid.Division.ToString(CI):"",value=>
            {int n;if(!int.TryParse(value,out n))throw new FormatException("N 须是整数。 Integer N required.");airGrid.SetDivision(n);hoverValid=false;});
            GUILayout.Label(L("N=20 为 5%；N=3 为三等分。修改仅影响后续吸附，不重排原谱。","N=20 means 5%; N=3 means thirds. Existing notes are not resampled."));
            string[] ease={"Linear","SineOut","SineIn"};
            GUILayout.BeginHorizontal();if(GUILayout.Button("Sky L: "+ease[skyLeftEase]))QueueGui(()=>skyLeftEase=(skyLeftEase+1)%3);
            if(GUILayout.Button("Sky R: "+ease[skyRightEase]))QueueGui(()=>skyRightEase=(skyRightEase+1)%3);GUILayout.EndHorizontal();
            GUILayout.Space(6);GUILayout.Label(L("视图与网格（不改谱面）","View and grid (not source-note changes)"));
            SetBeatGrid(GUILayout.Toggle(showGrid,L("节拍网格 / 强制时间吸附","Beat grid / mandatory snap")));
            GUILayout.Label(L("细分在左侧输入 N（1/N拍）。","Subdivision N is editable in the left rail."));
            showMeasureLines=GUILayout.Toggle(showMeasureLines,L("关闭节拍网格后仍显示小节线","Persistent measure lines"));
            bool layout=GUILayout.Toggle(linearTimeLayout,L("单调时间布局（仅视图忽略滚速）","Monotonic time layout"));
            if(layout!=linearTimeLayout){CancelGesture();linearTimeLayout=layout;}
            hidePast=GUILayout.Toggle(hidePast,L("裁去已播放的音符","Clip played notes"));showNotes=GUILayout.Toggle(showNotes,L("显示音符","Show notes"));
            showAirShadows=GUILayout.Toggle(showAirShadows,L("空中音符的地面投影","Air note floor projections"));
            showCenterCombo=GUILayout.Toggle(showCenterCombo,L("中央 COMBO（预计）","Center COMBO (estimated)"));
            pickFilter=GUILayout.Toolbar(pickFilter,new[]{"Pick Both","Ground","Air"});
            GUILayout.Space(6);GUILayout.Label(L("音乐音量","Music volume"));transport.Volume=GUILayout.HorizontalSlider(transport.Volume,0,1);DrawKeySoundOptions();
            showFeedback=GUILayout.Toggle(showFeedback,L("简约打击光效 / 粒子","Hit glow / particles"));
            if(GUILayout.Button(chinese?"界面语言: 中文 → English":"UI language: English → 中文"))QueueGui(()=>chinese=!chinese);
            GUILayout.Space(6);GUILayout.Label(L("循环 / 书签","Loop / bookmarks"));GUILayout.BeginHorizontal();DrawLoopControls();GUILayout.EndHorizontal();
            DrawPresentationOptions();
            GUILayout.Space(6);GUILayout.Label(L("验证预设","Reference presets"));
            GUILayout.BeginHorizontal();if(GUILayout.Button("Empty"))QueueGui(()=>{CancelGesture();transport.Pause();showNotes=false;SetSelection(new int[0]);});
            if(GUILayout.Button("Opening ~1826"))QueueGui(()=>Preset(1826));GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button("3610 ms"))QueueGui(()=>Preset(3610));if(GUILayout.Button("5500 ms"))QueueGui(()=>Preset(5500));if(GUILayout.Button("22400 ms"))QueueGui(()=>Preset(22400));GUILayout.EndHorizontal();
            GUILayout.Label(notes.RenderedNotes+" visible / "+notes.DrawnMeasureLines+" measures / "+notes.InvalidNotes+" invalid");
            EndPanelContent();GUILayout.EndArea();
        }
        private void DrawerTool(string label,Tool requested)
        {if(GUILayout.Button((tool==requested?"* ":"")+label))QueueGui(()=>UseTool(requested));}
        private void RebuildKeySoundPlan()
        {if(keySounds!=null)keySounds.Rebuild(new HitSoundPlan(events,skyAttackPolicy,groundHoldAttacks));}
        private void DrawKeySoundOptions()
        {
            GUILayout.Label(L("打击音 / 地面与空域持续音独立","KEY SOUNDS / independent buses"));
            if(keySounds==null){GUILayout.Label("Audio audition is not initialized.");return;}
            keySounds.Enabled=GUILayout.Toggle(keySounds.Enabled,L("启用打击音","Enable key sounds"));
            GUILayout.Label("Master "+keySounds.MasterVolume.ToString("0.00",CI));keySounds.MasterVolume=GUILayout.HorizontalSlider(keySounds.MasterVolume,0,1);
            GUILayout.Label("Attacks "+keySounds.ShotVolume.ToString("0.00",CI));keySounds.ShotVolume=GUILayout.HorizontalSlider(keySounds.ShotVolume,0,1);
            GUILayout.Label("Hold beds "+keySounds.HoldVolume.ToString("0.00",CI));keySounds.HoldVolume=GUILayout.HorizontalSlider(keySounds.HoldVolume,0,1);
            string[] skyPolicies={L("每组首段","Group first"),L("关闭头音","Head sound off")};int chosen=GUILayout.Toolbar(skyAttackPolicy==SkyAttackPolicy.Off?1:0,skyPolicies);int requested=chosen==1?(int)SkyAttackPolicy.Off:(int)SkyAttackPolicy.ContinuousRegionStart;
            if(requested!=(int)skyAttackPolicy)QueueGui(()=>{skyAttackPolicy=(SkyAttackPolicy)requested;RebuildKeySoundPlan();});
            GUILayout.Label(L("默认：同 group 仅第一段有攻击音（间隔不产生新头）。","Only the first Sky in each group has a head attack, even across gaps."));
            bool attacks=GUILayout.Toggle(groundHoldAttacks,"Extra Tap sound on ground Hold heads (optional)");
            if(attacks!=groundHoldAttacks)QueueGui(()=>{groundHoldAttacks=attacks;RebuildKeySoundPlan();});
            GUILayout.Label("Music priority 0 | virtual: "+keySounds.MusicVirtual+" | real voices: "+keySounds.RealVoiceLimit+" | shots budget: "+keySounds.ShotBudget);
            GUILayout.Label("Missing: "+(string.IsNullOrEmpty(keySounds.MissingClips)?"none":keySounds.MissingClips)+" | late / capacity dropped: "+keySounds.LateDropped+" / "+keySounds.CapacityDropped);
        }
        private void DrawTimingBrowser()
        {
            GUILayout.BeginArea(inspectorRect,GUI.skin.box);BeginPanelContent(ref inspectorScroll,inspectorRect);
            GUILayout.BeginHorizontal();GUILayout.Label(L("时序 / 滚速事件","Timing / track"));if(GUILayout.Button(L("关闭","Close"),GUILayout.Width(48)))QueueGui(()=>showInspector=false);GUILayout.EndHorizontal();
            var header=events.FirstOrDefault(n=>n.Kind==EventKind.Chart);var history=History;
            if(header!=null)
            {
                int hid=header.SourceId;
                timingBpm=CommittedRow(L("初始 BPM（音乐拍速）","Initial BPM"),"field_header_bpm_"+hid,header.Get(0).ToString("0.##########",CI),value=>
                {double n=Finite(value);if(n<=0)throw new ArgumentOutOfRangeException("BPM");if(History!=history)throw new InvalidOperationException("谱面已切换。 Chart changed.");if(!Change(d=>AuthoredNumber.Set(d,hid,0,n)))throw new ArgumentException(status);});
                timingMeter=CommittedRow(L("每小节拍数（非细分数）","Beats per measure"),"field_header_meter_"+hid,header.Get(1).ToString("0.##########",CI),value=>
                {double n=Finite(value);if(n<=0)throw new ArgumentOutOfRangeException("meter");if(History!=history)throw new InvalidOperationException("Chart changed.");if(!Change(d=>AuthoredNumber.Set(d,hid,1,n)))throw new ArgumentException(status);});
                if(GUILayout.Button(L("在播放头新建 BPM 事件","Add BPM event at playhead")))QueueGui(()=>AddTimingFromFields(false));
            }
            timingSpeed=CommittedRow(L("新建 track 默认速度（不改音乐 BPM）","New track speed (not BPM)"),"field_timing_speed_default",timingSpeed,value=>{Finite(value);timingSpeed=value;});
            if(GUILayout.Button(L("在播放头新建 track 事件","Add track event at playhead")))QueueGui(()=>
                Change(d=>d.Append("track("+EditOperations.Number(SnapTime(CurrentMs))+","+EditOperations.Number(Finite(timingSpeed))+")")));
            GUILayout.Space(5);
            if(GUILayout.Button(L("从配对明文补入 BPM / 拍号…","Merge BPM/meter from paired plaintext…")))QueueGui(ImportPairedTiming);
            foreach(var ev in guiTimingEvents)if(GUILayout.Button(ev.ToString(),GUI.skin.box)){int id=ev.SourceId;QueueGui(()=>Select(id));}
            if(guiPrimary!=null&&!guiPrimary.IsNote)
            {
                int id=guiPrimaryId;string expected=History.Document.SourceLine(id);
                GUILayout.Label(L("当前语句：回车 / 失焦应用到内存","Current source: Enter / blur applies to memory"));
                sourceEditText=CommittedSource("field_timing_source_"+id,expected,value=>
                {
                    if(History!=history||!History.Document.Contains(id)||History.Document.SourceLine(id)!=expected)throw new InvalidOperationException("源语句已变化。 Source changed.");
                    if(!Change(d=>{d.ReplaceSourceLine(id,value);if(!d.ReadEvents().Any(n=>n.Kind==EventKind.Chart))throw new ArgumentException("Need a valid chart header.");}))throw new ArgumentException(status);
                });
                if(guiPrimary.Kind!=EventKind.Chart&&GUILayout.Button(L("删除该事件","Delete event")))QueueGui(DeleteSelected);
            }
            bool show=GUILayout.Toggle(guiExtraParserNotices,L("高级：未识别内容提示（"+guiDiagnostics.Length+"）","Advanced: parser notices ("+guiDiagnostics.Length+")"),GUI.skin.button);
            if(show!=guiExtraParserNotices)QueueGui(()=>extraParserNotices=show);
            if(guiExtraParserNotices)
            {
                GUILayout.Label(L("普通 // 注释不再逐行提示。真正未知的语句保留原文，不是时序事件。","Ordinary // comments are silent. Unsupported statements are kept verbatim, not rendered as timing events."));
                foreach(string warning in guiDiagnostics)GUILayout.Label(warning);
                foreach(string detail in guiOpaqueDescriptions)GUILayout.Label(detail);
            }
            EndPanelContent();GUILayout.EndArea();
        }
    }
}
