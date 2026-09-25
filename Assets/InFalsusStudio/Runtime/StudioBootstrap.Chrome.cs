using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private bool chinese=true,showFeedback=true;
        private string divisionInput="4",rateInput="100",viewBpmInput="",delayInput="0",scrollInput="1";
        private double viewReferenceBpm; // 0 = follow the chart header. This NEVER edits chart/BPM events.
        private float canonicalUnitsPerSecond=15;
        private bool progressDragging;
        private double exitDeadline=-1;
        private int progressControlId;
        private Rect progressRect,chromeRect,playButtonRect,difficultyRect;
        private readonly Texture2D[] sideDifficultySkins=new Texture2D[4];
        private bool sideSkinsLoaded;
        private Vector2 helpScroll;
        private string L(string zh,string en){return chinese?zh:en;}
        private double InitialBpm {get {var h=events.FirstOrDefault(n=>n.Kind==EventKind.Chart);return h!=null&&h.Get(0)>0?h.Get(0):100;}}
        private void ApplyViewNormalization()
        {
            if(Profile==null)return;
            double reference=viewReferenceBpm>0?viewReferenceBpm:InitialBpm;
            Profile.unitsPerSecondAtSpeedOne=canonicalUnitsPerSecond*(float)(reference/InitialBpm);
        }
        private void TogglePlayback07()
        {
            if(transport.Playing){transport.Pause();return;}
            if(!CommitAllInputFieldsNow()||!CommitPendingNoteNow())return;CancelGesture();CancelHudField();showNotes=true;transport.Play();
        }
        private void LayoutChrome()
        {
            chromeRect=HR(525,7,795,54);playButtonRect=HR(18,90,109,63);
            progressRect=HR(1338,129,565,19);difficultyRect=HR(1828,170,84,342);
        }
        private bool ChromeBlocks(Vector2 p)
        {
            return playButtonRect.Contains(p)||progressRect.Contains(p)||
                guiShowUi&&(chromeRect.Contains(p)||difficultyRect.Contains(p));
        }
        private void EnsureUiFont()
        {EnsureHudAssets();if(hudFont!=null)GUI.skin.font=hudFont;}
        private void DrawExternalChrome()
        {
            EnsureUiFont();LayoutChrome();
            DrawIntegratedPlay();
            DrawSongProgress();
            if(!guiShowUi)return;
            HudFill(chromeRect,new Color(.015f,.032f,.042f,.86f));
            float[] xs={534,873,1004,1132};float[] ws={144,124,120,178};
            string[] labels={L("谱面流速","Scroll speed"),L("基准 BPM（仅视图）","Reference BPM (view)"),L("音乐延迟 ms","Music delay ms"),L("当前时间 ms","Chart time ms")};
            DrawCurrentGroupChrome();
            for(int i=0;i<4;i++)HudLabel(HR(xs[i],10,ws[i],17),labels[i],13,new Color(.7f,.9f,.95f));
            ChromeField(HR(xs[0],29,71,25),"field_chrome_scroll",ref scrollInput,Profile.scrollMultiplier.ToString("0.0",CI),v=>
                {double x=Finite(v);if(x<.1||x>10)throw new ArgumentOutOfRangeException("scroll","0.1..10");Profile.scrollMultiplier=SnapScroll(x);});
            float speed=GUI.HorizontalSlider(HR(xs[0]+77,38,62,18),Profile.scrollMultiplier,.1f,10);
            if(Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint&&Mathf.Abs(speed-Profile.scrollMultiplier)>.0001f)Profile.scrollMultiplier=SnapScroll(speed);
            ChromeField(HR(xs[1],29,ws[1],25),"field_chrome_reference",ref viewBpmInput,(viewReferenceBpm>0?viewReferenceBpm:InitialBpm).ToString("0.###",CI),v=>
                {double x=Finite(v);if(x<0||x>2000)throw new ArgumentOutOfRangeException("reference BPM","0 = chart header, or 0 < BPM <= 2000");viewReferenceBpm=x;ApplyViewNormalization();});
            ChromeField(HR(xs[2],29,ws[2],25),"field_chrome_delay",ref delayInput,(-transport.AudioOffsetMs).ToString("0.###",CI),v=>
                {double x=Finite(v);if(Math.Abs(x)>3600000)throw new ArgumentOutOfRangeException("delay");transport.SetAudioOffset(-x);SyncActiveSongDraft();RefreshProjectDirty();RefreshDocument();});
            ChromeField(HR(xs[3],29,ws[3],25),"field_chrome_time",ref timeText,CurrentMs.ToString("0.###",CI),v=>
                {transport.Pause();transport.Seek(SnapTime(Math.Max(0,Finite(v))));});
            DrawDifficultyTabs();
        }
        private static double Finite(string s)
        {double x;if(!double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out x)||double.IsNaN(x)||double.IsInfinity(x))throw new FormatException("Enter a finite number.");return x;}
        private void ChromeField(Rect r,string name,ref string text,string live,Action<string> commit)
        {text=CommittedField(r,name,live,commit);}
        private static float SnapScroll(double value)
        {return (float)Math.Max(.1,Math.Min(10,Math.Round(value,1,MidpointRounding.AwayFromZero)));}
        private void CyclePlaybackRate()
        {
            double[] rates={1,.75,.5,.25};int current=Array.FindIndex(rates,x=>Math.Abs(x-transport.Rate)<.001);
            transport.SetRate(rates[(current+1)%rates.Length]);
        }
        private void DrawIntegratedPlay()
        {
            bool clicked=GUI.Button(playButtonRect,new GUIContent("",L(transport.Playing?"暂停 / 空格":"播放 / 空格",transport.Playing?"Pause / Space":"Play / Space")),GUIStyle.none);
            if(Event.current.type==EventType.Repaint)
            {
                Color c=playButtonRect.Contains(Event.current.mousePosition)?Color.white:new Color(.6f,.91f,.96f,.92f);
                DrawPlayTrapezoid(playButtonRect,c);
                // Keep the arrow at its original size and calibrated location; enlarge only the button.
                DrawSmallIcon(HR(46.5f,108.5f,18,18),transport.Playing?"pause":"play",c);
            }
            if(clicked)QueueGui(TogglePlayback07);
        }
        private void DrawPlayTrapezoid(Rect r,Color c)
        {
            Vector2[] p={new Vector2(r.x+4*hudScale,r.y+6*hudScale),new Vector2(r.xMax-28*hudScale,r.y+2*hudScale),new Vector2(r.xMax-5*hudScale,r.yMax-4*hudScale),new Vector2(r.x+2*hudScale,r.yMax-10*hudScale)};
            // A low contrast outline uses the existing SCORE nook, not an extra opaque rectangle.
            Color border=new Color(c.r,c.g,c.b,r.Contains(Event.current.mousePosition)?.9f:.4f);
            for(int i=0;i<4;i++)GuiLine(p[i].x,p[i].y,p[(i+1)%4].x,p[(i+1)%4].y,1.2f*hudScale,border);
        }
        private void DrawSongProgress()
        {
            progressControlId=GUIUtility.GetControlID("InFalsusStudio.Progress07".GetHashCode(),FocusType.Passive);
            var e=Event.current;double end=Math.Max(1,durationMs);
            if(Event.current.type==EventType.Repaint)
            {
                HudFill(progressRect,new Color(.12f,.23f,.27f,.85f));
                float f=(float)Math.Max(0,Math.Min(1,CurrentMs/end));HudFill(new Rect(progressRect.x,progressRect.y+3*hudScale,progressRect.width*f,progressRect.height-6*hudScale),new Color(.23f,.88f,.92f,.85f));
                HudLabel(progressRect,CurrentMs.ToString("0",CI)+" / "+end.ToString("0",CI)+" ms",11,Color.white,TextAnchor.MiddleCenter);
            }
            if(radial.Pressed)return;
            if(e.type==EventType.MouseDown&&e.button==0&&progressRect.Contains(e.mousePosition))
            {progressDragging=true;GUIUtility.hotControl=progressControlId;transport.Pause();SeekProgress(e.mousePosition.x,end);e.Use();}
            else if(progressDragging&&e.type==EventType.MouseDrag&&e.button==0){SeekProgress(e.mousePosition.x,end);e.Use();}
            else if(progressDragging&&e.rawType==EventType.MouseUp&&e.button==0)
            {SeekProgress(e.mousePosition.x,end);progressDragging=false;if(GUIUtility.hotControl==progressControlId)GUIUtility.hotControl=0;e.Use();}
        }
        private void SeekProgress(float x,double end)
        {
            // Deliberately NOT CancelGesture: floating paste/range/point drafts survive scrubbing.
            double t=Mathf.Clamp01((x-progressRect.x)/Math.Max(1,progressRect.width))*end;
            transport.Seek(showGrid?SnapTime(t):AuthoringGrid.RoundMs(t));timeText=CurrentMs.ToString("0",CI);
        }
        private void DrawDifficultyTabs()
        {
            if(!sideSkinsLoaded){sideSkinsLoaded=true;for(int i=0;i<4;i++)sideDifficultySkins[i]=Resources.Load<Texture2D>("InFalsusStudio/Hud/SideDifficulty"+i);}
            int active=guiSongIndex;for(int i=0;i<4;i++)if(i!=active)DrawDifficultyTab(i,false);
            if(active>=0&&active<4)DrawDifficultyTab(active,true);
        }
        private void DrawDifficultyTab(int i,bool active)
        {
            float w=active?78:64,h=active?91:77;
            Rect r=HR(1908-w,179+i*82-(active?5:0),w,h);
            Color[] colors={new Color(.05f,.65f,.9f),new Color(.02f,.83f,.81f),new Color(.66f,.36f,.96f),new Color(1,.22f,.35f)};
            if(Event.current.type==EventType.Repaint)
            {
                HudFill(r,new Color(.02f,.04f,.06f,.82f));
                if(sideDifficultySkins[i]!=null)GUI.DrawTexture(r,sideDifficultySkins[i],ScaleMode.StretchToFill,true);
                else HudFill(new Rect(r.x,r.y,5*hudScale,r.height),colors[i]);
                HudLabel(new Rect(r.x+5*hudScale,r.y+9*hudScale,r.width-10*hudScale,21*hudScale),SongFolderProject.Codes[i],active?17:14,colors[i],TextAnchor.MiddleCenter);
                string rating=songProject==null?"—":songProject.Difficulties[i].Rating;
                HudLabel(new Rect(r.x+5*hudScale,r.y+32*hudScale,r.width-10*hudScale,28*hudScale),rating,active?22:19,Color.white,TextAnchor.MiddleCenter);
                if(active)HudLabel(new Rect(r.x,r.yMax-20*hudScale,r.width,17*hudScale),L("当前","ACTIVE"),11,Color.white,TextAnchor.MiddleCenter);
            }
            bool old=GUI.enabled;
            Rect front=guiSongIndex>=0?HR(1830,179+guiSongIndex*82-5,78,91):new Rect();
            GUI.enabled=old&&(active||!front.Contains(Event.current.mousePosition));
            bool pressed=GUI.Button(r,new GUIContent("",SongFolderProject.Names[i]),GUIStyle.none);GUI.enabled=old;
            if(pressed&&songProject!=null){int index=i;QueueGui(()=>{CancelGesture();ActivateSongDifficulty(index);});}
        }
        private void RequestExit07()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>exitDeadline){exitDeadline=now+1;status=L("1 秒内再点一次退出；未保存内容会丢弃。","Click Exit again within 1 second. Unsaved changes will be discarded.");return;}
            CancelGesture();CancelRadial();transport.Pause();if(keySounds!=null)keySounds.StopAll();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private bool IconButton(Rect r,string icon,string text,bool active=false)
        {
            Color old=GUI.backgroundColor;if(active)GUI.backgroundColor=new Color(.42f,.76f,.82f);
            bool pressed=GUI.Button(r,new GUIContent("",text));GUI.backgroundColor=old;
            if(Event.current.type==EventType.Repaint)
            {DrawSmallIcon(new Rect(r.x+6*hudScale,r.center.y-9*hudScale,18*hudScale,18*hudScale),icon,new Color(.72f,.94f,1));HudLabel(new Rect(r.x+29*hudScale,r.y,r.width-32*hudScale,r.height),text,12,Color.white);}
            return pressed;
        }
        private void DrawSmallIcon(Rect r,string name,Color c)
        {
            Action<float,float,float,float> line=(x0,y0,x1,y1)=>GuiLine(r.x+x0*r.width,r.y+y0*r.height,r.x+x1*r.width,r.y+y1*r.height,1.6f*hudScale,c);
            if(name=="play"){line(.22f,.12f,.85f,.5f);line(.85f,.5f,.22f,.88f);line(.22f,.88f,.22f,.12f);}
            else if(name=="pause"){HudFill(new Rect(r.x+r.width*.2f,r.y,r.width*.2f,r.height),c);HudFill(new Rect(r.x+r.width*.62f,r.y,r.width*.2f,r.height),c);}
            else if(name=="save"){line(.1f,.1f,.9f,.1f);line(.9f,.1f,.9f,.9f);line(.9f,.9f,.1f,.9f);line(.1f,.9f,.1f,.1f);line(.25f,.1f,.25f,.4f);line(.25f,.4f,.72f,.4f);line(.3f,.9f,.3f,.62f);line(.3f,.62f,.7f,.62f);}
            else if(name=="undo"||name=="redo"){float sign=name=="undo"?1:-1;Func<float,float> xx=x=>sign==1?x:1-x;line(xx(.8f),.8f,xx(.8f),.35f);line(xx(.8f),.35f,xx(.12f),.35f);line(xx(.12f),.35f,xx(.4f),.1f);line(xx(.12f),.35f,xx(.4f),.6f);}
            else if(name=="grid"||name=="range"){for(int i=0;i<3;i++){float u=.1f+.4f*i;line(.1f,u,.9f,u);if(name=="grid")line(u,.1f,u,.9f);}if(name=="range"){line(.1f,.1f,.1f,.9f);line(.9f,.1f,.9f,.9f);}}
            else if(name=="open"){line(.1f,.35f,.1f,.9f);line(.1f,.9f,.9f,.9f);line(.9f,.9f,.9f,.35f);line(.9f,.35f,.1f,.35f);line(.1f,.35f,.1f,.15f);line(.1f,.15f,.4f,.15f);line(.4f,.15f,.55f,.35f);}
            else if(name=="auto"){line(.55f,.05f,.2f,.55f);line(.2f,.55f,.58f,.55f);line(.58f,.55f,.45f,.95f);line(.45f,.95f,.85f,.4f);line(.85f,.4f,.52f,.4f);}
            else if(name=="exit"){line(.1f,.1f,.1f,.9f);line(.1f,.9f,.5f,.9f);line(.1f,.1f,.5f,.1f);line(.4f,.5f,.95f,.5f);line(.95f,.5f,.7f,.25f);line(.95f,.5f,.7f,.75f);}
            else if(name=="settings"){for(int i=0;i<3;i++){float y=.2f+.3f*i;line(.1f,y,.9f,y);float x=i==1?.3f:.65f;line(x,y-.12f,x,y+.12f);}}
            else if(name=="wave"){for(int i=0;i<7;i++){float x=.1f+i*.13f,h=.1f+.32f*Mathf.Abs(Mathf.Sin(i*2));line(x,.5f-h,x,.5f+h);}}
            else {line(.5f,.08f,.5f,.35f);line(.5f,.5f,.5f,.7f);line(.2f,.9f,.8f,.9f);}
        }
        private static void GuiLine(float x0,float y0,float x1,float y1,float width,Color color)
        {
            Matrix4x4 old=GUI.matrix;Color prior=GUI.color;Vector2 delta=new Vector2(x1-x0,y1-y0);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,new Vector2(x0,y0));GUI.color=color;
            GUI.DrawTexture(new Rect(x0,y0-width*.5f,delta.magnitude,width),Texture2D.whiteTexture);GUI.matrix=old;GUI.color=prior;
        }
        public string RuntimeReport07()
        {
            return "InFalsus Studio 0.9\nUnity "+Application.unityVersion+"\nMusic priority="+transport.MusicPriority+", virtual="+transport.MusicVirtual+", playing="+transport.MusicPlaying+
                "\nReal voices="+keySounds.RealVoiceLimit+", effects shots="+keySounds.ShotBudget+", hold reservations="+keySounds.HoldBudget+
                "\nEffects dropped (capacity / late)="+keySounds.CapacityDropped+" / "+keySounds.LateDropped+
                "\nCurrent subdivision="+Division+", Alt unlocked="+EditHeld+", selection="+selection.Count+
                "\nFloating="+(floating!=null)+", range="+rangeActive+", source untouched until drop/explicit Save.";
        }
        private void DrawHelp07()
        {
            GUILayout.BeginArea(HelpRect,GUI.skin.box);BeginPanelContent(ref helpScroll,HelpRect);
            GUILayout.Label("InFalsus Studio 0.9 · "+L("操作帮助","Help"));
            if(GUILayout.Button(chinese?"English":"中文"))QueueGui(()=>chinese=!chinese);
            GUILayout.Label(L(
                "右键立即出菜单，移到按钮松开右键确认；类型子菜单悬停0.5秒。\nCtrl点击增选/取消；混合类型不弹参数面板。同类型可批量改值。\nAlt按住才可拖主体和把手。参数选项点击立即改内存；数字只在回车或点击外部失焦时应用。\n段选：点击开始，再点击结束；Hold/Sky以结束时间纳入。\nCtrl+C复制 / Ctrl+X剪贴：浮动预览，点击网格放下。滚轮和进度条不会取消预览。\n右键按下或Esc取消当前浮动/段选/未完成放置；不会同时打开菜单。\n有选区时右键有删除、镜像、剪贴、复制；网格开启时还有对齐网格。\n对齐分别处理Hold/Sky首尾；若长度归零，整个操作拒绝。\n滚轮上=下一格，下=上一格。细分输入N表示1/N拍，支持1..1024。\n基准BPM只影响显示倍率，不写入谱面BPM。默认等于首个chart BPM。\n延迟正数=音乐晚开始；保存到工程offset（取负号），不是SPC事件。\nSpace播放/暂停；播放时隐藏编辑控件，进度条仍可定位。\nCtrl+S/保存写全部内存草稿；退出不自动保存。1秒内二次点击退出确认。\nF1干净预览；高级SOURCE也用回车或点击外部应用，保留原始精度。",
                "RMB opens immediately; release RMB ON a button. Type submenu dwell: 0.5s.\nCtrl-click toggles selection. Same-type selections share a live batch panel; mixed types have no panel.\nHold Alt to drag bodies/handles. Choices apply immediately in memory; numbers commit only on Enter or focus loss.\nRange: start then end. Hold/Sky membership is based on END time.\nCtrl+C/X creates a floating copy/cut. Click a grid line to place. Wheel/progress scrubbing preserves drafts.\nRMB press or Esc cancels drafts without opening a menu. Cut cancellation never changes originals.\nSelection RMB menu: delete/mirror/cut/copy; align-to-grid only with grid enabled.\nAlign snaps both long-note endpoints independently; collapsed spans reject atomically.\nWheel up=next grid, down=previous. Subdivision N means 1/N beat (1..1024).\nReference BPM only normalizes VIEW speed, never writes chart BPM. Default follows the chart header.\nPositive delay starts music later; project audio offset stores its negative.\nSpace plays/pauses; editing chrome hides while playing. The progress bar still works.\nSave/Ctrl+S writes all drafts. Exit never auto-saves. Click Exit twice within 1s.\nF1 clean preview. Raw source commits on Enter or focus loss, preserving precision."));
            if(GUILayout.Button(L("关闭","Close")))QueueGui(()=>showHelp=false);
            EndPanelContent();GUILayout.EndArea();
        }
    }
}
