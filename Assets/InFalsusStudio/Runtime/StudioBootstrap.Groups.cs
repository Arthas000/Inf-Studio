using System;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private int currentSkyGroup;
        private int groupFollowingId=-1;
        private double exactGroupJump=double.NaN;
        private string currentGroupInput="0";
        private void ResetGroupBrowser()
        {currentSkyGroup=0;groupFollowingId=-1;exactGroupJump=double.NaN;}
        private void FollowSelectedSkyGroup()
        {
            var n=events.FirstOrDefault(e=>e.SourceId==selectedId&&e.Kind==EventKind.SkyArea);
            if(n==null){groupFollowingId=-1;return;}
            groupFollowingId=n.SourceId;currentSkyGroup=SkyGroups.GroupOf(n);
        }
        private void UpdateFollowedGroupAfterEdit()
        {
            if(groupFollowingId<0||groupFollowingId!=selectedId)return;
            var n=events.FirstOrDefault(e=>e.SourceId==groupFollowingId&&e.Kind==EventKind.SkyArea);
            if(n!=null)currentSkyGroup=SkyGroups.GroupOf(n);
        }
        private void SetCurrentSkyGroup(string value)
        {
            int g;if(!int.TryParse(value,out g)||g<0)throw new FormatException(L("组号必须是 0～2147483647 的整数。","Group must be an integer in 0..2147483647."));
            currentSkyGroup=g;groupFollowingId=-1;
            int count=SkyGroupNavigation.Members(events,g).Length;
            status=count==0?L("当前组内无 SkyArea。","No SkyArea in this group."):L("当前组 ","Current group ")+g+" · "+count+" SkyArea";
        }
        private void JumpWithinSkyGroup(int direction)
        {
            if(!CommitAllInputFieldsNow())return;
            int g=currentSkyGroup;
            var members=SkyGroupNavigation.Members(events,g);
            if(members.Length==0){status=L("当前组内无 SkyArea。","No SkyArea in this group.");return;}
            var target=SkyGroupNavigation.Neighbor(members,g,CurrentMs,direction);
            if(target==null){status=L(direction>0?"当前时间之后没有同组 SkyArea。":"当前时间之前没有同组 SkyArea。",direction>0?"No later SkyArea in the current group.":"No earlier SkyArea in the current group.");return;}
            // Explicit navigation ends a placement draft, but never writes the chart or changes tools.
            CancelGesture();transport.Pause();SetSelection(new[]{target.SourceId},target.SourceId);
            exactGroupJump=target.TimeMs;transport.Seek(target.TimeMs);timeText=target.TimeMs.ToString("0.##########",CI);showNotes=true;
            status=L("组 ","Group ")+g+" · "+(Array.FindIndex(members,n=>n.SourceId==target.SourceId)+1)+"/"+members.Length+" · "+timeText+" ms";
        }
        private bool PreserveExactGroupJump()
        {
            if(double.IsNaN(exactGroupJump))return false;
            if(transport.Playing||Math.Abs(CurrentMs-exactGroupJump)>1e-7){exactGroupJump=double.NaN;return false;}
            return true; // Browsing a source event must NOT move it to a nearby authored grid point.
        }
        private void DrawCurrentGroupChrome()
        {
            int count=currentSkyGroup<0?0:events.Count(n=>n.Kind==EventKind.SkyArea&&SkyGroups.GroupOf(n)==currentSkyGroup);
            HudLabel(HR(684,10,182,17),L("当前组","Current group")+" ("+count+")",12,new Color(.7f,.9f,.95f));
            ChromeField(HR(684,29,62,25),"field_current_sky_group",ref currentGroupInput,currentSkyGroup.ToString(CI),SetCurrentSkyGroup);
            if(GUI.Button(HR(749,29,54,25),new GUIContent(L("上一个","Prev"),L("跳到当前时间之前的同组 SkyArea 起点","Previous same-group Sky start before the playhead"))))QueueGui(()=>JumpWithinSkyGroup(-1));
            if(GUI.Button(HR(806,29,57,25),new GUIContent(L("下一个","Next"),L("跳到当前时间之后的同组 SkyArea 起点","Next same-group Sky start after the playhead"))))QueueGui(()=>JumpWithinSkyGroup(1));
        }
    }
}
