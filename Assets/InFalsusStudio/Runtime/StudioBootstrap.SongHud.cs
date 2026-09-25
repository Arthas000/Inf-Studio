using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private Texture2D scoreFrame;
        private readonly Texture2D[] difficultySkins=new Texture2D[4];
        private bool hudLoaded;
        private Font hudFont;
        private Rect hudScoreRect,hudSongRect;
        private float hudScale=1;
        private enum HudField { None, Title, Artist, Level }
        private HudField hudField;
        private string hudEditValue="";
        private SongFolderProject hudEditProject;
        private int hudEditDifficulty=-1;
        private bool hudFocusPending;
        private void BeginHudField(HudField field)
        {
            if(songProject==null){status="Open a song folder first.";return;}
            hudField=field;hudEditProject=songProject;hudEditDifficulty=songProject.ActiveDifficulty;
            hudEditValue=field==HudField.Title?songProject.Title:field==HudField.Artist?songProject.Artist:songProject.Difficulties[hudEditDifficulty].Rating;
            ForgetInputField("field_hud_metadata");hudFocusPending=true;status=L("回车或点击外部应用到草稿，Ctrl+S 才保存。","Enter / focus loss applies to draft. Ctrl+S saves.");
        }
        private void CancelHudField()
        {hudField=HudField.None;hudEditProject=null;hudEditDifficulty=-1;hudFocusPending=false;}
        private void ApplyMetadata(SongFolderProject expected,int index,string title,string artist,string rating)
        {
            if(songProject!=expected||expected==null||index!=expected.ActiveDifficulty)
                throw new InvalidOperationException("The song/difficulty changed during metadata editing; nothing was written.");
            status=SongMetadataEdit.Stage(expected,index,title,artist,rating);
            songTitleInput=expected.Title;songArtistInput=expected.Artist;songRatingInput=expected.Difficulties[index].Rating;RefreshProjectDirty();
        }
        private void CommitHudField()
        {
            if(hudField==HudField.None)return;
            var expected=hudEditProject;int index=hudEditDifficulty;var field=hudField;string value=hudEditValue;
            QueueGui(()=>
            {
                try
                {
                    if(expected==null||expected!=songProject||index!=expected.ActiveDifficulty)
                        throw new InvalidOperationException("Song/difficulty changed; reopen the field before editing.");
                    ApplyMetadata(expected,index,field==HudField.Title?value:expected.Title,
                        field==HudField.Artist?value:expected.Artist,field==HudField.Level?value:expected.Difficulties[index].Rating);
                    CancelHudField();GUI.FocusControl(null);
                }
                catch(Exception ex){status="Metadata not applied: "+ex.Message;hudFocusPending=true;}
            });
        }
        private bool HandleHudEditingKeys(Event e)
        {
            if(hudField==HudField.None)return false;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)
            {QueueGui(()=>{CancelHudField();ForgetInputField("field_hud_metadata");GUI.FocusControl(null);});e.Use();return true;}
            return false;
        }
        private void DrawHudEditField()
        {
            if(hudField==HudField.None)return;
            Rect input=hudField==HudField.Title?HR(1460,19,425,31):hudField==HudField.Artist?HR(1460,51,425,26):HR(1483,83,54,26);
            var expected=hudEditProject;int index=hudEditDifficulty;HudField key=hudField;
            string live=expected==null?hudEditValue:key==HudField.Title?expected.Title:key==HudField.Artist?expected.Artist:expected.Difficulties[index].Rating;
            hudEditValue=CommittedField(input,"field_hud_metadata",live,value=>
            {
                if(expected==null||expected!=songProject||index!=expected.ActiveDifficulty)throw new InvalidOperationException("Song/difficulty changed.");
                ApplyMetadata(expected,index,key==HudField.Title?value:expected.Title,key==HudField.Artist?value:expected.Artist,key==HudField.Level?value:expected.Difficulties[index].Rating);
                CancelHudField();GUI.FocusControl(null);
            });
            if(hudFocusPending&&Event.current.type==EventType.Repaint){GUI.FocusControl("field_hud_metadata");hudFocusPending=false;}
        }
        private Vector2 hudOrigin;
        private void EnsureHudAssets()
        {
            if(hudLoaded)return;hudLoaded=true;
            scoreFrame=Resources.Load<Texture2D>("InFalsusStudio/Hud/ScoreFrame");
            for(int i=0;i<4;i++)difficultySkins[i]=Resources.Load<Texture2D>("InFalsusStudio/Hud/Difficulty"+i);
            try{hudFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Arial","Noto Sans"},20);}catch{hudFont=null;}
        }
        private Rect HR(float x,float y,float w,float h){return new Rect(hudOrigin.x+x*hudScale,hudOrigin.y+y*hudScale,w*hudScale,h*hudScale);}
        private void LayoutHud()
        {
            Rect view=ViewCamera.pixelRect;hudScale=Mathf.Min(view.width/1920f,view.height/1080f);hudScale=Mathf.Max(.1f,hudScale);
            hudOrigin=new Vector2(view.x+(view.width-1920*hudScale)*.5f,Screen.height-view.y-view.height+(view.height-1080*hudScale)*.5f);
            hudScoreRect=HR(6,4,580,145);hudSongRect=HR(1335,10,575,146);
        }
        private bool HudBlocksInput(Vector2 p)
        {return guiShowSongHud&&(hudScoreRect.Contains(p)||hudSongRect.Contains(p))||guiShowSongPanel&&songPanelRect.Contains(p);}
        private static void HudFill(Rect r,Color c)
        {Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        private GUIStyle HudStyle(int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var s=new GUIStyle(GUI.skin.label){fontSize=Mathf.Max(9,Mathf.RoundToInt(size*hudScale)),alignment=alignment,clipping=TextClipping.Clip};
            if(hudFont!=null)s.font=hudFont;s.normal.textColor=color;s.padding=new RectOffset(0,0,0,0);return s;
        }
        private void HudLabel(Rect r,string text,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var s=HudStyle(size,color,alignment);
            while(s.fontSize>Mathf.RoundToInt(11*hudScale)&&s.CalcSize(new GUIContent(text)).x>r.width)s.fontSize--;
            GUI.Label(r,text,s);
        }
        private void DrawSongHud()
        {
            if(!guiShowSongHud)return;EnsureHudAssets();
            // Fixed-rect GUI only; no dynamic GUILayout groups in the gameplay HUD.
            if(Event.current.type==EventType.Repaint)
            {
                if(scoreFrame!=null)GUI.DrawTexture(hudScoreRect,scoreFrame,ScaleMode.StretchToFill,true);
                else HudFill(hudScoreRect,new Color(.02f,.045f,.06f,.94f));
                // SCORE lettering is inset in the left compartment of the supplied art.
                // Reference anchor: roughly x=35,y=31 in the 2048x1152 screenshot.
                HudLabel(HR(33,31,78,20),"SCORE",14,new Color(.62f,.86f,.9f));
                string digits=null,mode="COUNT UNAVAILABLE";long? max=null;int current=0,total=0;
                if(comboTimeline!=null&&comboTimeline.Supported)
                {
                    current=comboTimeline.At(CurrentMs).Total;total=comboTimeline.Totals.Total;
                    digits=ScoreEvidence.Format(PreviewScore.At(current,total));
                    max=PreviewScore.Maximum(total);
                    mode="AUTO EXACT / EST. SCORE & TICKS";
                }
                // Explicit user recordings can override the estimate; never mistake an
                // interpolating reference score display for a recovered scoring formula.
                if(scoreEvidence!=null&&evidenceMatches)
                {
                    var observation=scoreEvidence.At(CurrentMs);
                    if(observation!=null){digits=ScoreEvidence.Format(observation.Score);mode="RECORDED SCORE";}
                }
                DrawScoreDigits(HR(138,40,328,54),digits);
                HudLabel(HR(138,101,350,17),max.HasValue?"MAX "+ScoreEvidence.Format(max.Value)+" | "+current+" / "+total:"MAX UNAVAILABLE",11,new Color(.64f,.8f,.84f));
                HudLabel(HR(138,119,350,17),mode,11,new Color(.64f,.8f,.84f));
                // Right panel is an original simple frame. Only the supplied difficulty
                // textures and jacket are used; no fabricated original-game top-right art.
                HudFill(HR(1395,17,508,92),new Color(.025f,.053f,.067f,.97f));
                HudFill(HR(1395,17,508,2),new Color(.45f,.69f,.72f,.65f));HudFill(HR(1395,107,508,2),new Color(.45f,.69f,.72f,.65f));
                HudFill(HR(1901,19,2,60),new Color(.62f,.81f,.84f,.9f));
                int index=guiSongIndex>=0?guiSongIndex:3;
                var skin=difficultySkins[index];Rect badge=HR(1338,77,184,46);
                if(skin!=null)GUI.DrawTexture(badge,skin,ScaleMode.StretchToFill,true);else HudFill(badge,new Color(.4f,.18f,.6f));
                Rect cover=HR(1338,11,106,106);
                if(songJacket!=null)GUI.DrawTexture(cover,songJacket,ScaleMode.ScaleAndCrop,true);
                else{HudFill(cover,new Color(.08f,.13f,.16f));HudLabel(cover,"NO JACKET",12,Color.white,TextAnchor.MiddleCenter);}
                HudLabel(HR(1460,19,425,31),guiSongTitle,22,Color.white);
                HudLabel(HR(1460,51,425,24),guiSongArtist,16,new Color(.79f,.82f,.84f));
                HudLabel(HR(1453,87,30,22),guiSongIndex>=0?SongFolderProject.Codes[index]:"—",12,Color.white);
                HudLabel(HR(1483,87,36,22),guiSongRating,12,Color.white);
                HudLabel(HR(1527,87,139,22),"SPEED  "+Profile.scrollMultiplier.ToString("0.0",CI),12,new Color(.65f,.8f,.85f));
                HudFill(HR(1527,111,358,1),new Color(.33f,.52f,.58f,.8f));

            }
            // Buttons exist in all GUI passes. A click queues mutation for the next Layout.
            if(!guiShowUi)return;
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!radial.Pressed;
            try
            {
                if(GUI.Button(HR(1338,11,106,106),new GUIContent("","Open song folder"),GUIStyle.none))QueueGui(()=>{ChooseSongFolder();});
                if(hudField==HudField.None)
                {
                    if(GUI.Button(HR(1460,19,425,31),new GUIContent("","Edit title"),GUIStyle.none))QueueGui(()=>BeginHudField(HudField.Title));
                    if(GUI.Button(HR(1460,51,425,24),new GUIContent("","Edit artist"),GUIStyle.none))QueueGui(()=>BeginHudField(HudField.Artist));
                    if(GUI.Button(HR(1483,87,36,22),new GUIContent("","Edit level (difficulty code is fixed)"),GUIStyle.none))QueueGui(()=>BeginHudField(HudField.Level));
                }
                DrawHudEditField();
            }
            finally{GUI.enabled=enabled;}
        }
        private void DrawCenterCombo()
        {
            if(!showCenterCombo||Event.current.type!=EventType.Repaint||comboTimeline==null||!comboTimeline.Supported)return;
            EnsureHudAssets();int count=comboTimeline.At(CurrentMs).Total;
            if(count<=0)return;
            // Fixed viewport-relative overlay, not a world-space object or input target.
            // F1 hides editor controls but deliberately keeps the gameplay HUD and combo.
            HudLabel(HR(710,472,500,24),"COMBO",15,new Color(.87f,.74f,1,.76f),TextAnchor.MiddleCenter);
            HudLabel(HR(711,495,500,86),count.ToString(CI),66,new Color(.025f,.015f,.05f,.60f),TextAnchor.MiddleCenter);
            HudLabel(HR(710,493,500,86),count.ToString(CI),66,new Color(.86f,.67f,1,.92f),TextAnchor.MiddleCenter);
        }
        private void DrawScoreDigits(Rect rect,string text)
        {
            // Fixed nine-digit budget includes TWO separator slots, eight gaps, padding,
            // and glow. All generated rectangles are bounded without an extra GUI group.
            string digits=text==null?"---------":text.Replace("'","");
            if(digits.Length>9){HudLabel(rect,"OVERFLOW",25,Color.red);return;}
            // No BeginGroup inside a Repaint-only branch: that would allocate an extra
            // control ID and destabilize the buttons that follow in other GUI passes.
            var boxes=ScoreHudLayout.Digits(rect.width,rect.height);
            for(int i=0;i<9;i++)
            {
                var b=boxes[i];char c=i<digits.Length?digits[i]:'0';
                DrawSeven(new Rect(rect.x+(float)b.X,rect.y+(float)b.Y,(float)b.W,(float)b.H),c);
                if(i==2||i==5)
                {var mark=ScoreHudLayout.Separator(rect.width,rect.height,i);HudFill(new Rect(rect.x+(float)mark.X,rect.y+(float)mark.Y,(float)mark.W,(float)mark.H),new Color(.7f,1,1,.92f));}
            }
        }
        private void DrawSeven(Rect r,char digit)
        {
            // Axis-aligned filled segments avoid GUI.matrix rotation bleed and do not
            // light inactive segments. The glow stays inside each glyph's own rectangle.
            int[] patterns={63,6,91,79,102,109,125,7,127,111};int mask=digit>='0'&&digit<='9'?patterns[digit-'0']:64;
            float t=Mathf.Max(.5f,Mathf.Min(r.width*.095f,r.height*.07f));
            float pad=t*1.6f,left=r.x+pad,right=r.xMax-pad,top=r.y+pad,bottom=r.yMax-pad,mid=(top+bottom)*.5f;
            Rect[] segments={
                new Rect(left+t,top,right-left-2*t,t),new Rect(right-t,top+t,t,mid-top-1.5f*t),
                new Rect(right-t,mid+.5f*t,t,bottom-mid-1.5f*t),new Rect(left+t,bottom-t,right-left-2*t,t),
                new Rect(left,mid+.5f*t,t,bottom-mid-1.5f*t),new Rect(left,top+t,t,mid-top-1.5f*t),
                new Rect(left+t,mid-.5f*t,right-left-2*t,t)};
            for(int k=0;k<7;k++)if((mask&(1<<k))!=0)
            {Rect s=segments[k];float glow=t*.7f;HudFill(new Rect(s.x-glow,s.y-glow,s.width+2*glow,s.height+2*glow),new Color(.2f,.92f,1,.14f));HudFill(s,new Color(.67f,1,1,.97f));}
        }
    }
}
