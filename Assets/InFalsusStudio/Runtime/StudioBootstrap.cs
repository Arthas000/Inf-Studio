using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
#if INFALSUS_USE_UNITY_WEBREQUEST_AUDIO
using UnityEngine.Networking;
#endif
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    // Import this folder into a Unity 6 Universal 3D project. No Input System dependency.
    // This component owns only its generated children; it never rewrites your Packages/ProjectSettings.
    public sealed partial class StudioBootstrap : MonoBehaviour
    {
        public StudioProfile profileAsset;
        public TextAsset initialChart;
        public AudioClip music;
        public float initialTimeMs = 1846;
        public bool initialShowNotes = false;
        public Texture2D referenceImage;
        [NonSerialized] public StudioProfile Profile;
        public Camera ViewCamera {get;private set;}
        public EditHistory History {get;private set;}
        public IList<SpcEvent> Events {get{return events;}}
        public double CurrentMs {get{return transport!=null?transport.TimeMs:initialTimeMs;}}
        private readonly List<SpcEvent> events=new List<SpcEvent>();
        private ScrollTimeline scroll;
        private BeatTimeline beats;
        private StudioMaterials materials;
        private StageRenderer stage;
        private NoteRenderer notes;
        private HitFeedbackRenderer hitFeedback;
        private StudioTransport transport;
        private Transform generated;
        private bool showNotes,hidePast=true,showGrid=true,showUi=true,showInspector,showCalibration,overlay;
        private float overlayAlpha=.40f;
        private int selectedId=-1,pickFilter,brushLane=1,brushWidth=1;
        private string[] fieldValues=new string[0];
        private Vector2 inspectorScroll,calibrationScroll;
        private string timeText="1846",pathText="",offsetText="0",status="";
        private string loadedPath="";
        private double durationMs=120000;
        private bool initialized,geometryDirty=true,documentDirty;
        private int lastW,lastH,audioRequest;
        private AudioClip ownedAudioClip;
        private Texture2D ownedReference;
        private Rect topRect,bottomRect,inspectorRect,calibrationRect;
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;

        private void Start()
        {
            try { Initialize(); }
            catch(Exception ex){status="INITIALIZATION FAILED: "+ex.Message;Debug.LogException(ex,this);}
        }
        public void Initialize()
        {
            if(initialized)return;
            // This demo uses a canonical world root, not a transformed child of another scene rig.
            transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);transform.localScale=Vector3.one;
            Profile=profileAsset!=null?Instantiate(profileAsset):ScriptableObject.CreateInstance<StudioProfile>();Profile.Validate();Profile.scrollMultiplier=SnapScroll(Profile.scrollMultiplier);canonicalUnitsPerSecond=Profile.unitsPerSecondAtSpeedOne;
            var root=new GameObject("Generated - do not hand-rotate the side lanes");root.transform.SetParent(transform,false);generated=root.transform;
            var cameraObject=new GameObject("Calibrated gameplay camera",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(generated,false);
            ViewCamera=cameraObject.GetComponent<Camera>();ViewCamera.cullingMask=1<<30;ViewCamera.depth=10;
            StageSpace.ApplyCamera(ViewCamera,Profile);
            var audio=gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;transport=new StudioTransport(audio);transport.SetClip(music);
            keySounds=new StudioKeySounds(generated,transport);
            materials=new StudioMaterials(Profile);stage=new StageRenderer(generated,Profile,materials);notes=new NoteRenderer(generated,Profile,materials,ViewCamera);ghostRenderer=new NoteRenderer(generated,Profile,materials,ViewCamera,true);hitFeedback=new HitFeedbackRenderer(generated,Profile,materials,ViewCamera);
            TextAsset fallback=initialChart!=null?initialChart:Resources.Load<TextAsset>("InFalsusStudio/LightsOut.spc");
            LoadDocument(fallback!=null?SpcDocument.FromBytes(fallback.bytes):SpcDocument.Parse("chart(130,4)\n"));
            showNotes=initialShowNotes;transport.Seek(initialTimeMs);timeText=initialTimeMs.ToString("0.###",CI);
            InitializePresentation();geometryDirty=false;initialized=true;status="Ready 0.9. Open folder to load a song project. Press RMB for instant menus; Alt unlocks dragging. Point placement uses clicks. F1 hides UI.";
        }
        private void LateUpdate()
        {
            if(!initialized)return;
            if(Screen.width!=lastW||Screen.height!=lastH||geometryDirty){StageSpace.ApplyCamera(ViewCamera,Profile);lastW=Screen.width;lastH=Screen.height;}
            if(geometryDirty)
            {
                stage.Dispose();notes.Dispose();if(ghostRenderer!=null)ghostRenderer.Dispose();if(hitFeedback!=null)hitFeedback.Dispose();materials.Dispose();
                materials=new StudioMaterials(Profile);stage=new StageRenderer(generated,Profile,materials);notes=new NoteRenderer(generated,Profile,materials,ViewCamera);ghostRenderer=new NoteRenderer(generated,Profile,materials,ViewCamera,true);hitFeedback=new HitFeedbackRenderer(generated,Profile,materials,ViewCamera);geometryDirty=false;notes.UpdateConnections(events);hitFeedback.Rebuild(events);
            }
            TickSession();TickEditing();TickPresentation();
            if(transport.Playing&&transport.TimeMs>durationMs)transport.Pause();
            EnsureBrowseGridLock();
            if(keySounds!=null){keySounds.AutoPlayEnabled=autoCursor;keySounds.Tick(loopEnabled&&loopB>loopA+.001?loopB:double.PositiveInfinity);} // Uses the music DSP anchor, not rendered frames or combo ticks.
            // Editor browsing is snapped; active audio playback is NEVER quantized.
            // Only visible note meshes are rebuilt. This is intentionally simple for the prototype.
            cursorPose=autoCursor&&showNotes?cursorMotion.Evaluate(transport.TimeMs):new CursorPose(.5,-1,"Idle");
            notes.ShowGroundProjections=showAirShadows;
            notes.Draw(events,EffectiveScroll,beats,transport.TimeMs,showNotes,hidePast&&autoCursor,showGrid&&!transport.Playing,transport.Playing?-1:selectedId,(float)cursorPose.X,transport.Playing?null:selection,authoringGrid,Division,autoCursor?cursorMotion:null,showMeasureLines,true,floatingHidden);
            if(ghostRenderer!=null)ghostRenderer.Draw(ghostEvents,EffectiveScroll,beats,transport.TimeMs,floating!=null&&floatingValid&&!transport.Playing,false,false,-1,.5f,null,authoringGrid,Division,null,false,false);
            if(hitFeedback!=null)hitFeedback.Draw(events,transport.TimeMs,showFeedback&&autoCursor&&showNotes&&transport.Playing,cursorMotion,transport.PlaybackStartMs);
        }
        public void RebuildGeometry(){if(Profile!=null)Profile.Validate();geometryDirty=true;}
        private void LoadDocument(SpcDocument document)
        {
            var parsed=document.ReadEvents();
            if(!parsed.Any(e=>e.Kind==EventKind.Chart))throw new InvalidDataException("The SPC has no valid chart(...) header.");
            viewReferenceBpm=0;
            var header=parsed.First(n=>n.Kind==EventKind.Chart);timingBpm=header.Get(0).ToString("0.##########",CI);timingMeter=header.Get(1).ToString("0.##########",CI);
            History=new EditHistory(document);History.BeforeCommit=SkyGroups.NormalizeAuthoredChanges;ClearInputDrafts();documentDirty=false;ResetEditingForDocument();RefreshDocument();selectedId=-1;fieldValues=new string[0];
        }
        private void RefreshDocument()
        {
            events.Clear();events.AddRange(History.Document.ReadEvents());scroll=new ScrollTimeline(events);beats=new BeatTimeline(events);authoringGrid=new AuthoringGrid(beats);
            durationMs=Math.Max(10000,events.Where(e=>e.IsNote).Select(e=>e.EndMs).DefaultIfEmpty(0).Max()+3000);
            if(transport!=null&&transport.Clip!=null)durationMs=Math.Max(durationMs,transport.Clip.length*1000.0-transport.AudioOffsetMs);
            RefreshSongDerivedState();RebuildKeySoundPlan();ApplyViewNormalization();if(notes!=null)notes.UpdateConnections(events);if(hitFeedback!=null)hitFeedback.Rebuild(events);
        }
        private void Preset(double time,float speed=1)
        {CancelGesture();transport.Pause();transport.Seek(time);timeText=time.ToString("0.###",CI);Profile.scrollMultiplier=speed;showNotes=true;showCalibration=false;SetSelection(new int[0]);showInspector=false;tool=Tool.Select;ResetPointDraft();}
        private void Select(int id)
        {SetSelection(id>=0?new[]{id}:new int[0],id);
            var item=events.FirstOrDefault(n=>n.SourceId==id);
            if(item!=null&&!item.IsNote){showInspector=true;showSongPanel=false;}}
        private bool Change(Action<SpcDocument> action)
        {
            try{History.Execute(action);AfterDocumentEdit();status="Edited in memory. Ctrl+S saves a working copy.";return true;}
            catch(Exception ex){status=ex.Message;return false;}
        }
        private void Undo(bool redo)
        {
            CancelGesture();
            if(redo?History.Redo():History.Undo()){AfterDocumentEdit();status=redo?"Redo":"Undo";}
        }
        private void SeekText()
        {double value;if(double.TryParse(timeText,NumberStyles.Float,CI,out value)&&!double.IsInfinity(value)&&!double.IsNaN(value))transport.Seek(Math.Max(0,value));else status="Time must be finite milliseconds.";}
        private void OnGUI()
        {
            if(!initialized){GUI.Box(new Rect(20,20,700,90),"InFalsus Studio\n"+status);return;}
            var e=Event.current;HandleWindowRecovery(e);
            if(e.type==EventType.Layout)BeginGuiFrame();
            if(!guiFrameReady)return;
            controlId=GUIUtility.GetControlID("InFalsusStudio.SceneGesture".GetHashCode(),FocusType.Passive);
            radialControlId=GUIUtility.GetControlID("InFalsusStudio.RadialGesture".GetHashCode(),FocusType.Passive);
            if(releaseCapture){if(GUIUtility.hotControl==controlId||GUIUtility.hotControl==radialControlId||GUIUtility.hotControl==progressControlId)GUIUtility.hotControl=0;releaseCapture=false;}
            float bottomHeight=guiShowDetail?206:28;
            LayoutHud();
            var previousSkin=GUI.skin;
            try
            {
            UseStudioSkin();guiScreenOrigin=GUIUtility.GUIToScreenPoint(Vector2.zero);
            bool inputConsumed=HandleCommittedInput(e);
            LayoutWorkspace(bottomHeight);LayoutChrome();
            bool textFocused=hudField!=HudField.None||GUI.GetNameOfFocusedControl().StartsWith("field",StringComparison.Ordinal);
            bool hudKeyCaptured=HandleHudEditingKeys(e);
            ObserveEditorModifiers(e);
            bool menuCaptured=HandleRadialEvent(e);
            if(!menuCaptured&&!hudKeyCaptured&&!inputConsumed)HandleKeyboard(e,textFocused);
            RefreshAuthoringHover(e.mousePosition);
            if(overlay&&referenceImage!=null)
            {
                Color previous=GUI.color;GUI.color=new Color(1,1,1,overlayAlpha);Rect r=ViewCamera.pixelRect;r.y=Screen.height-r.y-r.height;
                GUI.DrawTexture(r,referenceImage,ScaleMode.StretchToFill,true);GUI.color=previous;
            }
            DrawKeyLabels();DrawSongHud();DrawCenterCombo();DrawExternalChrome();
            if(guiShowUi)
            {
                bool oldEnabled=GUI.enabled;
                try
                {
                    GUI.enabled=oldEnabled&&!radial.Pressed;
                    DrawSideDock();if(guiToolsExpanded)DrawToolbar();if(NotePanelVisible)DrawNotePanel();DrawTimeline();DrawDetailTimeline();
                    if(guiShowInspector)DrawInspector();if(guiShowCalibration)DrawCalibration();if(guiShowSongPanel)DrawSongPanel();if(guiShowHelp)DrawHelp07();
                }
                finally{GUI.enabled=oldEnabled;}
            }
            if(guiShowUi){DrawEditingOverlay();DrawWorkflowOverlay();}
            if(!menuCaptured&&!inputConsumed){HandleDetailTimeline(e);HandleSceneEvent(e);}
            DrawRadialOverlay();
            }
            finally{GUI.skin=previousSkin;}
        }
        private void DrawToolbar(){DrawToolDrawer();}
        private void DrawTimeline()
        {
            Rect r=new Rect(bottomRect.x,bottomRect.y,bottomRect.width,24);
            HudFill(r,new Color(.015f,.03f,.04f,.72f));
            HudLabel(new Rect(r.x+8,r.y,r.width-16,r.height),status,12,new Color(.73f,.88f,.92f));
        }
        private void DrawInspector(){DrawTimingBrowser();}
        private void AddNote(EventKind kind,int flickDirection=16)
        {
            double t=SnapTime(CurrentMs);int w=GroundBrush(kind,brushLane),id=-1,startLane=brushLane;
            if(kind==EventKind.Tap||kind==EventKind.Hold)
            {
                int resolvedLane,resolvedWidth;
                if(!GroundPlacement.TryResolve(brushLane,w,out resolvedLane,out resolvedWidth)){status="Invalid ground placement brush.";return;}
                startLane=resolvedLane;w=resolvedWidth;
            }
            double center=airGrid.Snap((Math.Max(1,Math.Min(4,brushLane))-1+.5)/4.0);
            if(Change(d=>{id=EditOperations.Add(d,kind,t,DefaultEnd(t),startLane,w,center,center,(kind==EventKind.SkyArea?skyWidthPercent:NewFlickWidthPercent)/100.0,flickDirection,100);
                if(kind==EventKind.SkyArea){EditOperations.SetEase(d,new[]{id},false,skyLeftEase);EditOperations.SetEase(d,new[]{id},true,skyRightEase);}}))
            {Select(id);showNotes=true;}
        }
        private void DrawCalibration()
        {
            GUILayout.BeginArea(calibrationRect,GUI.skin.box);BeginPanelContent(ref calibrationScroll,calibrationRect);
            GUILayout.Label("CALIBRATION  /  reference 2048 x 1152");
            GUILayout.Label("FOV 50 degrees is a chosen model gauge.\nThe camera is not claimed to be the game's original camera.");
            float fov=Slider("Vertical FOV",Profile.verticalFov,30,75),pitch=Slider("Pitch down",Profile.cameraPitch,10,40),height=Slider("Camera height",Profile.cameraHeight,1,5),back=Slider("Camera back",Profile.cameraBack,1,6);
            float rise=Slider("Side rise",Profile.sideRise,0,1.8f),run=Slider("Side run",Profile.sideRun,.2f,1.8f),sky=Slider("Sky height",Profile.skyHeight,.2f,2.3f),flick=Slider("Flick fixed height (width-independent)",Profile.flickHeightUnits,.05f,.8f),skew=Slider("Flick diagonal cap skew",Profile.flickCapSkew,0,1.5f);
            if(fov!=Profile.verticalFov||pitch!=Profile.cameraPitch||height!=Profile.cameraHeight||back!=Profile.cameraBack||rise!=Profile.sideRise||run!=Profile.sideRun||sky!=Profile.skyHeight||flick!=Profile.flickHeightUnits||skew!=Profile.flickCapSkew)
            {Profile.verticalFov=fov;Profile.cameraPitch=pitch;Profile.cameraHeight=height;Profile.cameraBack=back;Profile.sideRise=rise;Profile.sideRun=run;Profile.skyHeight=sky;Profile.flickHeightUnits=flick;Profile.flickCapSkew=skew;RebuildGeometry();}
            if(GUILayout.Button("Restore image-calibrated defaults"))
            {
                var reset=ScriptableObject.CreateInstance<StudioProfile>();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(reset),Profile);Destroy(reset);RebuildGeometry();
            }
            if(GUILayout.Button("Load calibration JSON..."))QueueGui(ChooseProfile);
            if(GUILayout.Button("Write calibration JSON to persistent data"))
            {string path=Path.Combine(Application.persistentDataPath,"infalsus-camera-profile.json");File.WriteAllText(path,JsonUtility.ToJson(Profile,true));status="Profile saved: "+path;}
            GUILayout.Space(10);if(GUILayout.Button("Load reference PNG overlay..."))QueueGui(ChooseReference);
            overlay=GUILayout.Toggle(overlay,"Reference overlay (F1 hides controls only)");overlayAlpha=Slider("Reference alpha",overlayAlpha,0,1);
            GUILayout.Space(10);offsetText=CommittedRow(L("音频偏移 ms（audio = chart + offset）","Audio offset ms"),"field_audiooffset",transport.AudioOffsetMs.ToString("0.###",CI),v=>{transport.SetAudioOffset(Finite(v));SyncActiveSongDraft();RefreshProjectDirty();RefreshDocument();});
            GUILayout.Label("Video is a reference at SPEED 4.50.\nScreenshots are SPEED 1.00. Do not compare spacing without matching this setting.");
            DrawNavigationOptions();
            GUILayout.Space(10);GUILayout.Label("Standalone: enter a full local file path");pathText=CommittedRow("Local path","field_local_path",pathText,v=>pathText=v);
            GUILayout.BeginHorizontal();if(GUILayout.Button("Load SPC path")){string path=pathText;QueueGui(()=>LoadChartPath(path));}if(GUILayout.Button("Load audio path")){string path=pathText;QueueGui(()=>StartCoroutine(LoadAudio(path)));}GUILayout.EndHorizontal();
            EndPanelContent();GUILayout.EndArea();
        }
        private static float Slider(string name,float value,float min,float max)
        {GUILayout.Label(name+": "+value.ToString("0.####",CI));return GUILayout.HorizontalSlider(value,min,max);}
        private void DrawKeyLabels()
        {
            string[] keys={"Shift","A","S","D","F","Space"};Color old=GUI.color;GUI.color=new Color(.64f,.97f,1,.86f);
            var style=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=Mathf.Clamp(Screen.height/42,14,29)};
            for(int i=0;i<6;i++)
            {
                Vector3 s=ViewCamera.WorldToScreenPoint(StageSpace.LanePoint(Profile,i,.5f,.18f,.02f));if(s.z<=0)continue;
                Matrix4x4 matrix=GUI.matrix;Vector2 center=new Vector2(s.x,Screen.height-s.y);
                if(i==0||i==5)GUIUtility.RotateAroundPivot(i==0?32:-32,center);
                GUI.Label(new Rect(center.x-70,center.y-20,140,40),keys[i],style);GUI.matrix=matrix;
            }
            GUI.color=old;
        }
        public void LoadChartPath(string path)
        {
            try
            {
                var d=SpcDocument.Load(path);if(!d.ReadEvents().Any(x=>x.Kind==EventKind.Chart))throw new InvalidDataException("No valid plaintext chart header. For binary ICP1 + decoded JSON use Open folder instead.");
                CancelGesture();if(!LeaveCurrentDocumentSafely())return;
                d=SpcDocument.Load(path);if(!d.ReadEvents().Any(x=>x.Kind==EventKind.Chart))throw new InvalidDataException("Chart changed during opening; valid header required.");
                DetachSongProject();transport.Pause();LoadDocument(d);loadedPath=Path.GetFullPath(path);
                transport.Seek(0);showNotes=true;status="Loaded "+Path.GetFileName(path)+"; "+events.Count(x=>x.IsNote)+" objects (not combo). Original not modified.";LoadSessionSidecar(loadedPath);
            }
            catch(ExitGUIException){throw;}
            catch(Exception ex){status="Load failed: "+ex.Message;}
        }
        private void ChooseChart()
        {string path=StudioFileDialogs.Open("Open plaintext SPC","","spc");if(path.Length>0)LoadChartPath(path);}
        private void ChooseSave()
        {string path=StudioFileDialogs.Save("Export a separate SPC copy","","chart_edited.spc","spc");if(path.Length>0)SaveCopyTo(path);}
        private void ChooseAudio()
        {
            if(songProject!=null){status="Use Open folder for another song. The project already has validated main music.";return;}
            string path=StudioFileDialogs.Open("Open local WAV / OGG / MP3","","");
            if(path.Length>0)StartCoroutine(LoadAudio(path));
        }
        private IEnumerator LoadAudio(string path)
        {
            // WAV works without UnityWebRequest. In Editor, OGG/MP3 use Unity's normal importer.
            // No package manifests or Player Settings are rewritten by this code.
            int requestId=++audioRequest; AudioClip local=null;bool owned=false;string full="",audioError=null;
            try
            {
                full=Path.GetFullPath(path);if(!File.Exists(full))throw new IOException("Audio file does not exist.");
                string ext=Path.GetExtension(full).ToLowerInvariant();
                if(ext==".wav")
                {
                    var info=new FileInfo(full);if(info.Length>256*1024*1024)throw new IOException("WAV exceeds 256 MiB safety limit.");
                    var data=WaveReader.Decode(File.ReadAllBytes(full));
                    local=AudioClip.Create(Path.GetFileNameWithoutExtension(full),data.Frames,data.Channels,data.Frequency,false);
                    if(!local.SetData(data.Samples,0)){Destroy(local);throw new IOException("AudioClip.SetData failed.");}owned=true;
                }
#if UNITY_EDITOR
                else if(ext==".mp3"||ext==".ogg")
                {
                    // User-selected local audio is copied into a dedicated USER folder outside this
                    // package. Updating Assets/InFalsusStudio will not erase their imported music.
                    string folder="Assets/InFalsusStudioUser/Audio";Directory.CreateDirectory(folder);
                    string name=Path.GetFileNameWithoutExtension(full);foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');
                    string assetsRoot=Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
                    string dest;
                    if(full.StartsWith(assetsRoot,StringComparison.OrdinalIgnoreCase))
                        dest="Assets/"+full.Substring(assetsRoot.Length).Replace('\\','/');
                    else
                    {
                        dest=UnityEditor.AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+ext);
                        File.Copy(full,dest,false);
                    }
                    UnityEditor.AssetDatabase.ImportAsset(dest,UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                    var importer=UnityEditor.AssetImporter.GetAtPath(dest) as UnityEditor.AudioImporter;
                    if(importer!=null){var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;importer.defaultSampleSettings=settings;importer.SaveAndReimport();}
                    local=UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(dest);
                    if(local==null)throw new IOException("Unity could not import that audio file.");
                    full=Path.GetFullPath(dest); // Reopening the session reuses this imported clip, not another copy.
                }
#endif
            }
            catch(Exception ex){audioError=ex.Message;}
            if(audioError!=null){status="Audio load failed: "+audioError;yield break;}
            if(local!=null){InstallAudioClip(local,full,owned);yield break;}
#if INFALSUS_USE_UNITY_WEBREQUEST_AUDIO
            string extension=Path.GetExtension(full).ToLowerInvariant();
            AudioType type=extension==".ogg"?AudioType.OGGVORBIS:extension==".mp3"?AudioType.MPEG:AudioType.UNKNOWN;
            using(var req=UnityWebRequestMultimedia.GetAudioClip(new Uri(full).AbsoluteUri,type))
            {
                yield return req.SendWebRequest();if(requestId!=audioRequest)yield break;
                if(req.result!=UnityWebRequest.Result.Success){status="Audio load failed: "+req.error;yield break;}
                InstallAudioClip(DownloadHandlerAudioClip.GetContent(req),full,true);
            }
#else
            status="Use PCM/float WAV for standalone loading, or import OGG/MP3 with the Unity Editor. No WebRequest modules required for WAV.";
            yield break;
#endif
        }
        private void ChooseProfile()
        {
            string path=StudioFileDialogs.Open("Load calibration JSON","","json");if(path.Length==0)return;
            try{JsonUtility.FromJsonOverwrite(File.ReadAllText(path),Profile);RebuildGeometry();status="Loaded calibration into memory.";}
            catch(Exception ex){status="Profile load failed: "+ex.Message;}
        }
        private void ChooseReference()
        {
            string path=StudioFileDialogs.Open("Choose reference screenshot","","png");if(path.Length==0)return;
            try{var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);if(!ImageConversion.LoadImage(tex,File.ReadAllBytes(path)))throw new IOException("Unsupported image.");if(ownedReference!=null)Destroy(ownedReference);ownedReference=tex;referenceImage=tex;overlay=true;showNotes=false;}
            catch(Exception ex){status=ex.Message;}
        }
        private void OnDisable()
        {
            if(keySounds!=null)keySounds.StopAll();
            if(transport!=null&&transport.Playing)transport.Pause();
        }
        private void OnDestroy()
        {
            CancelGesture();DisposePresentation();
            if(keySounds!=null)keySounds.Dispose();
            if(transport!=null)transport.Pause();if(stage!=null)stage.Dispose();if(notes!=null)notes.Dispose();if(ghostRenderer!=null)ghostRenderer.Dispose();if(hitFeedback!=null)hitFeedback.Dispose();if(materials!=null)materials.Dispose();
            if(studioSkin!=null)Destroy(studioSkin);if(radialFont!=null)Destroy(radialFont);if(Profile!=null)Destroy(Profile);if(ownedAudioClip!=null)Destroy(ownedAudioClip);if(ownedReference!=null)Destroy(ownedReference);
            if(hudFont!=null)Destroy(hudFont);if(songJacket!=null)Destroy(songJacket);
        }
    }
}
