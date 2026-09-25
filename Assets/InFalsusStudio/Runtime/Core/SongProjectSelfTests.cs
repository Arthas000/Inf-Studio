using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // Real C# tests, executed ONLY by Unity's Run CSharp Core Checks or dotnet Runner.
    // Dummy .ogg bytes below test folder rules, NOT the actual audio decoder.
    public static class SongProjectSelfTests
    {
        private static bool Rejects(Action action)
        {try{action();return false;}catch(InvalidDataException){return true;}catch(IOException){return true;}catch(ArgumentException){return true;}}
        private static string Rat(int n,int d){return "{\"numerator\":"+n+",\"denominator\":"+d+",\"value\":0}";}
        private static string Note(int id,int type,int side,int start,int end,int x,int width,int flags)
        {
            return "{\"id\":"+id+",\"type\":"+type+",\"side\":"+side+",\"start_ms\":"+start+",\"end_ms\":"+end+
                ",\"start_x\":"+Rat(x,4)+",\"end_x\":"+Rat(x,4)+",\"start_width\":"+Rat(width,4)+",\"end_width\":"+Rat(width,4)+
                ",\"extra_flags\":"+flags+",\"packed_flags\":"+((type<<8)|side)+",\"relay\":0,\"group_id\":7}";
        }
        private static string ChartJson(string notes,string events="")
        {return "{\"format\":\"ICP1\",\"version\":1,\"bpm\":172,\"beats_per_bar\":4,\"notes\":["+notes+"],\"events\":["+events+"]}";}
        public static void Run(Action<bool,string> check)
        {
            Func<double,double,bool> near=(x,y)=>Math.Abs(x-y)<1e-8;
            var j=ProjectJson.Object(ProjectJson.Parse("\uFEFF{\"title\":\"音乐 \\u540d\",\"values\":[1,-2.5,3e2,true,null],\"s\":\"a\\nb\"}"));
            check(ProjectJson.Text(j,"title")=="音乐 名","song JSON unicode and BOM");
            check(ProjectJson.Array(ProjectJson.Get(j,"values")).Count==5,"song JSON heterogeneous array");
            check(ProjectJson.Text(ProjectJson.Object(ProjectJson.Parse(ProjectJson.Write(j))),"s")=="a\nb","song JSON writer roundtrip");
            foreach(string bad in new[]{"{\"a\":1,\"a\":2}","[1,]","{\"a\":NaN}","{\"a\":1e999}","[01]","{\"a\":1} garbage"})
                check(Rejects(()=>ProjectJson.Parse(bad)),"malformed JSON rejected: "+bad);
            check(IcpJsonImport.IsBinary(new byte[]{73,67,80,49,0})&&!IcpJsonImport.IsBinary(Encoding.UTF8.GetBytes("chart(100,4)")),"ICP1 binary distinguished from text");
            string fixture=ChartJson(string.Join(",",new[]{Note(0,1,1,100,100,2,3,0),Note(1,2,2,200,1200,0,1,0),Note(2,4,4,1400,1400,2,2,4096),Note(3,5,4,1600,2200,2,1,72)}),"{\"timestamp_ms\":500,\"type\":0,\"value\":0.3},{\"timestamp_ms\":0,\"type\":3,\"side\":2,\"enabled\":0}");
            var imported=IcpJsonImport.Convert(ProjectJson.Object(ProjectJson.Parse(fixture)));var es=imported.Document.ReadEvents();
            check(imported.ImportedNotes==4&&imported.SourceNotes==4,"decoded fixture all four note types imported");
            var tap=es.First(e=>e.Kind==EventKind.Tap);check(tap.Lane==2&&tap.GroundWidth==3,"ICP ground start_x is lane anchor, not center");
            check(es.First(e=>e.Kind==EventKind.Hold).Lane==0,"ICP side mask2 is left lane0");
            check(es.First(e=>e.Kind==EventKind.Flick).Get(4)==16,"ICP4096 to left Flick16");
            check(es.First(e=>e.Kind==EventKind.SkyArea).Get(7)==1,"declared import-profile72 to SineOut (profile, not game proof)");
            check(es.Any(e=>e.Kind==EventKind.Track&&near(e.Get(1),.3)),"ICP type0 scroll preserved");
            check(imported.Document.ToText().Contains("icp_event("),"unknown side visibility event retained, not fake lane index");
            int l,r;check(!IcpJsonImport.TryEase(0,out l,out r),"missing sky bits never silently Linear");
            foreach(int lc in new[]{4,8,16})foreach(int rc in new[]{32,64,128})check(IcpJsonImport.TryEase(lc|rc,out l,out r),"declared profile supports one-hot pair "+(lc|rc));
            var opaque=IcpJsonImport.Convert(ProjectJson.Object(ProjectJson.Parse(ChartJson(Note(9,9,4,0,100,1,1,0)))));
            check(opaque.ImportedNotes==0&&opaque.Document.ToText().Contains("icp_unknown_note("),"unknown note retained rather than fabricated");
            check(Rejects(()=>IcpJsonImport.Convert(ProjectJson.Object(ProjectJson.Parse(fixture.Replace("\"version\":1","\"version\":2"))))),"unknown ICP version fails");

            var motion=new AutoplayCursor();var e1=SpcDocument.Parse("chart(100,4)\nskyarea(1000,35,100,40,65,100,50,1,2,1000)\nflick(2400,25,100,10,16)\nflick(3000,75,100,30,4)\n").ReadEvents();motion.Rebuild(e1);
            var sky=e1.First(e=>e.Kind==EventKind.SkyArea);
            foreach(double t in new[]{1000d,1100,1500,1999})check(near(motion.Evaluate(t).X,ChartMath.SkyAt(sky,t).Center),"cursor exact asymmetric sky center @"+t);
            check(near(motion.Evaluate(2400).X,.25)&&motion.Evaluate(2450).X<.25,"left swipe begins center then left");
            check(near(motion.Evaluate(3000).X,.75)&&motion.Evaluate(3050).X>.75,"right swipe begins center then right");
            double measured=motion.Evaluate(1500).X;motion.Evaluate(50000);motion.Evaluate(0);check(near(motion.Evaluate(1500).X,measured),"cursor seeking/order independent");
            motion.Rebuild(SpcDocument.Parse("chart(100,4)\nskyarea(100,20,100,10,20,100,10,0,0,1000)\nskyarea(100,80,100,10,80,100,10,0,0,1000)\n").ReadEvents());
            check(motion.Evaluate(500).Conflicts==1,"incompatible simultaneous sky centers are surfaced");
            motion.Rebuild(new SpcEvent[0]);check(near(motion.Evaluate(0).X,.5),"empty cursor centered");

            string temp=Path.Combine(Path.GetTempPath(),"ifstudio_core_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
            try
            {
                check(Rejects(()=>SongFolderProject.Prepare(temp)),"zero song audio is error");
                string audio=Path.Combine(temp,"曲.mp3");File.WriteAllBytes(audio,new byte[]{1,2,3});
                var p=SongFolderProject.Prepare(temp);check(p.Difficulties.Length==4&&p.Difficulties.All(d=>d.Document.ToText()=="chart(100,4)\n"),"music-only defaults100bpm four blank slots");
                check(!File.Exists(p.ManifestPath),"Prepare is read-only (audio may still fail)");
                check(Rejects(()=>p.Resolve("../escape.spc"))&&Rejects(()=>p.Resolve("C:/escape.spc")),"metadata path traversal blocked");
                p.CommitInitialWorkspace();check(File.Exists(p.ManifestPath)&&p.Difficulties.All(d=>File.Exists(d.WorkingPath)),"initial workspace persisted");
                p.Difficulties[0].Document.Append("tap(600,1,1)");p.Difficulties[0].Document.Save(p.Difficulties[0].WorkingPath);p.ActiveDifficulty=2;p.WriteManifest();
                var reopened=SongFolderProject.Prepare(temp);check(reopened.Existing&&reopened.ActiveDifficulty==2&&reopened.Difficulties[0].Document.ToText().Contains("tap(600"),"reopen retains edited slot and active difficulty");
                check(File.ReadAllBytes(audio).SequenceEqual(new byte[]{1,2,3}),"audio bytes never rewritten");
                string extra=Path.Combine(temp,"extra.ogg");File.WriteAllBytes(extra,new byte[]{3});check(Rejects(()=>SongFolderProject.Prepare(temp,true)),"unclassified second audio rejected even with override");File.Delete(extra);
                File.Delete(reopened.Difficulties[1].WorkingPath);check(Rejects(()=>SongFolderProject.Prepare(temp)),"missing edited workspace never replaced with blank");

                string declared=Path.Combine(temp,"official");Directory.CreateDirectory(declared);File.WriteAllBytes(Path.Combine(declared,"enigma.ogg"),new byte[]{1});File.WriteAllBytes(Path.Combine(declared,"enigma_bga.ogg"),new byte[]{2});
                File.WriteAllText(Path.Combine(declared,"song.json"),"{\"title\":{\"default\":\"Enigma\"},\"artist\":{\"default\":\"sanmal\"},\"audio\":{\"path\":\"Songs/014_enigma/enigma.ogg\"},\"supplementary_audio\":[{\"path\":\"Songs/014_enigma/enigma_bga.ogg\"}],\"charts\":[]}");
                bool conflict=false;try{SongFolderProject.Prepare(declared);}catch(SongAudioConflict ex){conflict=ex.CanUseDeclaredPrimary&&ex.Files.Length==2;}
                check(conflict,"declared supplementary still requires explicit first confirmation");
                var confirmed=SongFolderProject.Prepare(declared,true);confirmed.CommitInitialWorkspace();check(SongFolderProject.Prepare(declared).AudioPath.EndsWith("enigma.ogg"),"confirmed manifest remembers main audio, preserves supplement");
                check(confirmed.Resolve("Songs/014_enigma/enigma.ogg",true)==Path.Combine(declared,"enigma.ogg"),"export paths work after moving/renaming song root");

                byte[] bytes=SpcDocument.Parse("chart(100,4)\n").ToBytes();string evidence=Path.Combine(temp,"evidence.json");ScoreEvidence.WriteTemplate(evidence,bytes);
                var score=ScoreEvidence.Load(evidence,bytes);check(!score.Maximum.HasValue&&score.At(5000)==null,"unknown ticks have neither fake max nor generated score");
                ScoreEvidence.WriteTemplate(evidence,bytes,123);score=ScoreEvidence.Load(evidence,bytes);check(score.Maximum==100000123L,"measured max applies user's stated formula only");
                check(Rejects(()=>ScoreEvidence.Load(evidence,Encoding.UTF8.GetBytes("chart(101,4)\n"))),"chart change invalidates measured score evidence");
                check(ScoreEvidence.Format(100000123L)=="100'000'123","nine-digit score grouping");
            }
            finally{try{Directory.Delete(temp,true);}catch{}}
        }
    }
}
