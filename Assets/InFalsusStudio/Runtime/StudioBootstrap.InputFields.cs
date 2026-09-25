using System;
using System.Collections.Generic;
using UnityEngine;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        // IMGUI TextField consumes Return on some editor versions. Observe it BEFORE
        // drawing any field. Keep dirty text separate from the live source; never restore
        // the old value merely because a control lost focus during this event.
        private sealed class FieldDraft
        {
            public string Text,Live;
            public bool Dirty,Queued;
            public int Ticket;
            public Rect RootRect;
            public Action<string> Commit;
        }
        private readonly Dictionary<string,FieldDraft> fieldDrafts=new Dictionary<string,FieldDraft>();
        private string activeField="";
        private Vector2 guiScreenOrigin;
        private GUISkin studioSkin;
        private int skinPixels=-1;
        private bool extraParserNotices,guiExtraParserNotices;

        private void UseStudioSkin()
        {
            EnsureHudAssets();
            if(studioSkin==null)studioSkin=Instantiate(GUI.skin);
            int pixels=Mathf.Clamp(Mathf.RoundToInt(13*hudScale),9,20);
            if(skinPixels!=pixels)
            {
                skinPixels=pixels;studioSkin.font=hudFont;
                foreach(var style in new[]{studioSkin.label,studioSkin.button,studioSkin.box,studioSkin.toggle,studioSkin.textField,studioSkin.textArea})
                {style.fontSize=pixels;style.fixedHeight=0;style.clipping=TextClipping.Clip;style.contentOffset=Vector2.zero;}
                studioSkin.label.wordWrap=true;studioSkin.label.stretchWidth=true;
                studioSkin.button.wordWrap=true;studioSkin.button.alignment=TextAnchor.MiddleCenter;
                studioSkin.toggle.wordWrap=true;
                studioSkin.textField.wordWrap=false;studioSkin.textField.alignment=TextAnchor.MiddleLeft;
                studioSkin.textArea.wordWrap=true;
                int v=Mathf.Max(1,Mathf.RoundToInt(2*hudScale));
                studioSkin.textField.padding=new RectOffset(4,4,v,v);
                studioSkin.textArea.padding=new RectOffset(4,4,v,v);
                studioSkin.button.padding=new RectOffset(4,4,v,v);
            }
            GUI.skin=studioSkin;
        }
        private Rect RootGuiRect(Rect local)
        {
            Vector2 a=GUIUtility.GUIToScreenPoint(local.position)-guiScreenOrigin;
            Vector2 b=GUIUtility.GUIToScreenPoint(new Vector2(local.xMax,local.yMax))-guiScreenOrigin;
            return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        private string CommittedField(Rect rect,string name,string live,Action<string> commit,bool area=false)
        {
            FieldDraft state;
            if(!fieldDrafts.TryGetValue(name,out state))
            {state=new FieldDraft{Text=live??"",Live=live??""};fieldDrafts[name]=state;}
            if(!state.Dirty&&!state.Queued&&GUI.GetNameOfFocusedControl()!=name)state.Text=live??"";
            state.Live=live??"";state.Commit=commit;if(Event.current.type==EventType.Repaint)state.RootRect=RootGuiRect(rect);
            GUI.SetNextControlName(name);
            string value=area?GUI.TextArea(rect,state.Text??"",GUI.skin.textArea):GUI.TextField(rect,state.Text??"",GUI.skin.textField);
            if(value!=state.Text){state.Text=value;state.Dirty=value!=state.Live;}
            if(GUI.GetNameOfFocusedControl()==name)
            {
                // A Tab key can transfer focus while fields are being drawn. Commit
                // the old field before replacing the remembered focus name.
                if(!string.IsNullOrEmpty(activeField)&&activeField!=name)QueueFieldCommit(activeField);
                activeField=name;
            }
            return state.Text;
        }
        private string CommittedRow(string label,string name,string live,Action<string> commit)
        {
            GUILayout.Label(label);
            Rect r=GUILayoutUtility.GetRect(30,Mathf.Max(22,25*hudScale),GUILayout.ExpandWidth(true));
            return CommittedField(r,name,live,commit);
        }
        private string CommittedSource(string name,string live,Action<string> commit,float height=66)
        {
            Rect r=GUILayoutUtility.GetRect(30,Mathf.Max(48,height*hudScale),GUILayout.ExpandWidth(true));
            return CommittedField(r,name,live,commit,true);
        }
        private bool CommitFieldNow(string name)
        {
            FieldDraft state;if(!fieldDrafts.TryGetValue(name,out state)||!state.Dirty)return true;
            string value=state.Text;
            try
            {
                if(state.Commit==null)return true;
                state.Commit(value);state.Dirty=false;state.Queued=false;state.Ticket++;
                return true;
            }
            catch(Exception ex)
            {state.Queued=false;status=L("输入未应用：","Input not applied: ")+ex.Message;return false;}
        }
        private void QueueFieldCommit(string name)
        {
            FieldDraft state;if(!fieldDrafts.TryGetValue(name,out state)||!state.Dirty||state.Queued)return;
            state.Queued=true;int ticket=++state.Ticket;
            // Snapshot the action and value: subsequent selection must not redirect a draft.
            string value=state.Text;Action<string> action=state.Commit;
            QueueGui(()=>
            {
                FieldDraft present;
                if(!fieldDrafts.TryGetValue(name,out present)||!ReferenceEquals(present,state)||ticket!=state.Ticket||!state.Dirty)return;
                try{if(action!=null)action(value);if(state.Text==value){state.Dirty=false;state.Queued=false;}}
                catch(Exception ex){state.Queued=false;status=L("输入未应用：","Input not applied: ")+ex.Message;}
            });
        }
        private bool CommitAllInputFieldsNow()
        {
            foreach(string key in new List<string>(fieldDrafts.Keys))if(key!="field_songfolder"&&!CommitFieldNow(key))return false;
            return true;
        }
        private bool HandleCommittedInput(Event e)
        {
            string name=GUI.GetNameOfFocusedControl();
            if(string.IsNullOrEmpty(name))name=activeField;
            FieldDraft state;bool found=fieldDrafts.TryGetValue(name,out state);
            if(e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)&&found)
            {QueueFieldCommit(name);activeField="";GUI.FocusControl(null);e.Use();return true;}
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape&&found)
            {state.Text=state.Live;state.Dirty=false;state.Queued=false;state.Ticket++;activeField="";GUI.FocusControl(null);e.Use();return true;}
            if(e.type==EventType.MouseDown&&found&&!state.RootRect.Contains(e.mousePosition))
            {QueueFieldCommit(name);activeField="";GUI.FocusControl(null);return true;}
            // Tab transfer is observed on the next GUI event, without an idle-time timer.
            if(!string.IsNullOrEmpty(activeField)&&GUI.GetNameOfFocusedControl()!=activeField&&e.type==EventType.Layout)
            {QueueFieldCommit(activeField);activeField="";}
            return false;
        }
        private void ForgetInputField(string name){fieldDrafts.Remove(name);if(activeField==name)activeField="";}
        private void ClearInputDrafts()
        {fieldDrafts.Clear();activeField="";}
        public string RuntimeReport08()
        {
            return "InFalsus Studio 0.9 / Unity "+Application.unityVersion+
                "\nTime grid 1/"+Division+"; air grid 1/"+airGrid.Division+
                "\nScroll "+Profile.scrollMultiplier.ToString("0.0",CI)+"; playback "+(transport.Rate*100).ToString("0",CI)+"%"+
                "\nSky group heads: "+Core.SkyGroups.Heads(events).Count+
                "\nPending placement: "+pointPlacement.Pending+" / "+pointPlacement.Phase+"; point menu mode: "+radial.InPointMode+
                "\nProjection vertices: "+notes.GroundProjectionVertices+"; bounds valid: "+notes.CheckGroundProjectionBounds()+
                "\nLast focused field: "+activeField+"; dirty fields: "+CountDirtyInputs()+
                "\nNative Windows dialogs/local MP3-OGG need standalone runtime acceptance; this report is not that test.";
        }
        private int CountDirtyInputs(){int count=0;foreach(var f in fieldDrafts.Values)if(f.Dirty)count++;return count;}
        private void BeginPanelContent(ref Vector2 scroll,Rect panel)
        {
            scroll.x=0;
            scroll=GUILayout.BeginScrollView(scroll,false,true,GUIStyle.none,GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(100,panel.width-26)));
        }
        private void EndPanelContent(){GUILayout.EndVertical();GUILayout.EndScrollView();}
    }
}
