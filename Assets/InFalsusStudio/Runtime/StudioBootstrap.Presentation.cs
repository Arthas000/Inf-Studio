using System;
using System.IO;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        [Serializable]private sealed class PresentationPreferences
        {
            public int version=1,width=1600,height=900,windowMode;
            public string background="";
            public bool backgroundEnabled,stretch;
            public float brightness=1;
        }
        private PresentationPreferences presentation=new PresentationPreferences();
        private StudioBackground backdrop;
        private StudioWindow studioWindow;
        private Texture2D ownedBackground;
        private bool presentationDirty;
        private string presentationMessage="";
        private string PreferencesPath {get{return Path.Combine(Application.persistentDataPath,"presentation.v09.json");}}
        private void InitializePresentation()
        {
            backdrop=new StudioBackground(ViewCamera);studioWindow=gameObject.AddComponent<StudioWindow>();
            try
            {
                if(File.Exists(PreferencesPath)&&new FileInfo(PreferencesPath).Length<65536)
                {
                    var loaded=JsonUtility.FromJson<PresentationPreferences>(File.ReadAllText(PreferencesPath));
                    if(loaded!=null&&loaded.version==1&&loaded.width>=960&&loaded.height>=540&&loaded.width<=7680&&loaded.height<=4320&&loaded.windowMode>=0&&loaded.windowMode<=3)presentation=loaded;
                }
                if(!string.IsNullOrEmpty(presentation.background))SetBackground(presentation.background,false);
                backdrop.Enabled=presentation.backgroundEnabled;backdrop.Stretch=presentation.stretch;if(float.IsNaN(presentation.brightness)||float.IsInfinity(presentation.brightness))presentation.brightness=1;
                backdrop.Brightness=Mathf.Clamp(presentation.brightness,0,2);
#if !UNITY_EDITOR
                studioWindow.Apply(presentation.width,presentation.height,(StudioWindowMode)presentation.windowMode);
#endif
            }
            catch(Exception ex){presentationMessage="Presentation preferences: "+ex.Message;Debug.LogWarning(presentationMessage,this);}
            presentationDirty=false;
        }
        private void SetBackground(string path,bool changed=true)
        {
            Texture2D next=null;bool owned=false;
            try
            {
                if(path=="res:Corridor")
                    next=Resources.Load<Texture2D>("InFalsusStudio/Backgrounds/Corridor");
                else if(!string.IsNullOrEmpty(path))
                {
                    string full=Path.GetFullPath(path);var info=new FileInfo(full);string ext=info.Extension.ToLowerInvariant();
                    if(ext!=".png"&&ext!=".jpg"&&ext!=".jpeg")throw new IOException("Background must be PNG or JPEG.");
                    if(!info.Exists||info.Length<1||info.Length>32L*1024*1024)throw new IOException("Background missing or over 32 MiB.");
                    // Decode only local image bytes. No material/script/code is loaded from the picker.
                    byte[] data=File.ReadAllBytes(full);LocalImageSize.Validate(data,8192,33554432);
                    next=new Texture2D(2,2,TextureFormat.RGBA32,false);owned=true;
                    if(!ImageConversion.LoadImage(next,data)||next.width>8192||next.height>8192)throw new IOException("Invalid image or dimensions over 8192.");
                    next.wrapMode=TextureWrapMode.Clamp;next.filterMode=FilterMode.Bilinear;path=full;
                }
                if(!string.IsNullOrEmpty(path)&&next==null)throw new IOException("Background texture could not be loaded.");
                if(ownedBackground!=null)Destroy(ownedBackground);ownedBackground=owned?next:null;
                backdrop.SetTexture(next);presentation.background=path??"";
                if(changed){presentation.backgroundEnabled=next!=null;presentationDirty=true;}
                backdrop.Enabled=presentation.backgroundEnabled;presentationMessage=next!=null?L("背景已应用到视图；全局保存后记住。","Background applied to view; use Save to remember it."):L("背景已清除。","Background cleared.");
            }
            catch{if(owned&&next!=null)Destroy(next);throw;}
        }
        private void ChooseBackground()
        {
            string path=StudioFileDialogs.Open(L("选择背景 PNG / JPG","Choose background PNG / JPG"),"","");
            if(path.Length>0)SetBackground(path);
        }
        private void ApplyWindow(int w,int h,StudioWindowMode mode)
        {
            if(w<960||h<540||w>7680||h>4320)throw new ArgumentOutOfRangeException("size",L("宽 960～7680，高 540～4320。","Width 960..7680, height 540..4320."));
            presentation.width=w;presentation.height=h;presentation.windowMode=(int)mode;presentationDirty=true;
            studioWindow.Apply(w,h,mode);
        }
        private void DrawPresentationOptions()
        {
            GUILayout.Space(10);GUILayout.Label(L("背景 / 窗口","Background / window"));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(L("选择背景…","Choose image…")))QueueGui(ChooseBackground);
            if(GUILayout.Button(L("附件示例","Included sample")))QueueGui(()=>SetBackground("res:Corridor"));
            if(GUILayout.Button(L("清除","Clear")))QueueGui(()=>SetBackground(""));
            GUILayout.EndHorizontal();
            bool enabled=GUILayout.Toggle(presentation.backgroundEnabled,L("显示背景（不修改轨道或相机）","Show background (no camera/track changes)"));
            if(enabled!=presentation.backgroundEnabled){presentation.backgroundEnabled=enabled;backdrop.Enabled=enabled;presentationDirty=true;}
            bool stretch=GUILayout.Toggle(presentation.stretch,L("拉伸完整图片；关闭则等比铺满并居中裁边","Stretch full image; off = centered aspect-preserving cover"));
            if(stretch!=presentation.stretch){presentation.stretch=stretch;backdrop.Stretch=stretch;presentationDirty=true;}
            GUILayout.Label(L("背景亮度 ","Background brightness ")+presentation.brightness.ToString("0.00",CI));
            float light=GUILayout.HorizontalSlider(presentation.brightness,0,1.5f);
            if(Math.Abs(light-presentation.brightness)>.0001){presentation.brightness=light;backdrop.Brightness=light;presentationDirty=true;}
            GUILayout.Label(string.IsNullOrEmpty(presentation.background)?L("未选择背景。","No background selected."):presentation.background=="res:Corridor"?"4615631595253104_ordirehv_bg.png":Path.GetFileName(presentation.background));
            GUILayout.Space(5);GUILayout.Label(L("独立 exe 的窗口尺寸 / 显示模式","Standalone exe window size / display mode"));
            GUILayout.BeginHorizontal();WindowPreset(1280,720,"720p");WindowPreset(1920,1080,"1080p");GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();WindowPreset(2560,1440,"1440p / 2K");WindowPreset(3840,2160,"2160p / 4K");GUILayout.EndHorizontal();
            CommittedRow(L("宽（像素）","Width (px)"),"field_window_width",presentation.width.ToString(CI),v=>{int n;if(!int.TryParse(v,out n))throw new FormatException("Integer width required.");ApplyWindow(n,presentation.height,(StudioWindowMode)presentation.windowMode);});
            CommittedRow(L("高（像素）","Height (px)"),"field_window_height",presentation.height.ToString(CI),v=>{int n;if(!int.TryParse(v,out n))throw new FormatException("Integer height required.");ApplyWindow(presentation.width,n,(StudioWindowMode)presentation.windowMode);});
            GUILayout.BeginHorizontal();WindowModeButton(StudioWindowMode.Windowed,L("普通窗口","Windowed"));WindowModeButton(StudioWindowMode.BorderlessWindow,L("无边框窗口","Borderless window"));GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();WindowModeButton(StudioWindowMode.Fullscreen,L("独占全屏","Exclusive full screen"));WindowModeButton(StudioWindowMode.BorderlessFullscreen,L("无边框全屏","Borderless full screen"));GUILayout.EndHorizontal();
            GUILayout.Label(L("普通窗口与无边框窗口可拖边缘缩放。F11 恢复 1280×720 普通窗口。","Drag edges to resize normal/borderless windows. F11 recovers 1280x720 windowed."));
            GUILayout.Label(L("16:9 校准视口保持不变；其他宽高比可能留边。","Calibrated 16:9 viewport is preserved; other aspect ratios may letterbox."));
            GUILayout.Label(studioWindow!=null?studioWindow.Message:"");
            GUILayout.Label((presentationDirty?"* ":"")+L("外观/窗口偏好：全局保存时记住，不改歌曲谱面。","Presentation preferences are remembered on global Save; no chart edits."));
            GUILayout.Label(presentationMessage??"");
        }
        private void WindowPreset(int width,int height,string label)
        {if(GUILayout.Button(label))QueueGui(()=>ApplyWindow(width,height,(StudioWindowMode)presentation.windowMode));}
        private void WindowModeButton(StudioWindowMode mode,string label)
        {if(GUILayout.Button((presentation.windowMode==(int)mode?"• ":"")+label))QueueGui(()=>ApplyWindow(presentation.width,presentation.height,mode));}
        private void SavePresentationPreferences()
        {
            if(!presentationDirty)return;
            string temp=PreferencesPath+".tmp";
            try
            {
#if !UNITY_EDITOR
                // Remember actual resized dimensions, not only the last preset. No autosave on resize/exit.
                if(studioWindow!=null&&!studioWindow.Busy&&(studioWindow.Mode==StudioWindowMode.Windowed||studioWindow.Mode==StudioWindowMode.BorderlessWindow))
                {presentation.width=Math.Max(960,Math.Min(7680,Screen.width));presentation.height=Math.Max(540,Math.Min(4320,Screen.height));}
#endif
                Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath));File.WriteAllText(temp,JsonUtility.ToJson(presentation,true));
                if(File.Exists(PreferencesPath))File.Replace(temp,PreferencesPath,PreferencesPath+".bak",true);else File.Move(temp,PreferencesPath);
                presentationDirty=false;presentationMessage=L("外观/窗口偏好已保存。","Presentation preferences saved.");
            }
            catch(Exception ex){presentationMessage=L("谱面保存流程独立；外观偏好保存失败：","Chart save is separate; presentation save failed: ")+ex.Message;status=presentationMessage;}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception cleanup){Debug.LogWarning("Presentation temporary-file cleanup: "+cleanup.Message,this);}}
        }
        private void TickPresentation()
        {
            if(backdrop!=null)backdrop.Tick();
#if !UNITY_EDITOR
            if(studioWindow!=null&&!studioWindow.Busy&&(studioWindow.Mode==StudioWindowMode.Windowed||studioWindow.Mode==StudioWindowMode.BorderlessWindow)&&
                (presentation.width!=Screen.width||presentation.height!=Screen.height))presentationDirty=true;
#endif
        }
        private void HandleWindowRecovery(Event e)
        {
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.F11)
            {e.Use();QueueGui(()=>ApplyWindow(1280,720,StudioWindowMode.Windowed));}
        }
        public string RuntimeReport09()
        {return RuntimeReport08()+"\nCurrent Sky group: "+currentSkyGroup+" / count: "+SkyGroupNavigation.Members(events,currentSkyGroup).Length+"\nKey sample pitch: 1.0 (music rate="+transport.Rate+")\nBackground: "+presentation.background+" enabled="+presentation.backgroundEnabled+"\nDisplay: "+Screen.width+"x"+Screen.height+" / "+(studioWindow!=null?studioWindow.Mode.ToString():"none")+"\nPreferences: "+PreferencesPath+"\n"+(studioWindow!=null?studioWindow.Message:"");}
        private void DisposePresentation()
        {if(backdrop!=null)backdrop.Dispose();if(ownedBackground!=null)Destroy(ownedBackground);}
    }
}
