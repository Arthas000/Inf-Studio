#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using InFalsusStudio.Core;

namespace InFalsusStudio.Editor
{
    public static class StudioSetup
    {
        public const string Base="Assets/InFalsusStudio/";
        public const string ScenePath=Base+"Scenes/InFalsusStudio_Demo.unity";
        [MenuItem("Tools/InFalsus Studio/Open Demo Scene")]
        public static void OpenDemo()
        {
            if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("InFalsus Studio","Exit Play Mode before opening the demo scene.","OK");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("InFalsus Studio - Play to initialize");var app=root.AddComponent<StudioBootstrap>();
            app.profileAsset=AssetDatabase.LoadAssetAtPath<StudioProfile>(Base+"Resources/InFalsusStudio/CalibratedProfile.asset");
            app.initialChart=AssetDatabase.LoadAssetAtPath<TextAsset>(Base+"Resources/InFalsusStudio/LightsOut.spc.txt");
            app.initialShowNotes=false;app.initialTimeMs=1846;
            Directory.CreateDirectory(Base+"Scenes");EditorSceneManager.SaveScene(scene,ScenePath);Selection.activeGameObject=root;
        }
        [MenuItem("Tools/InFalsus Studio/Run CSharp Core Checks")]
        public static void RunCoreChecks()
        {
            try{string path=Base+"Resources/InFalsusStudio/LightsOut.spc.txt";string report=CoreSelfTests.Run(File.Exists(path)?File.ReadAllBytes(path):null);Debug.Log(report);EditorUtility.DisplayDialog("Core checks","Passed. Full assertion report is in the Console.\nThese checks do not certify shader or visual fidelity.","OK");}
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog("Core checks failed",ex.Message,"OK");}
        }
        [MenuItem("Tools/InFalsus Studio/Check Running Camera Landmarks")]
        public static void CheckCamera()
        {
            var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
            if(!EditorApplication.isPlaying||app==null||app.ViewCamera==null){EditorUtility.DisplayDialog("Camera checks","Open the demo and enter Play Mode first.","OK");return;}
            try
            {
                Camera c=app.ViewCamera;StudioProfile p=app.Profile;
                Check(c,new Vector3(-2,0,0),434,1029,"ground left");Check(c,new Vector3(2,0,0),1614,1029,"ground right");
                Check(c,new Vector3(0,p.skyHeight,0),1024,734,"sky center");
                Check(c,StageSpace.LanePoint(p,0,0,0),124,840,"left side outer");Check(c,StageSpace.LanePoint(p,5,1,0),1924,840,"right side outer");
                if((StageSpace.LanePoint(p,0,1,0)-StageSpace.LanePoint(p,1,0,0)).magnitude>.00001f)throw new Exception("Left side detached from floor.");
                if((StageSpace.LanePoint(p,5,0,0)-StageSpace.LanePoint(p,4,1,0)).magnitude>.00001f)throw new Exception("Right side detached from floor.");
                CheckFlick(c,p,false,-2,0,1.23f);CheckFlick(c,p,true,-1.3333333f,.6666667f,8.16f);
                foreach(float z in new[]{0f,5f,20f})foreach(float width in new[]{.1666667f,.3333333f,1f,2f,4f})
                {CheckFlick(c,p,false,-width*.5f,width*.5f,z);CheckFlick(c,p,true,-width*.5f,width*.5f,z);}
                string message="Camera landmarks two shared seams and flat Flick geometry passed at 2048x1152-equivalent coordinates.\nChosen camera model; not recovered game internals.";Debug.Log(message);EditorUtility.DisplayDialog("Camera checks",message,"OK");
            }
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog("Camera check failed",ex.Message+"\nRestore calibrated defaults and use locked 16:9.","OK");}
        }
        [MenuItem("Tools/InFalsus Studio/Check v0.5 Air Mesh Clipping")]
        public static void CheckAirClip()
        {
            GameObject root=null;StudioMaterials mats=null;MeshBatch batch=null;StudioProfile p=null;
            try
            {
                p=ScriptableObject.CreateInstance<StudioProfile>();p.Validate();mats=new StudioMaterials(p);
                root=new GameObject("Temporary InFalsus clipping test");root.hideFlags=HideFlags.HideAndDontSave;
                batch=new MeshBatch(root.transform,"clipping test",mats.Transparent);batch.Object.SetActive(false);
                batch.ClipX=true;batch.MinX=-2;batch.MaxX=2;
                batch.Quad(new Vector3(-3,1,0),new Vector3(3,1,0),new Vector3(3,1,2),new Vector3(-3,1,2),Color.white);
                batch.Strip(new Vector3(-2,1,0),new Vector3(-2,1,10),.2f,Color.white);
                batch.Strip(new Vector3(2,1,0),new Vector3(2,1,10),.2f,Color.white);
                if(batch.Vertices.Count==0||batch.Vertices.Count!=batch.Colors.Count)throw new Exception("Clipping lost all geometry/colors.");
                foreach(var v in batch.Vertices)if(v.x < -2.00001f||v.x>2.00001f||float.IsNaN(v.x)||float.IsInfinity(v.x))throw new Exception("Air border/glow escaped reach bounds.");
                foreach(int i in batch.Triangles)if(i<0||i>=batch.Vertices.Count)throw new Exception("Invalid clipped triangle index.");
                Debug.Log("PASS v0.5 actual C# MeshBatch X clipping: crossing quad + both expanded border strips. This is geometry testing, not a screenshot or gameplay judgement test.");
                EditorUtility.DisplayDialog("Air mesh checks","Clipped fill + expanded borders stay in X [-2,2]. Passed actual MeshBatch code.","OK");
            }
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog("Air mesh checks failed",ex.Message,"OK");}
            finally
            {
                if(batch!=null)batch.Dispose();if(mats!=null)mats.Dispose();
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(p!=null)UnityEngine.Object.DestroyImmediate(p);
            }
        }
        private static void CheckFlick(Camera c,StudioProfile p,bool right,float xl,float xr,float z)
        {
            float y=p.skyHeight+.019f;
            for(int i=0;i<=48;i++)
            {
                Vector3 point=FlickGeometry.BackVertex(p,c,xl,xr,y,z,right,i/48f);
                if(Mathf.Abs(point.y-y)>.00001f||point.z<z-.0001f)throw new Exception("Flick must remain flat and recede in +Z.");
            }
            Vector3 hs=c.WorldToScreenPoint(new Vector3(right?xr:xl,y,z));
            Vector3 ts=c.WorldToScreenPoint(new Vector3(right?xl:xr,y,z));
            Vector3 back=c.WorldToScreenPoint(FlickGeometry.BackVertex(p,c,xl,xr,y,z,right,0));
            float expected=FlickGeometry.ScreenHeight(p,c,p.skyHeight+.019f,z);
            if(Mathf.Abs(back.y-hs.y-expected)>1)throw new Exception("Flick width-independent height mismatch.");
        }
        [MenuItem("Tools/InFalsus Studio/Check v0.6 Key Sound Assets")]
        public static void CheckKeySoundAssets()
        {
            try
            {
                string[] names={"FloorHit","SideHit","SkyHit","Flick","FloorHold","SkyHold"};
                foreach(string name in names)
                {
                    string asset=Base+"Resources/InFalsusStudio/KeySounds/"+name+".ogg";
                    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(asset);
                    if(clip==null||clip.samples<=0||clip.channels!=2||clip.frequency!=48000)
                        throw new Exception("Missing or unexpected audio import: "+asset);
                    clip.LoadAudioData();
                    Debug.Log(name+": "+clip.length+"s / "+clip.frequency+"Hz / "+clip.channels+"ch / "+clip.loadState);
                }
                EditorUtility.DisplayDialog("Key sound assets","All six clips imported with the expected sample rate/channels. This does not measure output-device latency or validate loop seams.","OK");
            }
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog("Key sound check failed",ex.Message,"OK");}
        }
        [MenuItem("Tools/InFalsus Studio/Check v0.7 Runtime and Difficulty Assets")]
        public static void CheckRuntime07()
        {
            try
            {
                for(int i=0;i<4;i++)
                {
                    var tex=Resources.Load<Texture2D>("InFalsusStudio/Hud/SideDifficulty"+i);
                    if(tex==null||tex.width!=1024||tex.height!=1024)throw new Exception("Missing difficulty tab texture "+i);
                }
                var app=UnityEngine.Object.FindFirstObjectByType<StudioBootstrap>();
                if(!EditorApplication.isPlaying||app==null)throw new Exception("Enter demo Play Mode first.");
                string report=app.RuntimeReport07();Debug.Log(report);
                EditorUtility.DisplayDialog("v0.7 runtime diagnostics",report+"\n\nThis diagnostic does not substitute for an audible test of dense charts.","OK");
            }
            catch(Exception ex){Debug.LogException(ex);EditorUtility.DisplayDialog("v0.7 check",ex.Message,"OK");}
        }
        // Batch entry point: no modal dialogs; C# import/compile + pure-core checks only.
        public static void VerifyBatch()
        {
            try
            {
                string report=CoreSelfTests.Run(File.ReadAllBytes(Base+"Resources/InFalsusStudio/LightsOut.spc.txt"));
                Directory.CreateDirectory("Temp");File.WriteAllText("Temp/InFalsusCoreChecks.txt",report);Debug.Log(report);
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex)
            {
                Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;
            }
        }
        private static void Check(Camera c,Vector3 world,double x,double y,string name)
        {
            Vector3 v=c.WorldToViewportPoint(world);double px=v.x*2048,py=(1-v.y)*1152;
            Debug.Log(name+": "+px.ToString("F2")+", "+py.ToString("F2"));
            if(Math.Abs(px-x)>2.5||Math.Abs(py-y)>2.5)throw new Exception(name+" projection differs by more than 2.5 px.");
        }
        [MenuItem("Tools/InFalsus Studio/Build Windows x64 Player...")]
        public static void BuildWindows(){StudioBuild08.BuildInteractive();}
        [MenuItem("Tools/InFalsus Studio/Write Environment Report")]
        public static void EnvironmentReport()
        {
            string text="Unity: "+Application.unityVersion+"\nPlatform: "+Application.platform+"\nPipeline: "+(GraphicsSettings.currentRenderPipeline==null?"Built-in":GraphicsSettings.currentRenderPipeline.GetType().FullName)+"\nGPU: "+SystemInfo.graphicsDeviceName+"\nShader level: "+SystemInfo.graphicsShaderLevel+"\n\n";
            string manifest="Packages/manifest.json";if(File.Exists(manifest))text+=File.ReadAllText(manifest);
            string dest=EditorUtility.SaveFilePanel("Environment report","","infalsus-environment.txt","txt");if(dest.Length>0)File.WriteAllText(dest,text);
        }
    }
}
#endif
