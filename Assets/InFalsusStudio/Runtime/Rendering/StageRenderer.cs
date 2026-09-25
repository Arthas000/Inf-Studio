using System;
using UnityEngine;

namespace InFalsusStudio
{
    public sealed class StageRenderer : IDisposable
    {
        private readonly MeshBatch floor, detail, glow;
        public StageRenderer(Transform parent, StudioProfile p, StudioMaterials m)
        {
            floor=new MeshBatch(parent,"Stage - four floor lanes and two joined slopes",m.Opaque);
            detail=new MeshBatch(parent,"Stage - rails and key surfaces",m.Transparent,0);
            glow=new MeshBatch(parent,"Stage - judgement lights",m.Glow,30);
            Build(p);
        }
        private void Build(StudioProfile p)
        {
            floor.Clear();detail.Clear();glow.Clear();
            // Direct world vertices. Never rotate a Unity Plane around its centre to make side lanes.
            floor.Quad(StageSpace.Surface(p,-p.centralHalfWidth,p.stageNear-.3f),StageSpace.Surface(p,p.centralHalfWidth,p.stageNear-.3f),StageSpace.Surface(p,p.centralHalfWidth,p.stageFar),StageSpace.Surface(p,-p.centralHalfWidth,p.stageFar),new Color(.014f,.022f,.035f,1));
            for(int lane=0;lane<6;lane++)
            {
                Color baseColor=lane==0?p.leftSide:lane==5?p.rightSide:p.floorColor;
                float inset=lane==0||lane==5?0:.025f;
                floor.Quad(StageSpace.LanePoint(p,lane,inset,p.stageNear),StageSpace.LanePoint(p,lane,1-inset,p.stageNear),StageSpace.LanePoint(p,lane,1-inset,p.stageFar),StageSpace.LanePoint(p,lane,inset,p.stageFar),baseColor);
                // Subtle facets and lines, generated without original-game textures.
                for(float z=p.stageNear;z<p.stageFar;z+=1.6f)
                {
                    float end=Mathf.Min(z+1.6f,p.stageFar);
                    Color tint=Color.Lerp(baseColor,Color.white,.026f);
                    floor.Triangle(StageSpace.LanePoint(p,lane,inset,z,.001f),StageSpace.LanePoint(p,lane,1-inset,end,.001f),StageSpace.LanePoint(p,lane,inset,end,.001f),tint,tint,baseColor);
                }
                if(lane==0||lane==5)
                {
                    for(int s=0;s<=14;s++)
                    {
                        float u=s/14f;
                        Vector3 a=StageSpace.LanePoint(p,lane,u,p.stageNear,.006f),b=StageSpace.LanePoint(p,lane,u,p.stageFar,.006f);
                        detail.Strip(a,b,.008f,new Color(.57f,.65f,.84f,s%2==0?.20f:.07f));
                    }
                }
                else
                {
                    for(int s=1;s<=3;s++) detail.Strip(StageSpace.LanePoint(p,lane,s/4f,.05f,.008f),StageSpace.LanePoint(p,lane,s/4f,3.8f,.008f),.003f,new Color(.57f,.87f,1,.34f));
                }
                // Key-cap footprint uses the very same Surface/LanePoint as notes and judgement.
                Color key=new Color(.35f,.70f,.80f,.16f), edge=new Color(.55f,.93f,1,.42f);
                var a0=StageSpace.LanePoint(p,lane,.10f,.07f,.014f);var a1=StageSpace.LanePoint(p,lane,.90f,.07f,.014f);
                var a2=StageSpace.LanePoint(p,lane,.985f,.38f,.014f);var a3=StageSpace.LanePoint(p,lane,.90f,1.50f,.014f);
                var a4=StageSpace.LanePoint(p,lane,.10f,1.50f,.014f);var a5=StageSpace.LanePoint(p,lane,.015f,.38f,.014f);
                Vector3 mid=(a0+a3)*.5f;
                Vector3[] cap={a0,a1,a2,a3,a4,a5};
                for(int j=0;j<cap.Length;j++) {detail.Triangle(mid,cap[j],cap[(j+1)%cap.Length],new Color(key.r,key.g,key.b,.025f),key,key);detail.Strip(cap[j],cap[(j+1)%cap.Length],.005f,edge);}
                Color judge=lane==0?new Color(.62f,.39f,1,1):lane==5?new Color(1,.40f,.66f,1):new Color(.30f,.95f,1,1);
                Vector3 left=StageSpace.LanePoint(p,lane,0,0,.012f),right=StageSpace.LanePoint(p,lane,1,0,.012f);
                glow.Strip(left,right,.10f,new Color(judge.r,judge.g,judge.b,.16f));glow.Strip(left,right,.033f,judge);
                glow.Strip(left,right,.011f,new Color(.86f,1,1,.98f));
            }
            // Air judgement bar has its own world plane. It is not a full-screen GUI line.
            float ah=p.skyJudgeHalfWidth;
            for(int i=0;i<20;i++)
            {
                float xa=Mathf.Lerp(-ah,ah,i/20f),xb=Mathf.Lerp(-ah,ah,(i+1)/20f);
                float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-Mathf.Abs((i+.5f)/10f-1))*4));
                Vector3 a=new Vector3(xa,p.skyHeight,0),b=new Vector3(xb,p.skyHeight,0);
                glow.Strip(a,b,.045f,new Color(.61f,.30f,1,.25f*alpha));
                glow.Strip(a,b,.019f,new Color(.80f,.47f,1,alpha));
                glow.Strip(a,b,.006f,new Color(.98f,.80f,1,alpha));
            }
            // Side boundaries meet the ground exactly at x = +/- centralHalfWidth.
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(p.centralHalfWidth+p.sideRun);
                detail.Strip(StageSpace.Surface(p,x,p.stageNear,.010f),StageSpace.Surface(p,x,p.stageFar,.010f),.025f,new Color(.60f,.45f,.85f,.50f));
                // A restrained distant tunnel. Not a reconstruction of the game's animated backdrop.
                float z=p.stageFar;
                for(int j=0;j<4;j++)
                {
                    Vector3 a=new Vector3(side*(3.5f+j*.7f),.0f,z),b=new Vector3(side*(3.5f+j*.7f),8,z+10);
                    detail.Strip(a,b,.015f,new Color(.17f,.40f,.55f,.25f));
                }
            }
            floor.Commit();detail.Commit();glow.Commit();
        }
        public void Dispose(){floor.Dispose();detail.Dispose();glow.Dispose();}
    }
}
