using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InFalsusStudio
{
    public sealed class MeshBatch : IDisposable
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Triangles = new List<int>();
        public readonly GameObject Object;
        private readonly Mesh mesh;
        public bool ClipX;
        public float MinX,MaxX;
        private struct ClipVertex {public Vector3 P;public Color C; public ClipVertex(Vector3 p,Color c){P=p;C=c;}}
        public MeshBatch(Transform parent, string name, Material material, int order = 0)
        {
            Object = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); Object.layer = 30; Object.transform.SetParent(parent, false);
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 }; mesh.MarkDynamic();
            Object.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = Object.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.sortingOrder = order;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }
        public void Clear() { Vertices.Clear(); Colors.Clear(); Triangles.Clear(); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) { Quad(a,b,c,d,color,color,color,color); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd)
        {
            if(ClipX){Triangle(a,c,b,ca,cc,cb);Triangle(a,d,c,ca,cd,cc);return;}
            int i = Vertices.Count; Vertices.Add(a); Vertices.Add(b); Vertices.Add(c); Vertices.Add(d);
            Colors.Add(ca); Colors.Add(cb); Colors.Add(cc); Colors.Add(cd);
            Triangles.Add(i); Triangles.Add(i+2); Triangles.Add(i+1); Triangles.Add(i); Triangles.Add(i+3); Triangles.Add(i+2);
        }
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            if(ClipX&&(a.x<MinX||a.x>MaxX||b.x<MinX||b.x>MaxX||c.x<MinX||c.x>MaxX))
            {
                var polygon=new List<ClipVertex>{new ClipVertex(a,ca),new ClipVertex(b,cb),new ClipVertex(c,cc)};
                polygon=ClipPlane(polygon,MinX,true);polygon=ClipPlane(polygon,MaxX,false);
                for(int n=1;n+1<polygon.Count;n++)RawTriangle(polygon[0].P,polygon[n].P,polygon[n+1].P,polygon[0].C,polygon[n].C,polygon[n+1].C);
                return;
            }
            RawTriangle(a,b,c,ca,cb,cc);
        }
        private void RawTriangle(Vector3 a,Vector3 b,Vector3 c,Color ca,Color cb,Color cc)
        {int i=Vertices.Count;Vertices.Add(a);Vertices.Add(b);Vertices.Add(c);Colors.Add(ca);Colors.Add(cb);Colors.Add(cc);Triangles.Add(i);Triangles.Add(i+1);Triangles.Add(i+2);}
        private static List<ClipVertex> ClipPlane(List<ClipVertex> input,float plane,bool keepGreater)
        {
            var output=new List<ClipVertex>();if(input.Count==0)return output;
            ClipVertex a=input[input.Count-1];bool aIn=keepGreater?a.P.x>=plane:a.P.x<=plane;
            foreach(var b in input)
            {
                bool bIn=keepGreater?b.P.x>=plane:b.P.x<=plane;
                if(aIn!=bIn)
                {
                    float t=(plane-a.P.x)/(b.P.x-a.P.x);Vector3 p=Vector3.Lerp(a.P,b.P,t);p.x=plane;
                    output.Add(new ClipVertex(p,Color.Lerp(a.C,b.C,t)));
                }
                if(bIn)output.Add(b);a=b;aIn=bIn;
            }
            return output;
        }
        public void Strip(Vector3 a, Vector3 b, float width, Color color)
        {
            // Offset in the horizontal plane. For a transverse line this offsets Z.
            Vector3 side = Vector3.Cross(Vector3.up, b-a).normalized * width * .5f;
            if (side.sqrMagnitude < 1e-12f) side = Vector3.right * width * .5f;
            Quad(a-side,a+side,b+side,b-side,color);
        }
        /// <summary>Camera-facing ribbon with a stable pixel width, only for authoring guides.
        /// Centerline screen coordinates still come from the actual shared stage/time geometry.
        /// Bias slightly toward the camera so subpixel edges do not disappear inside the floor.
        /// No sorting layer, camera, track surface or note geometry is changed.</summary>
        public void ScreenStrip(Camera camera,Vector3 a,Vector3 b,float pixels,Color color)
        {
            Vector3 pa=camera.WorldToScreenPoint(a),pb=camera.WorldToScreenPoint(b);
            if(pa.z<=camera.nearClipPlane||pb.z<=camera.nearClipPlane)return;
            Vector2 direction=new Vector2(pb.x-pa.x,pb.y-pa.y);
            if(direction.sqrMagnitude<.01f)return;
            Vector2 offset=new Vector2(-direction.y,direction.x).normalized*(pixels*.5f);
            float factor=2*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)/Mathf.Max(1,camera.pixelRect.height);
            float za=Mathf.Max(camera.nearClipPlane+.001f,pa.z-pa.z*factor*(pixels+1));
            float zb=Mathf.Max(camera.nearClipPlane+.001f,pb.z-pb.z*factor*(pixels+1));
            Quad(camera.ScreenToWorldPoint(new Vector3(pa.x-offset.x,pa.y-offset.y,za)),
                 camera.ScreenToWorldPoint(new Vector3(pa.x+offset.x,pa.y+offset.y,za)),
                 camera.ScreenToWorldPoint(new Vector3(pb.x+offset.x,pb.y+offset.y,zb)),
                 camera.ScreenToWorldPoint(new Vector3(pb.x-offset.x,pb.y-offset.y,zb)),color);
        }
        public void Commit()
        {
            mesh.Clear(); if (Vertices.Count == 0) { Object.SetActive(false); return; }
            Object.SetActive(true); mesh.SetVertices(Vertices); mesh.SetColors(Colors); mesh.SetTriangles(Triangles,0); mesh.RecalculateBounds();
        }
        public void Dispose() { if (Application.isPlaying) { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(Object); } else { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(Object); } }
    }
    public sealed class StudioMaterials : IDisposable
    {
        public readonly Material Opaque, Transparent, Glow;
        public StudioMaterials(StudioProfile p)
        {
            Opaque = Make("VertexOpaque",p); Transparent = Make("VertexTransparent",p); Glow = Make("VertexGlow",p);
        }
        private static Material Make(string shaderName, StudioProfile p)
        {
            var shader = Resources.Load<Shader>("InFalsusStudio/" + shaderName);
            if (shader == null) throw new InvalidOperationException("Missing InFalsusStudio shader: " + shaderName + ". Copy the complete Assets/InFalsusStudio folder.");
            var m = new Material(shader) { name = "IF " + shaderName };
            m.SetColor("_FogColor",p.background); m.SetFloat("_FogStart",p.fogStart);m.SetFloat("_FogEnd",p.fogEnd);return m;
        }
        public void Dispose() { foreach (var m in new[]{Opaque,Transparent,Glow}) { if (Application.isPlaying) UnityEngine.Object.Destroy(m); else UnityEngine.Object.DestroyImmediate(m); } }
    }
}
