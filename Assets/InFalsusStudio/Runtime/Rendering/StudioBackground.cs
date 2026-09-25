using System;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    /// <summary>A camera-facing far background. Not a GUI overlay and never a gameplay surface.</summary>
    internal sealed class StudioBackground : IDisposable
    {
        private readonly Camera camera;
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Vector2[] uv=new Vector2[4];
        private Texture2D texture;
        public bool Enabled {get;set;}
        public float Brightness=1f;
        public bool Stretch;
        public StudioBackground(Camera view)
        {
            camera=view;
            Shader shader=Resources.Load<Shader>("InFalsusStudio/Background");
            if(shader==null)throw new InvalidOperationException("Background shader missing. Copy the entire update Assets folder.");
            material=new Material(shader){name="Studio local background (owned)"};
            mesh=new Mesh{name="Studio background quad"};
            mesh.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
            mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();
            root=new GameObject("User background (no collider)",typeof(MeshFilter),typeof(MeshRenderer));root.layer=30;
            root.transform.SetParent(camera.transform,false);root.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            root.SetActive(false);
        }
        public void SetTexture(Texture2D value){texture=value;material.mainTexture=value;}
        public void Tick()
        {
            bool visible=Enabled&&texture!=null;root.SetActive(visible);if(!visible)return;
            float depth=camera.farClipPlane*.94f;
            float hh=depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            root.transform.localPosition=new Vector3(0,0,depth);root.transform.localRotation=Quaternion.identity;
            root.transform.localScale=new Vector3(hh*camera.aspect*1.001f,hh*1.001f,1);
            UvCrop c=Stretch?new UvCrop(0,0,1,1):BackgroundFit.Cover((double)texture.width/texture.height,camera.aspect);
            uv[0]=new Vector2((float)c.X,(float)c.Y);uv[1]=new Vector2((float)(c.X+c.Width),(float)c.Y);
            uv[2]=new Vector2((float)(c.X+c.Width),(float)(c.Y+c.Height));uv[3]=new Vector2((float)c.X,(float)(c.Y+c.Height));mesh.uv=uv;
            material.SetFloat("_Brightness",Mathf.Clamp(Brightness,0,2));
        }
        public void Dispose()
        {if(root!=null)UnityEngine.Object.Destroy(root);if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(material!=null)UnityEngine.Object.Destroy(material);}
    }
}
