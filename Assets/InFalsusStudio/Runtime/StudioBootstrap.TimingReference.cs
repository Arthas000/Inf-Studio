using System;
using System.IO;
using System.Linq;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        private void ImportPairedTiming()
        {
            string path=StudioFileDialogs.Open(L("选择同一难度的原始明文 SPC（仅补入 BPM/拍号）","Select paired plaintext SPC (BPM/meter only)"),"","spc");
            if(string.IsNullOrEmpty(path))return;
            if(new FileInfo(path).Length>32L*1024*1024)throw new IOException("Reference chart exceeds 32 MiB.");
            var reference=SpcDocument.Load(path);
            string[] missing=TimingReferenceMerge.Prepare(History.Document,reference);
            if(missing.Length==0){status=L("配对谱面未提供缺失的 BPM 事件，无需修改。","No missing BPM events in this paired reference.");return;}
            string preview=string.Join("\n",missing.Take(16).ToArray());
            if(!StudioFileDialogs.Confirm(L("补入已知时序事件","Merge known timing events"),
                L("已验证音符已知语义一致。只向内存添加以下 BPM/拍号；未知原文不删除，音符不改动。全局保存才写文件。\n","Known note semantics match. Add these BPM/meter events in MEMORY only; opaque records and notes stay unchanged. Global Save writes files.\n")+preview+"\nTotal: "+missing.Length,"Merge","Cancel"))return;
            CancelGesture();transport.Pause();int count=0;
            if(Change(d=>count=TimingReferenceMerge.Apply(d,reference)))status=L("已补入 ","Merged ")+count+L(" 条 BPM/拍号到内存（一步撤销）；原始未知记录仍保留。"," BPM/meter events in memory (one undo); original opaque events retained.");
        }
    }
}
