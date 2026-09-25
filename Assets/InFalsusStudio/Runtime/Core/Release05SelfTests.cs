using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // These are executable C# tests, not a claim they ran in the delivery container.
    public static class Release05SelfTests
    {
        private static bool Near(double x,double y){return Math.Abs(x-y)<1e-7;}
        private static bool Rejects(Action action)
        {try{action();return false;}catch(ArgumentException){return true;}catch(IOException){return true;}}
        private static List<SpcEvent> Chart(string body){return SpcDocument.Parse("chart(172,4)\n"+body).ReadEvents();}
        public static void Run(Action<bool,string> check)
        {
            var edge=Chart("skyarea(45916,1,12,2,1,12,2,2,1,5363,256)\n")[1];
            check(Near(ChartMath.SkyAt(edge,45916).Left,0),"05 tested left boundary normalizes to exactly zero");
            check(AirReachBounds.Violation(edge)==null,"05 tested edge is legal (not all-six-lane or screen bounds)");
            var original=SpcDocument.Parse("chart(100,4)\r\nflick(1000,-3,24,12,16) # preserve\r\n");
            byte[] originalBytes=original.ToBytes();
            check(AirReachBounds.Violation(original.ReadEvents()[1])!=null&&original.ToBytes().SequenceEqual(originalBytes),"05 imported out-of-bounds note is diagnosed, not rewritten");
            foreach(double width in new[]{.01,.1,.25,1.0})foreach(double center in new[]{-1.0,0,.5,1,2})
            {
                double c=AirReachBounds.CenterWithin(center,width);
                check(AirReachBounds.Inside(new SkyRange(c-width*.5,c+width*.5)),"05 bounded center width="+width+" target="+center);
            }
            check(Rejects(()=>AirReachBounds.CenterWithin(.5,1.1)),"05 over-full air width rejected, not shrunk");
            var doc=SpcDocument.Parse("chart(100,4)\nflick(1000,50,100,20,16)\n");
            EditOperations.Move(doc,new[]{1},0,0,2);var flick=doc.ReadEvents()[1];
            check(Near(ChartMath.FlickRange(flick).Right,1)&&Near(flick.Get(3),20),"05 air body movement clamps delta without resizing");
            EditOperations.AirEdge(doc,1,false,true,3);
            check(Near(ChartMath.FlickRange(doc.ReadEvents()[1]).Right,1),"05 edge cannot leave hard right limit");
            var fixedTail=SpcDocument.Parse("chart(100,4)\nskyarea(1000,50,100,100,1,2,2,0,0,1000)\n");
            EditOperations.Endpoint(fixedTail,1,true,2500,.9,0);
            check(Near(fixedTail.ReadEvents()[1].Get(4),1)&&Near(fixedTail.ReadEvents()[1].DurationMs,1500),"05 full-width tail stays centered; changing endpoint time still works");
            var hist=new EditHistory(doc.Clone());byte[] before=hist.Document.ToBytes();
            hist.Begin();hist.Preview(d=>EditOperations.Move(d,new[]{1},0,0,-3));hist.Commit();
            check(hist.Undo()&&hist.Document.ToBytes().SequenceEqual(before),"05 clipped movement remains a single byte-exact undo");
            var motion=new AutoplayCursor();
            var sample=Chart("skyarea(0,50,100,100,50,100,100,0,0,3000,1)\nflick(1000,20,100,20,16)\nflick(1000,80,100,20,16)\n");
            motion.Rebuild(sample);
            check(motion.MultiFlickClusterCount==1&&Near(motion.Evaluate(1000).X,.8),"05 simultaneous LEFT cluster starts at rightmost target, even inside Sky");
            check(motion.Evaluate(1040).X<.8&&motion.Evaluate(1040).X>.2&&motion.Evaluate(1099).X<.21,"05 left cluster continuously sweeps both targets instead of separate competing swipes");
            foreach(double t in Enumerable.Range(0,1201).Select(x=>x*2.5))
            {
                var pose=motion.Evaluate(t);check(pose.X>=0&&pose.X<=1,"05 sampled auto cursor hard bound @"+t);
            }
            check(motion.VisualContactTime(sample.First(e=>e.Kind==EventKind.Flick&&e.Get(1)==80))==1000&&motion.VisualContactTime(sample.First(e=>e.Kind==EventKind.Flick&&e.Get(1)==20))>1000,"05 simultaneous targets stay visible until their VISUAL contact, without changing SPC time");
            double stable=motion.Evaluate(1043.25).X;motion.Evaluate(0);motion.Evaluate(3000);
            check(Near(motion.Evaluate(1043.25).X,stable),"05 cursor random-access seeking is deterministic");
            motion.Rebuild(Chart("skyarea(0,50,100,100,50,100,100,0,0,3000)\nflick(1000,80,100,20,4)\nflick(1000,20,100,20,4)\n"));
            check(Near(motion.Evaluate(1000).X,.2)&&motion.Evaluate(1099).X>.79,"05 RIGHT cluster uses left-to-right (not hardcoded left sweep)");
            motion.Rebuild(Chart("skyarea(0,50,100,40,50,100,40,0,0,3000)\nflick(1000,55,100,20,16)\n"));
            check(Near(motion.Evaluate(1000).X,.55)&&motion.Evaluate(1050).X<.55&&Near(motion.Evaluate(1300).X,.5),"05 single Flick takes priority over center then returns to Sky");
            motion.Rebuild(Chart("skyarea(0,50,100,40,50,100,40,0,0,3000)\nflick(1000,50,100,20,16)\nflick(1000,50,100,20,4)\n"));
            check(motion.Evaluate(1050).Conflicts>0&&motion.Evaluate(1050).X>=.3&&motion.Evaluate(1050).X<=.7,"05 mixed-direction visual sequencing reports ambiguous simultaneous semantics");
            motion.Rebuild(Chart("skyarea(0,50,100,40,50,100,40,0,0,3000)\nskyarea(0,50,100,40,50,100,40,0,0,3000)\n"));
            check(motion.Evaluate(1000).Conflicts==0,"05 duplicate identical Sky constraints are not false conflicts (records not merged)");
            motion.Rebuild(Chart("skyarea(0,10,100,10,10,100,10,0,0,3000)\nflick(1000,90,100,10,16)\n"));
            check(motion.Evaluate(1040).Conflicts>0&&motion.Evaluate(1040).X<=.15,"05 unreachable Flick flagged, cursor does not leave active Sky to fake a hit");
            motion.Rebuild(Chart("flick(1000,0,100,10,16)\n"));
            check(motion.Evaluate(1000).X>motion.Evaluate(1099).X&&motion.Evaluate(1099).X>=0,"05 left-edge Flick can swipe within reachable footprint rather than outside world bounds");
            foreach(double scale in new[]{.1,.5,1.0,2.0,4.0})
            {
                double width=328*scale,height=50*scale;var boxes=ScoreHudLayout.Digits(width,height);
                check(boxes.Length==9&&boxes.All(b=>b.X>=0&&b.Y>=0&&b.X+b.W<=width&&b.Y+b.H<=height),"05 HUD nine digits fit inner artwork at scale "+scale);
                foreach(int i in new[]{2,5}){var s=ScoreHudLayout.Separator(width,height,i);check(s.X>=boxes[i].X+boxes[i].W&&s.X+s.W<=boxes[i+1].X,"05 separator has dedicated space at scale "+scale+" slot "+i);}
            }
            check(TickCountResearch.Count(EventKind.Tap,0,172)==1&&TickCountResearch.Count(EventKind.Flick,0,172)==1,"05 Tap/Flick count each record once");
            check(TickCountResearch.Count(EventKind.Hold,348,172)==3&&TickCountResearch.Count(EventKind.Hold,349,172)==3,"05 fitted count short Enigma Holds");
            check(TickCountResearch.Count(EventKind.SkyArea,1308,172)==8,"05 fitted first Enigma Minimal Sky count8");
            check(TickCountResearch.Count(EventKind.SkyArea,176,172)==1&&TickCountResearch.Count(EventKind.SkyArea,177,172)==2,"05 candidate endpoint policy explicitly exposed (not certified)");
            var fit=TickCountResearch.Analyze(Chart("tap(1000,1,1)\nhold(2000,1,1,348)\nskyarea(4000,50,100,10,50,100,10,0,0,1308)\nflick(6000,50,100,10,4)\n"));
            check(fit.Total==13&&!fit.Verified,"05 computed total alone never marks a new chart measured");
            check(TickCountResearch.Analyze(Chart("bpm(500,200,4)\nhold(0,1,1,1000)\n")).BpmChanges,"05 true BPM change flags onset-vs-dynamic uncertainty");
            check(!TickCountResearch.Analyze(Chart("bpm(500,172,4)\nhold(0,1,1,1000)\n")).BpmChanges,"05 redundant same-BPM events do not pretend samples test BPM changes");
            check(!TickCountResearch.Analyze(Chart("bpm(500,0,4)\n")).Supported,"05 special nonpositive BPM refuses provisional count");
            string source="\uFEFF{\r\n  \"title\":{\"default\":\"Enigma\",\"localized\":{\"English\":\"Enigma\",\"Japanese\":\"別名\",\"Korean\":\"\"}},\r\n  \"artist\":{\"default\":\"sanmal\",\"localized\":{\"English\":\"sanmal\"}},\r\n  \"huge\":-1753900809042789876,\"charts\":[{\"Difficulty\":8,\"Rating\":13,\"LevelSectionIndicator\":\"13\"},{\"Difficulty\":1,\"Rating\":4,\"LevelSectionIndicator\":\"4\"}],\r\n  \"song_info\":{\"Id\":14,\"ChartInfos\":[{\"Difficulty\":8,\"Rating\":13,\"LevelSectionIndicator\":\"13\"}]},\"unknown\":\"a\\\\b\"\r\n}";
            var patch=new LosslessJsonPatch(Encoding.UTF8.GetBytes(source));patch.DisplayText("title","New \"Song\"");patch.DisplayText("artist","作者");patch.Rating(3,"14");
            string expected=source.Replace("\"default\":\"Enigma\"","\"default\":\"New \\\"Song\\\"\"").Replace("\"English\":\"Enigma\"","\"English\":\"New \\\"Song\\\"\"").Replace("sanmal","作者").Replace("\"Rating\":13","\"Rating\":14").Replace("\"LevelSectionIndicator\":\"13\"","\"LevelSectionIndicator\":\"14\"");
            check(Encoding.UTF8.GetString(patch.ToBytes())==expected,"05 source token patch preserves every other byte, huge IDs/BOM/CRLF/translations included");
            check(Rejects(()=>patch.Rating(3,"FBD"))&&Rejects(()=>patch.Rating(3,"13.5")),"05 difficulty code and noninteger grade cannot be entered as numeric level");
            var missing=new LosslessJsonPatch(Encoding.UTF8.GetBytes("{\"unrelated\":9223372036854775807}"));
            check(Rejects(()=>missing.DisplayText("title","x")),"05 unknown metadata schema fails before file mutation");
            string temp=Path.Combine(Path.GetTempPath(),"ifstudio05_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
            try
            {
                // These fake audio bytes test filesystem policy, not MP3 decoding.
                string audio=Path.Combine(temp,"track.mp3");File.WriteAllBytes(audio,new byte[]{1,2,3});
                var project=SongFolderProject.Prepare(temp);project.CommitInitialWorkspace();
                File.WriteAllText(Path.Combine(temp,"song.json"),source,new UTF8Encoding(false));
                SongMetadataEdit.Apply(project,3,"New \"Song\"","作者","14");
                check(project.Title=="New \"Song\""&&project.Artist=="作者"&&project.Difficulties[3].Rating=="14","05 committed metadata reflected in memory");
                check(File.ReadAllText(Path.Combine(temp,"song.json"))==expected.TrimStart('\uFEFF'),"05 disk source reflects only authorized scalar changes");
                check(File.Exists(Path.Combine(temp,"song.json.bak"))&&File.ReadAllBytes(Path.Combine(temp,"song.json.bak")).SequenceEqual(Encoding.UTF8.GetBytes(source)),"05 exact original source backup preserved");
                check(File.ReadAllBytes(audio).SequenceEqual(new byte[]{1,2,3}),"05 metadata editing never touches audio");
                byte[] saved=File.ReadAllBytes(project.ManifestPath),song=File.ReadAllBytes(Path.Combine(temp,"song.json"));
                check(Rejects(()=>SongMetadataEdit.Apply(project,3,"x","y","bad"))&&File.ReadAllBytes(project.ManifestPath).SequenceEqual(saved)&&File.ReadAllBytes(Path.Combine(temp,"song.json")).SequenceEqual(song),"05 invalid input changes neither manifest nor source");
            }
            finally{try{Directory.Delete(temp,true);}catch{}}
        }
        public static string RunSamples(string researchFolder,string referenceJson)
        {
            int count=0;var text=new StringBuilder();
            foreach(string path in Directory.GetFiles(Path.Combine(researchFolder,"CountSamples"),"*.spc").OrderBy(p=>p,StringComparer.Ordinal))
            {
                var result=TickCountResearch.Analyze(SpcDocument.Load(path).ReadEvents(),referenceJson);
                if(!result.Verified)throw new InvalidOperationException("Measured reference mismatch: "+path);
                text.AppendLine("PASS "+Path.GetFileName(path)+": "+result.Total+" ["+result.Tap+","+result.Hold+","+result.SkyArea+","+result.Flick+"]");count++;
            }
            if(count!=5)throw new InvalidOperationException("Expected all five supplied reference charts.");
            return text.ToString();
        }
    }
}
