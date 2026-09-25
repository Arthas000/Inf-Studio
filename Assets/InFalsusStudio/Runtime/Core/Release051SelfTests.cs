using System;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>Real C# tests, executed by CoreSelfTests in Unity/.NET; not claimed run by Python.</summary>
    public static class Release051SelfTests
    {
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
        private static bool Rejects(Action a){try{a();return false;}catch{return true;}}
        private static SpcEvent Sky(string line){return SpcDocument.Parse("chart(120,4)\n"+line+"\n").ReadEvents().First(e=>e.Kind==EventKind.SkyArea);}
        public static void Run(Action<bool,string> check)
        {
            foreach(bool right in new[]{false,true})foreach(double delta in new[]{-.6,-.1,.1,.6})
            {
                var g=new SkyEaseGeometry{Start=delta>0?.2:.8,End=delta>0?.2+delta:.8+delta,RightEdge=right};
                int left=g.FromDrag(0,-32),r=g.FromDrag(0,32);
                check(g.Midpoint(left)<g.Midpoint(0)&&g.Midpoint(r)>g.Midpoint(0),"051 geometry follows screen drag regardless of edge/onset-end direction");
                foreach(int initial in new[]{0,1,2})check(g.FromDrag(initial,0)==initial,"051 grab current curve without jump");
                check(g.FromDrag(left,32)==0&&g.FromDrag(r,-32)==0,"051 return to Linear from either direction");
            }
            var wall=Sky("skyarea(1000,20,100,40,10,100,20,2,1,1000)");
            check(GeometricSkyEase.Describe(wall,false).Pinned&&!GeometricSkyEase.Describe(wall,true).Pinned&&GeometricSkyEase.Describe(wall,true).CanCurve,"051 only wall-side boundary is fixed");
            var doc=SpcDocument.Parse("chart(120,4)\nskyarea(1000,20,100,40,10,100,20,2,1,1000)\n");
            EditOperations.SetEase(doc,new[]{1},false,1);EditOperations.SetEase(doc,new[]{1},true,2);
            check(doc.ReadEvents()[1].Get(7)==0&&doc.ReadEvents()[1].Get(8)==2,"051 pin normalization never disables opposite edge");
            EditOperations.Endpoint(doc,1,true,2000,.3,0);
            check(!GeometricSkyEase.Describe(doc.ReadEvents()[1],false).Pinned&&GeometricSkyEase.Describe(doc.ReadEvents()[1],false).CanCurve,"051 moving one endpoint immediately unlocks old wall-side handle");
            var rectangle=SpcDocument.Parse("chart(120,4)\nskyarea(1000,5,100,10,5,100,10,1,1,1000)\n");
            EditOperations.Move(rectangle,new[]{1},0,0,.3);
            var shape=GeometricSkyEase.Describe(rectangle.ReadEvents()[1],false);
            check(!shape.Pinned&&shape.Stationary,"051 translating whole constant-X edge is not a stale boundary lock");
            string original="chart(120,4)\nskyarea(1000,20,100,20,70,100,20,0,0,1000,10)\n";
            var history=new EditHistory(SpcDocument.Parse(original));history.Begin();history.Preview(d=>EditOperations.SetEase(d,new[]{1},false,2));history.Commit();
            check(history.Undo()&&Encoding.UTF8.GetString(history.Document.ToBytes())==original,"051 intuitive curve remains one byte-exact undo");

            var es=SpcDocument.Parse("chart(172,4)\ntap(100,1,1)\nflick(100,50,100,10,16)\nhold(1000,1,1,349)\nskyarea(2000,50,100,10,50,100,10,0,0,1308)\n").ReadEvents();
            var tl=new ProvisionalComboTimeline(es);
            check(tl.Supported&&tl.Totals.Total==13,"051 timeline total equals candidate A summary");
            check(tl.At(99).Total==0&&tl.At(100).Total==2,"051 simultaneous independent Tap/Flick count at original timestamp");
            var hold=tl.Notes.First(n=>n.Kind==EventKind.Hold);var sky=tl.Notes.First(n=>n.Kind==EventKind.SkyArea);
            check(hold.Total==3&&hold.TickTime(0)==1000&&hold.TickTime(1)==1175&&hold.TickTime(2)==1349,"051 explicit provisional Hold head/interior/tail schedule");
            check(hold.At(999)==0&&hold.At(1000)==1&&hold.At(1174.999)==1&&hold.At(1175)==2&&hold.At(1349)==3,"051 Hold inclusive tick boundary");
            check(sky.Total==8&&sky.TickTime(7)==3225&&sky.At(3308)==8,"051 explicit provisional Sky start/periodic schedule");
            int last=0;foreach(int t in Enumerable.Range(0,3600)){int now=tl.At(t).Total;check(now>=last&&now<=13,"051 count monotone at "+t);last=now;}
            check(tl.At(1500).Total==5&&tl.At(50).Total==0&&tl.At(1500).Total==5,"051 random seek has no accumulated state");
            var changed=SpcDocument.Parse("chart(120,4)\nbpm(2500,240,4)\nhold(1000,1,1,3000)\nhold(3000,2,1,1000)\ntrack(2000,0.1)\ntrack(2600,2)\n").ReadEvents();
            var crossing=new ProvisionalComboTimeline(changed);
            check(crossing.Notes[0].StartBpm==120&&crossing.Notes[0].IntervalMs==250&&crossing.Notes[0].CrossesBpm&&crossing.Notes[0].Total==13,"051 true BPM crossing uses documented provisional onset rule");
            check(crossing.Notes[1].StartBpm==240&&crossing.Notes[1].Total==9,"051 following Hold uses its own onset BPM");
            var trackOnly=new ProvisionalComboTimeline(SpcDocument.Parse("chart(130,4)\nhold(22154,0,1,3231)\nhold(22154,5,1,3231)\ntrack(22154,0.3)\ntrack(25385,1)\n").ReadEvents());
            check(trackOnly.Totals.Hold==30&&trackOnly.Notes.All(n=>n.IntervalMs==231&&!n.CrossesBpm),"051 LightsOut two changing-scroll Holds are 15+15 candidate ticks");
            check(!new ProvisionalComboTimeline(SpcDocument.Parse("chart(120,4)\nbpm(500,0,4)\n").ReadEvents()).Supported,"051 nonpositive BPM is not silently assigned ticks");
            ProjectJson.Parse(tl.Report(1349,"example-hash"));

            string root=Path.Combine(Path.GetTempPath(),"ifstudio051_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                // Fake audio bytes test filesystem semantics only, not decoding.
                File.WriteAllBytes(Path.Combine(root,"music.mp3"),new byte[]{1,2,3});
                var p=SongFolderProject.Prepare(root);p.CommitInitialWorkspace();
                string src="\uFEFF{\r\n\"title\":\"Old\",\"artist\":\"Artist\",\"huge\":9223372036854775807,\"charts\":[{\"Difficulty\":8,\"Rating\":13,\"LevelSectionIndicator\":\"13\"}]}\r\n";
                string source=Path.Combine(root,"song.json");File.WriteAllBytes(source,Encoding.UTF8.GetBytes(src));
                var session=new ManualSongSession(p);byte[] m0=File.ReadAllBytes(p.ManifestPath),s0=File.ReadAllBytes(source),c0=File.ReadAllBytes(p.Difficulties[0].WorkingPath),c3=File.ReadAllBytes(p.Difficulties[3].WorkingPath);
                string diskTitle=p.Title;
                SongMetadataEdit.Stage(p,3,"Draft","Draft artist","14");p.Difficulties[0].Document.Append("tap(1000,1,1)");
                p.ActiveDifficulty=3;p.Difficulties[3].Document.Append("hold(2000,1,1,1000)");
                check(session.HasChanges&&session.ChartDirty(0)&&session.ChartDirty(3)&&session.MetadataDirty,"051 draft dirty tracks all four slots plus metadata");
                check(File.ReadAllBytes(p.ManifestPath).SequenceEqual(m0)&&File.ReadAllBytes(source).SequenceEqual(s0)&&File.ReadAllBytes(p.Difficulties[0].WorkingPath).SequenceEqual(c0)&&File.ReadAllBytes(p.Difficulties[3].WorkingPath).SequenceEqual(c3),"051 staging/switching changes zero project bytes");
                var reopened=SongFolderProject.Prepare(root);
                check(reopened.Title==diskTitle&&reopened.Difficulties.All(d=>!d.Document.ReadEvents().Any(e=>e.IsNote)),"051 reload without Save observes last saved state, not memory drafts");
                session.Save();
                check(!session.HasChanges&&File.ReadAllBytes(p.Difficulties[0].WorkingPath).SequenceEqual(p.Difficulties[0].Document.ToBytes())&&File.ReadAllBytes(p.Difficulties[3].WorkingPath).SequenceEqual(p.Difficulties[3].Document.ToBytes()),"051 explicit Save commits all modified difficulties");
                check(File.ReadAllText(source).Contains("9223372036854775807")&&File.ReadAllText(source).Contains("Draft artist")&&File.ReadAllBytes(source).Take(3).SequenceEqual(new byte[]{239,187,191}),"051 manual metadata save preserves huge integer + BOM and edits proper tokens");
                check(SongFolderProject.Prepare(root).Title=="Draft","051 saved metadata survives reopen");
                byte[] saved0=File.ReadAllBytes(p.Difficulties[0].WorkingPath),savedManifest=File.ReadAllBytes(p.ManifestPath),savedSource=File.ReadAllBytes(source);
                p.Difficulties[0].Document.Append("tap(1500,1,2)");SongMetadataEdit.Stage(p,3,"Second draft","Second artist","15");
                check(Rejects(()=>session.Save((i,path)=>{if(i==1)throw new IOException("Injected write failure");})),"051 injected multi-file failure reported");
                check(File.ReadAllBytes(p.Difficulties[0].WorkingPath).SequenceEqual(saved0)&&File.ReadAllBytes(p.ManifestPath).SequenceEqual(savedManifest)&&File.ReadAllBytes(source).SequenceEqual(savedSource)&&session.HasChanges,"051 partial save rolls back previous replacement and retains memory drafts");
                File.AppendAllText(p.Difficulties[0].WorkingPath,"external(1)\n");
                check(Rejects(()=>session.Save())&&File.ReadAllText(p.Difficulties[0].WorkingPath).Contains("external(1)"),"051 external chart edit is not silently overwritten");
                check(File.ReadAllBytes(source).SequenceEqual(savedSource)&&File.ReadAllBytes(p.ManifestPath).SequenceEqual(savedManifest),"051 conflict preflight prevents metadata half-save");
                string draftTitle=p.Title;
                check(Rejects(()=>SongMetadataEdit.Stage(p,3,"Changed","Artist","bad"))&&p.Title==draftTitle,"051 validation before staging any metadata");
            }
            finally{try{Directory.Delete(root,true);}catch{}}
        }
    }
}
