using System;
using System.Collections.Generic;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    /// <summary>Bounded procedural audition effects. Deterministic by chart time; no gameplay
    /// judgement, no score increments, no new textures/ParticleSystem dependency.</summary>
    public sealed class HitFeedbackRenderer:IDisposable
    {
        private readonly StudioProfile p;private readonly Camera camera;private readonly MeshBatch glow;
        private SkyConnections connections;
        public int EffectCount {get;private set;}
        public HitFeedbackRenderer(Transform parent,StudioProfile profile,StudioMaterials materials,Camera cam)
        {p=profile;camera=cam;glow=new MeshBatch(parent,"Autoplay feedback - bounded procedural glow",materials.Glow,45);}
        public void Rebuild(IList<SpcEvent> notes){connections=new SkyConnections(notes);}
        private static Color A(Color c,float a){c.a=a;return c;}
        private static float Noise(int seed,int n){return (float)(Math.Sin(seed*12.9898+n*78.233)*43758.5453-Math.Floor(Math.Sin(seed*12.9898+n*78.233)*43758.5453));}
        private static float Pulse(double age){if(age<0||age>.34)return 0;return age<.026?(float)(age/.026):(float)Math.Pow(1-(age-.026)/.314,2);}
        public void Draw(IList<SpcEvent> notes,double now,bool enabled,AutoplayCursor cursor,double lowerBound=double.NegativeInfinity)
        {
            glow.Clear();glow.ClipX=false;EffectCount=0;
            if(enabled)
            {
                var skyRanges=new HashSet<string>();
                foreach(var n in notes)
                {
                    if(!n.IsNote||ChartMath.InvalidReason(n)!=null)continue;
                    if(EffectCount>=128)break; // Predictable effect-only budget; no note/audio changes.
                    if(n.Kind==EventKind.Tap)
                    {double age=(now-n.TimeMs)*.001;if(n.TimeMs>=lowerBound&&age>=0&&age<.34){GroundTap(n,age);EffectCount++;}}
                    else if(n.Kind==EventKind.Hold)
                    {if(now>=n.TimeMs&&now<n.EndMs){GroundHold(n,now);EffectCount++;}}
                    else if(n.Kind==EventKind.Flick)
                    {
                        double contact=cursor!=null?cursor.VisualContactTime(n):n.TimeMs,age=(now-contact)*.001;
                        if(contact>=lowerBound&&age>=0&&age<.34){Flick(n,age);EffectCount++;}
                    }
                    else if(n.Kind==EventKind.SkyArea&&now>=n.TimeMs&&now<n.EndMs)
                    {
                        var range=ChartMath.SkyAt(n,now);string key=Math.Round(range.Left,5)+":"+Math.Round(range.Right,5);
                        if(skyRanges.Add(key)){Sky(n,now,range.Left,range.Right);EffectCount++;}
                    }
                }
            }
            glow.ClipX=false;glow.Commit();
        }
        private Color GroundColor(int lane){return lane==0?new Color(.7f,.45f,1):lane==5?new Color(1,.25f,.64f):new Color(.18f,.86f,1);}
        private Vector3 Ground(SpcEvent n,float across,float z)
        {return Vector3.Lerp(StageSpace.FloorBound(p,n.Lane,(float)n.GroundWidth,false,z,0),StageSpace.FloorBound(p,n.Lane,(float)n.GroundWidth,true,z,0),across);}
        private void GroundTap(SpcEvent n,double age)
        {
            float power=Pulse(age);Color c=GroundColor(n.Lane);Vector3 left=Ground(n,.02f,.02f),right=Ground(n,.98f,.02f),mid=(left+right)*.5f;
            glow.Strip(left,right,.14f,A(c,.45f*power));glow.Strip(left,right,.035f,A(Color.white,.9f*power));
            Vector3 far=Ground(n,.5f,1.65f)+Vector3.up*.10f;
            glow.Strip(mid,far,.095f,A(c,.35f*power));glow.Strip(mid,far,.025f,A(Color.white,.65f*power));
            for(int k=0;k<8;k++)
            {
                Vector3 q=Ground(n,.1f+.8f*Noise(n.SourceId,k),.05f+(float)age*(.7f+Noise(n.SourceId,k+8)*2));
                q+=Vector3.up*(float)age*.65f;TriangleParticle(q,.018f+.018f*Noise(k,n.SourceId),A(c,power*.43f),k);
            }
        }
        private void GroundHold(SpcEvent n,double now)
        {
            Color c=GroundColor(n.Lane);float phase=(float)((now-n.TimeMs)*.001);
            Vector3 a=Ground(n,0,.02f),b=Ground(n,1,.02f);glow.Strip(a,b,.11f,A(c,.48f));glow.Strip(a,b,.025f,A(Color.white,.67f));
            for(int k=0;k<12;k++)
            {
                float u=(k+.5f)/12;float length=.35f+.65f*(.5f+.5f*Mathf.Sin(phase*8+k*2.17f));
                Vector3 l=Ground(n,Mathf.Max(0,u-.04f),.03f),r=Ground(n,Mathf.Min(1,u+.04f),.03f);
                Vector3 tip=Ground(n,u,.3f+length*.55f)+Vector3.up*(.05f+length*.14f);
                glow.Triangle(l,r,tip,A(c,.28f),A(c,.28f),A(c,0));
                TriangleParticle(Ground(n,u,.04f+Mathf.Repeat(phase*.65f+k*.173f,1)*.65f),.017f,A(c,.18f),k);
            }
        }
        private void Flick(SpcEvent n,double age)
        {
            var range=ChartMath.FlickRange(n);bool right=n.Get(4)==4;double x=AirReachBounds.Clamp(right?range.Right:range.Left);
            Vector3 q=new Vector3(StageSpace.AirX(p,x),p.skyHeight+.035f,.015f);Color c=right?p.rightFlick:p.leftFlick;float a=Pulse(age);
            glow.ClipX=true;glow.MinX=-p.centralHalfWidth;glow.MaxX=p.centralHalfWidth;
            glow.ScreenStrip(camera,q-Vector3.right*.23f,q+Vector3.right*.23f,5,A(c,.75f*a));
            glow.ScreenStrip(camera,q-Vector3.up*.11f,q+Vector3.up*.2f,3,A(Color.white,.75f*a));
            for(int k=0;k<7;k++)
            {float f=Noise(n.SourceId,k);Vector3 v=q+Vector3.right*(right?1:-1)*(float)age*(.4f+f)+Vector3.up*(float)age*(.4f+Noise(k,n.SourceId));TriangleParticle(v,.017f,A(c,.4f*a),k);}
            glow.ClipX=false;
        }
        private void Sky(SpcEvent n,double now,double left,double right)
        {
            left=AirReachBounds.Clamp(left);right=AirReachBounds.Clamp(right);if(right<left)return;
            float x0=StageSpace.AirX(p,left),x1=StageSpace.AirX(p,right);Color c=new Color(.76f,.43f,1);
            glow.ClipX=true;glow.MinX=-p.centralHalfWidth;glow.MaxX=p.centralHalfWidth;
            Vector3 a=new Vector3(x0,p.skyHeight+.022f,0),b=new Vector3(x1,p.skyHeight+.022f,0);
            glow.Strip(a,b,.10f,A(c,.25f));glow.Strip(a,b,.016f,A(Color.white,.45f));
            int amount=Math.Min(30,Math.Max(5,(int)((x1-x0)*8)));float t=(float)(now*.001);
            for(int k=0;k<amount;k++)
            {
                float seed=Noise(n.SourceId,k),phase=Mathf.Repeat(t*.7f+seed,1);
                Vector3 q=new Vector3(Mathf.Lerp(x0,x1,seed),p.skyHeight+.03f+phase*.12f,(phase-.5f)*.28f);
                float alpha=(.12f+.48f*Noise(k,n.SourceId))*(1-Mathf.Abs(phase-.5f)*2);
                TriangleParticle(q,.012f+.026f*Noise(n.SourceId,k+20),A(Color.Lerp(c,Color.white,seed*.55f),alpha),k);
            }
            glow.ClipX=false;
        }
        private void TriangleParticle(Vector3 q,float size,Color c,int seed)
        {
            Vector3 right=camera.transform.right*size,up=camera.transform.up*size*(seed%2==0?1:-1);
            glow.Triangle(q+up,q-right-up*.5f,q+right-up*.5f,c,c,c);
        }
        public void Dispose(){glow.Dispose();}
    }
}
