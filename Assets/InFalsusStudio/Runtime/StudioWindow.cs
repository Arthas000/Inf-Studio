using System;
using System.Collections;
using UnityEngine;

namespace InFalsusStudio
{
    public enum StudioWindowMode { Windowed=0, BorderlessWindow=1, Fullscreen=2, BorderlessFullscreen=3 }
    /// <summary>Does not modify ProjectSettings. The build enables resizableWindow.</summary>
    public sealed class StudioWindow : MonoBehaviour
    {
        public StudioWindowMode Mode {get;private set;}
        public bool Busy {get;private set;}
        public string Message {get;private set;}="";
        private int version;
        private readonly NativeBorderlessWindow native=new NativeBorderlessWindow();
        public void Apply(int width,int height,StudioWindowMode mode)
        {
            if(width<960||height<540||width>7680||height>4320)throw new ArgumentOutOfRangeException("resolution","Use 960..7680 width and 540..4320 height. 1280x720 or larger recommended.");
            if(!Enum.IsDefined(typeof(StudioWindowMode),mode))throw new ArgumentOutOfRangeException("mode");
#if UNITY_EDITOR
            Message="窗口设置仅用于独立 exe；Editor 内请调整 Game 视图。 / Standalone only; resize the Game view in Editor.";
            return;
#else
            int token=++version;StartCoroutine(Change(width,height,mode,token));
#endif
        }
        private IEnumerator Change(int width,int height,StudioWindowMode mode,int token)
        {
            Busy=true;Message="Applying display settings...";
            string restoreError=null;try{native.Restore();}
            catch(Exception ex){restoreError=ex.Message;}
            if(restoreError!=null){Busy=false;Message="Unable to restore window style: "+restoreError;yield break;}
            var full=mode==StudioWindowMode.Fullscreen?FullScreenMode.ExclusiveFullScreen:
                mode==StudioWindowMode.BorderlessFullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
            Screen.SetResolution(width,height,full);
            // Unity applies SetResolution at end-of-frame; native style changes must follow it.
            yield return null;yield return new WaitForEndOfFrame();
            if(token!=version)yield break;
            try
            {
                if(mode==StudioWindowMode.BorderlessWindow)native.Enable(width,height);
                Mode=mode;Message="Display: "+Screen.width+"x"+Screen.height+" / "+mode;
            }
            catch(Exception ex)
            {
                try{native.Restore();}catch{}Mode=StudioWindowMode.Windowed;Screen.SetResolution(width,height,FullScreenMode.Windowed);
                Message="Borderless fallback to normal window: "+ex.Message;
            }
            Busy=false;
        }
        public void RecoverWindowed(){Apply(1280,720,StudioWindowMode.Windowed);}
        private void OnDestroy(){version++;native.Dispose();}
    }
}
