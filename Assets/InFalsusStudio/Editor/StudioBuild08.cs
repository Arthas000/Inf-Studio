#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio.Editor
{
    /// <summary>Builds the ACTUAL Unity Player locally. Does not fake an executable
    /// or ship Editor-only dialogs as though they were a Windows runtime backend.</summary>
    public static class StudioBuild08
    {
        private static readonly string[] AudioModules={"com.unity.modules.unitywebrequest","com.unity.modules.unitywebrequestaudio"};
        private static UnityEditor.PackageManager.Requests.AddRequest addRequest;
        private static string[] pendingModules;private static int moduleIndex;
        [MenuItem("Tools/InFalsus Studio/Check v0.9 Runtime / Input / Projection")]
        public static void CheckRuntime()
        {
            var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(!EditorApplication.isPlaying||app==null){EditorUtility.DisplayDialog("Runtime checks","Open demo and enter Play first.","OK");return;}
            string text=app.RuntimeReport09();Debug.Log(text);EditorUtility.DisplayDialog("0.9 diagnostics",text,"OK");
        }
        [MenuItem("Tools/InFalsus Studio/Prepare Windows Audio Modules (0.9)...")]
        public static void EnableAudioModules()
        {
            if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("Audio modules","Stop Play first.","OK");return;}
            var installed=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(p=>p.name).ToArray();
            pendingModules=AudioModules.Where(n=>!installed.Contains(n)).ToArray();
            if(pendingModules.Length==0){EditorUtility.DisplayDialog("Audio modules","Both built-in modules are enabled.","OK");return;}
            if(!EditorUtility.DisplayDialog("Enable built-in modules?","This explicitly enables only these built-in modules in your project manifest:\n"+string.Join("\n",pendingModules)+"\n\nNo version upgrade, input system change or third-party package. Wait for import, then build again.","Enable","Cancel"))return;
            if(addRequest!=null)return;moduleIndex=0;addRequest=UnityEditor.PackageManager.Client.Add(pendingModules[0]+"@1.0.0");
            EditorApplication.update+=PollModules;
        }
        private static void PollModules()
        {
            if(addRequest==null||!addRequest.IsCompleted)return;
            if(addRequest.Status==UnityEditor.PackageManager.StatusCode.Failure)
            {string e=addRequest.Error==null?"Unknown package error":addRequest.Error.message;Cleanup();Debug.LogError(e);return;}
            moduleIndex++;
            if(moduleIndex<pendingModules.Length){addRequest=UnityEditor.PackageManager.Client.Add(pendingModules[moduleIndex]+"@1.0.0");return;}
            Cleanup();Debug.Log("InFalsus: built-in audio modules ready. After compilation, run Build Windows Editor 0.9.");
        }
        private static void Cleanup(){EditorApplication.update-=PollModules;addRequest=null;pendingModules=null;}
        [MenuItem("Tools/InFalsus Studio/Build Windows Editor 0.9...")]
        public static void BuildInteractive()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.isUpdating)
            {EditorUtility.DisplayDialog("Build","Stop Play and wait for compilation/import to complete.","OK");return;}
            string root=EditorUtility.OpenFolderPanel("Choose parent directory for a NEW Windows build",Path.GetDirectoryName(Application.dataPath),"");
            if(string.IsNullOrEmpty(root))return;
            string output=Path.Combine(root,"InFalsusStudio_Windows_0.9_"+DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            try
            {
                string exe=BuildTo(output);
                EditorUtility.RevealInFinder(exe);
                EditorUtility.DisplayDialog("Windows build succeeded",exe+"\n\nDouble-click this exe after the build. Keep the ENTIRE output directory together (Data, UnityPlayer.dll and other generated files). A failed build never reports success.","OK");
            }
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog("Windows build failed",ex.Message+"\n\nNo runnable release has been certified. See Console and Editor.log.","OK");}
        }
        public static void BuildBatch()
        {
            try
            {
                string output=Environment.GetEnvironmentVariable("INFALSUS_BUILD_OUTPUT");
                if(string.IsNullOrEmpty(output))output=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Builds","InFalsusStudio_Windows_0.9");
                BuildTo(output);if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }
        public static string BuildTo(string output)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before building.");
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows Build Support for this exact Unity Editor in Unity Hub.");
            var installed=UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(p=>p.name).ToArray();
            string[] missing=AudioModules.Where(n=>!installed.Contains(n)).ToArray();
            if(missing.Length>0)throw new InvalidOperationException("Built-in audio modules are disabled: "+string.Join(", ",missing)+".\nUse Tools/InFalsus Studio/Prepare Windows Audio Modules (0.9), then retry. Normal Editor operation does not require these modules.");
            if(!File.Exists(StudioSetup.ScenePath))throw new FileNotFoundException("Import/open the Demo scene before building.");
            string full=Path.GetFullPath(output),assets=Path.GetFullPath(Application.dataPath)+Path.DirectorySeparatorChar;
            if(full.StartsWith(assets,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a build output OUTSIDE Assets.");
            if(Directory.Exists(full)&&Directory.EnumerateFileSystemEntries(full).Any())throw new IOException("Choose a new EMPTY build directory to avoid mixing old and new Player files.");
            Directory.CreateDirectory(full);
            string core=CoreSelfTests.Run(File.ReadAllBytes(StudioSetup.Base+"Resources/InFalsusStudio/LightsOut.spc.txt"));
            File.WriteAllText(Path.Combine(full,"CoreChecks.txt"),core);
            int oldW=PlayerSettings.defaultScreenWidth,oldH=PlayerSettings.defaultScreenHeight;
            bool oldResizable=PlayerSettings.resizableWindow,oldRun=PlayerSettings.runInBackground;
            FullScreenMode oldMode=PlayerSettings.fullScreenMode;
            var target=NamedBuildTarget.Standalone;var oldBackend=PlayerSettings.GetScriptingBackend(target);
            string exe=Path.Combine(full,"InFalsusStudio.exe");
            try
            {
                PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
                PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
                PlayerSettings.SetScriptingBackend(target,ScriptingImplementation.Mono2x);
                var options=new BuildPlayerOptions{scenes=new[]{StudioSetup.ScenePath},locationPathName=exe,target=BuildTarget.StandaloneWindows64,
                    extraScriptingDefines=new[]{"INFALSUS_USE_UNITY_WEBREQUEST_AUDIO"},options=BuildOptions.DetailedBuildReport};
                var report=BuildPipeline.BuildPlayer(options);
                string text="Unity: "+Application.unityVersion+"\nResult: "+report.summary.result+"\nErrors: "+report.summary.totalErrors+
                    "\nWarnings: "+report.summary.totalWarnings+"\nDuration: "+report.summary.totalTime+"\nOutput: "+exe+
                    "\nBackend: Mono Windows x64\nAudio: local file URI, UnityWebRequestAudio\nNative dialogs: Windows Unicode / STA\n";
                File.WriteAllText(Path.Combine(full,"BuildReport.txt"),text);Debug.Log(text);
                if(report.summary.result!=BuildResult.Succeeded||!File.Exists(exe))throw new Exception("Unity BuildPlayer did not succeed. Check BuildReport.txt / Editor.log.");
                File.WriteAllText(Path.Combine(full,"READ_ME.txt"),"InFalsus Studio 0.9\r\nKeep all files in this directory together. Open a writable song folder outside the game's installation directory. Save/Ctrl+S commits drafts; closing without saving discards them.\r\nThis build result proves compilation only; UI, native dialogs, sound and file I/O still need runtime acceptance.\r\n");
                return exe;
            }
            finally
            {
                PlayerSettings.defaultScreenWidth=oldW;PlayerSettings.defaultScreenHeight=oldH;PlayerSettings.fullScreenMode=oldMode;
                PlayerSettings.resizableWindow=oldResizable;PlayerSettings.runInBackground=oldRun;PlayerSettings.SetScriptingBackend(target,oldBackend);
            }
        }
    }
}
#endif
