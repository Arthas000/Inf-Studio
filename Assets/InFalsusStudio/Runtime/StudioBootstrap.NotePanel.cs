using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private int[] batchIds=new int[0];
        private readonly Dictionary<string,string> batchValues=new Dictionary<string,string>();
        private readonly Dictionary<int,string> batchSources=new Dictionary<int,string>();
        private int batchRevision=-1;
        private string pendingNoteKey,pendingNoteValue;
        private double pendingNoteAt;
        private EditHistory batchHistory;
        private bool sameNoteType;
        private void CaptureBatchProperties()
        {
            var selected=events.Where(n=>selection.Contains(n.SourceId)).OrderBy(n=>n.SourceId).ToArray();EventKind kind;
            sameNoteType=SelectionAuthoring.SameType(selected,out kind);
            if(!sameNoteType){guiNoteForm=noteForm=null;pendingNoteKey=null;batchIds=new int[0];return;}
            var ids=selected.Select(n=>n.SourceId).ToArray();
            bool different=batchHistory!=History||!ids.SequenceEqual(batchIds);
            if(different){pendingNoteKey=null;noteScroll=Vector2.zero;noteSourceExpanded=false;}
            if(different||batchRevision!=History.Revision||noteForm==null)
            {
                batchIds=ids;batchHistory=History;batchRevision=History.Revision;batchValues.Clear();batchSources.Clear();
                var forms=selected.Select(n=>NoteProperties.Capture(History.Document,n)).ToArray();
                noteForm=forms.FirstOrDefault(n=>n.SourceId==selectedId)??forms[0];noteFormHistory=History;
                foreach(var kv in forms[0].Values)
                    batchValues[kv.Key]=forms.All(x=>x.Values.ContainsKey(kv.Key)&&x.Values[kv.Key]==kv.Value)?kv.Value:"";
                foreach(var n in selected)batchSources[n.SourceId]=History.Document.SourceLine(n.SourceId);
                noteRawText=noteForm.OriginalSource;
            }
            guiNoteForm=noteForm;
        }
        private void QueueBatchField(string key,string value)
        {
            if(batchIds.Length==0||!sameNoteType)return;
            var ids=(int[])batchIds.Clone();var sources=new Dictionary<int,string>(batchSources);var history=History;var kind=noteForm.Kind;
            // Numeric blur is queued before this button. Change only the requested key
            // against the latest document, so committing a sibling field does not turn
            // a valid click into a stale-source rejection. No disk write is involved.
            var edits=new List<KeyValuePair<string,string>>();
            if(pendingNoteKey!=null&&pendingNoteKey!=key)
            {
                double x;
                if(!double.TryParse(pendingNoteValue,NumberStyles.Float,CI,out x)||double.IsNaN(x)||double.IsInfinity(x))
                {status=L("请先补全数字，再选择其他参数。","Complete the numeric field before changing another parameter.");return;}
                edits.Add(new KeyValuePair<string,string>(pendingNoteKey,pendingNoteValue));
            }
            edits.Add(new KeyValuePair<string,string>(key,value));pendingNoteKey=null;
            QueueGui(()=>
            {
                if(History!=history||!ids.OrderBy(x=>x).SequenceEqual(selection.OrderBy(x=>x))||
                   ids.Any(id=>!History.Document.Contains(id))||events.Any(n=>ids.Contains(n.SourceId)&&n.Kind!=kind))
                {status=L("选择或音符已变化，未覆盖新内容。","Selection/source changed; no stale field overwrite.");noteForm=null;return;}
                if(Change(d=>{foreach(var field in edits)SelectionAuthoring.Field(d,ids,kind,field.Key,field.Value);}))
                {noteForm=null;status=L("参数已即时更新内存；Ctrl+S / 保存才写入整个工程。","Parameters updated in MEMORY; Save / Ctrl+S writes the whole project.");}
                else noteForm=null; // Re-read actual values after a rejected atomic edit.
            });
        }
        private void FlushNoteInput(bool force=false)
        {if(force&&pendingNoteKey!=null)CommitPendingNoteNow();}
        private bool CommitPendingNoteNow()
        {
            if(pendingNoteKey==null)return true;
            string key=pendingNoteKey,value=pendingNoteValue;double n;
            if(!double.TryParse(value,NumberStyles.Float,CI,out n)||double.IsNaN(n)||double.IsInfinity(n)){status=L("参数未完成，未保存。","Incomplete parameter: not saved.");return false;}
            if(noteForm==null||batchHistory!=History||batchSources.Any(kv=>!History.Document.Contains(kv.Key)||History.Document.SourceLine(kv.Key)!=kv.Value)){status="Parameter source changed; not saved.";return false;}
            if(!Change(d=>SelectionAuthoring.Field(d,batchIds,noteForm.Kind,key,value)))return false;
            pendingNoteKey=null;noteForm=null;return true;
        }
        private void DrawNotePanel()
        {
            var form=guiNoteForm;if(form==null||!sameNoteType)return;
            GUILayout.BeginArea(notePanelRect,GUI.skin.box);BeginPanelContent(ref noteScroll,notePanelRect);
            GUILayout.BeginHorizontal();GUILayout.Label(form.Kind.ToString().ToUpperInvariant()+" · "+(batchIds.Length==1?L("音符参数","NOTE"):L("批量参数 ×","BATCH ×")+batchIds.Length));
            if(GUILayout.Button("×",GUILayout.Width(24)))QueueGui(()=>SetSelection(new int[0]));GUILayout.EndHorizontal();
            GUILayout.Label(batchIds.Length==1?L("源行 ","Source line ")+guiPrimaryLine:L("空白字段代表不同值；只修改你操作的参数。","Blank = mixed values. Only the edited field changes."));
            bool old=GUI.enabled;GUI.enabled=old&&!transport.Playing&&!WorkflowActive;
            try
            {
                switch(form.Kind)
                {
                    case EventKind.Tap:LiveRow("start",L("时间 ms","Time ms"));LiveGround();break;
                    case EventKind.Hold:LiveRow("start",L("开始 ms","Start ms"));LiveRow("end",L("结束 ms","End ms"));LiveGround();break;
                    case EventKind.Flick:
                        LiveRow("start",L("时间 ms","Time ms"));LiveRow("x",L("中心 X %","Center X %"));LiveRow("width",L("宽度 %","Width %"));
                        LiveChoices("direction",L("划动方向","Direction"),new[]{L("向左 · 黄","Left · yellow"),L("向右 · 绿","Right · green")},new[]{"16","4"});break;
                    case EventKind.SkyArea:
                        LiveRow("start",L("开始 ms","Start ms"));LiveRow("end",L("结束 ms","End ms"));
                        LiveRow("sx",L("起点 X %","Start X %"));LiveRow("sw",L("起始宽度 %","Start width %"));
                        LiveRow("ex",L("终点 X %","End X %"));LiveRow("ew",L("结束宽度 %","End width %"));
                        if(batchIds.Length>1)LiveRow("bothWidths",L("统一首尾宽 %","Both widths %"));
                        string[] ease={L("直线","Linear"),"SineOut","SineIn"};
                        LiveChoices("leftEase",L("左边界形状","Left shape"),ease,new[]{"0","1","2"});
                        LiveChoices("rightEase",L("右边界形状","Right shape"),ease,new[]{"0","1","2"});
                        LiveRow("group",L("组 ID（-1无）","Group (-1 none)"));break;
                }
            }
            finally{GUI.enabled=old;}
            GUILayout.Space(5);GUILayout.Label(L("点击选项立即生效；数字按回车或离开输入框生效。\n这里只改内存，保存按钮才写盘。","Choices apply immediately; Enter / blur commits numbers.\nMEMORY only; Save writes disk."));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(L("跳转开头","Jump"))){double t=guiPrimary.TimeMs;QueueGui(()=>{CancelGesture();transport.Pause();transport.Seek(t);});}
            if(GUILayout.Button(L("复制","Copy")))QueueGui(()=>CopySelection(false));
            if(GUILayout.Button(L("删除","Delete")))QueueGui(DeleteSelected);GUILayout.EndHorizontal();
            long total=0,passed=0;if(comboTimeline!=null)foreach(int id in batchIds){var n=comboTimeline.Find(id);if(n!=null){total+=n.Total;passed+=n.At(CurrentMs);}}
            GUILayout.Label("EST. "+passed+" / "+total);
            if(batchIds.Length==1)
            {
                bool raw=GUILayout.Toggle(guiNoteSourceExpanded,L("高级：原始语句","Advanced: source"),GUI.skin.button);
                if(raw!=guiNoteSourceExpanded)QueueGui(()=>noteSourceExpanded=raw);
                if(guiNoteSourceExpanded)
                {
                    int id=form.SourceId;var history=History;string expected=form.OriginalSource;
                    noteRawText=CommittedSource("field_note_raw_"+id,form.OriginalSource,value=>
                    {
                        if(History!=history||!History.Document.Contains(id)||History.Document.SourceLine(id)!=expected)throw new InvalidOperationException("源语句已变化，请重新选择。 Source changed.");
                        if(!Change(d=>d.ReplaceSourceLine(id,value)))throw new ArgumentException(status);noteForm=null;
                    });
                    GUILayout.Label(L("回车或点击外面即应用到内存；保存才写盘。","Enter / blur applies to memory; Save writes disk."));
                }
            }
            EndPanelContent();GUILayout.EndArea();
        }
        private void LiveGround()
        {
            LiveRow("lane",L("起始轨 0～5","Start lane 0..5"));
            LiveChoices("width",L("中央轨宽度","Central width"),new[]{"1","2","3","4"},new[]{"1","2","3","4"});
            GUILayout.Label("0 Shift · 1 A · 2 S · 3 D · 4 F · 5 Space");
        }
        private void LiveRow(string key,string label)
        {
            string live;batchValues.TryGetValue(key,out live);live=live??"";
            var ids=(int[])batchIds.Clone();var history=History;var kind=noteForm.Kind;
            
            CommittedRow(label,"field_batch_"+string.Join("_",ids)+"_"+key,live,value=>
            {
                if(History!=history||ids.Any(id=>!History.Document.Contains(id)))throw new InvalidOperationException("谱面已变化，输入未应用。 Chart changed.");
                // Each action captures the source when the field is first drawn. Only its
                // own key is replaced; a previously committed sibling field stays intact.
                if(!Change(d=>SelectionAuthoring.Field(d,ids,kind,key,value)))throw new ArgumentException(status);
                noteForm=null;
            });
        }
        private void LiveChoices(string key,string label,string[] names,string[] values)
        {
            GUILayout.Label(label);GUILayout.BeginHorizontal();string current;batchValues.TryGetValue(key,out current);
            for(int i=0;i<names.Length;i++)if(GUILayout.Button((current==values[i]?"● ":"")+names[i]))QueueBatchField(key,values[i]);
            GUILayout.EndHorizontal();
        }
    }
}
