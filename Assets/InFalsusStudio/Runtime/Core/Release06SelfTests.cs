using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    // Real C# assertions. These are provided for Unity/.NET execution; the delivery
    // environment's Python checks are not a substitute for executing this class.
    public static class Release06SelfTests
    {
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
        private static bool Rejects(Action action){try{action();return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
        public static void Run(Action<bool,string> check,byte[] lightsOut=null)
        {
            var cursor=new AutoplayCursor();
            var symmetric=SpcDocument.Parse("chart(130,4)\nflick(70000,0,100,10,16)\nskyarea(73846,24,48,48,24,48,46,1,1,231,547)\nskyarea(74308,24,48,40,24,48,38,1,1,230,548)\nskyarea(74769,24,48,32,24,48,30,1,1,173,549)\nskyarea(75231,24,48,24,24,48,22,1,1,173,550)\nskyarea(75692,24,48,16,24,48,14,1,1,58,551)\nskyarea(76154,24,48,8,24,48,6,1,1,58,552)\nskyarea(76615,24,48,4,24,48,2,1,1,58,553)\nskyarea(77077,24,48,4,24,48,2,1,1,58,554)\n").ReadEvents();
            cursor.Rebuild(symmetric);
            foreach(var sky in symmetric.Where(e=>e.Kind==EventKind.SkyArea))
            {
                foreach(double offset in new[]{-0.001,0.0,0.001,1.0,20.0,40.0})
                    check(Near(cursor.Evaluate(sky.EndMs+offset).X,.5),"060 centered Sky exit/gap "+sky.EndMs+" + "+offset);
            }
            check(Near(cursor.Evaluate(74550).X,.5)&&Near(cursor.Evaluate(74080).X,.5)&&Near(cursor.Evaluate(74550).X,.5),"060 cursor seeking is deterministic, not delta-state");
            cursor.Rebuild(SpcDocument.Parse("chart(120,4)\nflick(1000,5,100,10,16)\nskyarea(2000,30,100,10,75,100,10,0,0,1000)\n").ReadEvents());
            check(Near(cursor.Evaluate(3000).X,.75)&&Near(cursor.Evaluate(3500).X,.75),"060 asymmetric completed Sky retains its real tail, not center or old Flick");
            cursor.Rebuild(SpcDocument.Parse("chart(120,4)\nskyarea(2000,50,100,60,50,100,60,0,0,1000)\nflick(2920,50,100,10,4)\n").ReadEvents());
            check(Math.Abs(cursor.Evaluate(2999.999).X-cursor.Evaluate(3000).X)<.0001,"060 Sky exit during swipe remains continuous");
            cursor.Rebuild(SpcDocument.Parse("chart(120,4)\nskyarea(2000,50,100,60,50,100,60,0,0,1025)\nflick(2900,50,100,10,4)\n").ReadEvents());
            check(Math.Abs(cursor.Evaluate(3024.999).X-cursor.Evaluate(3025).X)<.0001,"060 Sky exit during return retains constrained exit pose");
            var cluster=SpcDocument.Parse("chart(120,4)\nskyarea(0,50,100,100,50,100,100,0,0,4000)\nflick(2000,80,100,20,16)\nflick(2000,20,100,20,16)\n").ReadEvents();cursor.Rebuild(cluster);
            check(cursor.Evaluate(2000).X>.7&&cursor.Evaluate(2050).X<.6&&cursor.Evaluate(2099).X<.21,"060 same-time left Flick sweep remains right-to-left");
            if(lightsOut!=null)
            {
                var actual=SpcDocument.FromBytes(lightsOut).ReadEvents();cursor.Rebuild(actual);
                foreach(var sky in actual.Where(e=>e.Kind==EventKind.SkyArea&&e.TimeMs>=73846&&e.TimeMs<=77077))
                    check(Near(cursor.Evaluate(sky.EndMs).X,.5)&&Near(cursor.Evaluate(sky.EndMs+1).X,.5),"060 actual LightsOut tail "+sky.EndMs);
            }

            var grid=new AuthoringGrid(new BeatTimeline(SpcDocument.Parse("chart(120,4)\nbpm(1123,150,3)\nbpm(4000,100,5)\n").ReadEvents()));
            var times=grid.BarsBetween(0,10000).Select(x=>x.TimeMs).ToArray();
            check(times.SequenceEqual(new[]{0.0,1123.0,2323.0,3523.0,4000.0,7000.0,10000.0}),"060 future BPM origins and meter changes visible before judge line");
            check(!times.Contains(2000)&&!times.Contains(4723),"060 no old bar phase leaks across timing reset");
            foreach(int n in new[]{4,6,8})
                check(grid.Between(0,10000,n).Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(times),"060 bar membership agrees with snap subdivision "+n);
            var fractional=new AuthoringGrid(new BeatTimeline(SpcDocument.Parse("chart(130,3.5)\n").ReadEvents()));
            check(fractional.BarsBetween(0,5000).Select(x=>x.TimeMs).SequenceEqual(new[]{0.0,1615.0,3231.0,4846.0}),"060 fractional meter bars from absolute index");
            var same=new AuthoringGrid(new BeatTimeline(SpcDocument.Parse("chart(120,4)\nbpm(1000,120,3)\nbpm(1000,240,2)\n").ReadEvents()));
            check(same.BarsBetween(1000,2000).Select(x=>x.TimeMs).SequenceEqual(new[]{1000.0,1500.0,2000.0}),"060 equal-time timing resolves deterministically");

            check(PreviewScore.At(0,1334)==0&&PreviewScore.At(1334,1334)==100001334,"060 score zero/full endpoint exactly matches declared ceiling");
            check(PreviewScore.At(1,3)==33333334&&PreviewScore.At(2,3)==66666668&&PreviewScore.At(3,3)==100000003,"060 score uses rational total, no rounded per-hit accumulation");
            check(PreviewScore.At(0,0)==0&&PreviewScore.At(-1,3)==0&&PreviewScore.At(4,3)==100000003,"060 empty/negative/over-combo score guards");
            check(Rejects(()=>PreviewScore.At(1,-1)),"060 negative total rejected");
            long previous=-1;for(int i=0;i<=1334;i++){long s=PreviewScore.At(i,1334);if(s<previous)throw new Exception("060 score monotonicity");previous=s;}
            check(previous==100001334,"060 score monotone across 1335 cumulative points");

            var history=new EditHistory(SpcDocument.Parse("chart(130,4)\r\n  tap(1000.125000, 3 , 2,99) # tail\r\nopaque(9223372036854775807)\n"));
            string original=history.Document.ToText();var form=NoteProperties.Capture(history.Document,history.Document.ReadEvents().First(e=>e.IsNote));
            history.Execute(d=>form.Apply(d));check(history.UndoCount==0&&history.Document.ToText()==original,"060 untouched form is a byte-preserving no-op");
            form.Values["width"]="2";history.Execute(d=>form.Apply(d));
            check(history.Document.SourceLine(form.SourceId)=="  tap(1000.125000, 2 , 2,99) # tail"&&history.Document.SourceLine(2)=="opaque(9223372036854775807)","060 Tap width changes only its token; no time quantization or unknown loss");
            check(history.UndoCount==1&&history.Undo()&&history.Document.ToText()==original,"060 note form is one reversible RAM transaction");
            var hold=new EditHistory(SpcDocument.Parse("chart(130,4)\nhold(1000,2,2,500)\n"));var hf=NoteProperties.Capture(hold.Document,hold.Document.ReadEvents()[1]);hf.Values["start"]="1100";hold.Execute(d=>hf.Apply(d));
            check(hold.Document.ReadEvents()[1].EndMs==1500&&hold.Document.ReadEvents()[1].DurationMs==400,"060 Hold start field keeps absolute tail fixed");
            var skyHistory=new EditHistory(SpcDocument.Parse("chart(130,4)\nskyarea(1846.123456789,100,200,2,1,2,2,1,1,923)\n"));
            var sf=NoteProperties.Capture(skyHistory.Document,skyHistory.Document.ReadEvents()[1]);sf.Values["sw"]="10";sf.Values["leftEase"]="2";sf.Values["group"]="9";skyHistory.Execute(d=>sf.Apply(d));
            var se=skyHistory.Document.ReadEvents()[1];
            check(se.Get(2)==200&&se.Get(5)==2&&se.Get(3)==20&&se.Get(6)==2&&se.Get(7)==2&&se.Get(8)==1&&se.Get(10)==9,"060 Sky percent conversion preserves both splits, other endpoint/ease and appends optional group");
            check(skyHistory.Document.ArgumentTokens(1)[0]=="1846.123456789","060 Sky width-only edit preserves exact original time token");
            string before=skyHistory.Document.ToText();sf=NoteProperties.Capture(skyHistory.Document,se);sf.Values["sx"]="0";sf.Values["sw"]="20";
            check(Rejects(()=>skyHistory.Execute(d=>sf.Apply(d)))&&skyHistory.Document.ToText()==before,"060 out-of-range friendly form atomically rejected");
            sf=NoteProperties.Capture(skyHistory.Document,se);skyHistory.Execute(d=>d.SetNumber(1,9,500));
            check(Rejects(()=>skyHistory.Execute(d=>sf.Apply(d))),"060 stale form cannot overwrite concurrent handle edit");
            var fl=new EditHistory(SpcDocument.Parse("chart(130,4)\nflick(2000,12,24,6,16,77)\n"));var ff=NoteProperties.Capture(fl.Document,fl.Document.ReadEvents()[1]);ff.Values["direction"]="4";ff.Values["width"]="50";fl.Execute(d=>ff.Apply(d));
            check(fl.Document.SourceLine(1)=="flick(2000,12,24,12,4,77)","060 Flick width/direction changes preserve center, time, split and extra fields");

            var soundSource=SpcDocument.Parse("chart(120,4)\ntap(100,1,1)\ntap(200,1,0)\ntap(300,1,5)\nflick(400,25,100,10,16)\nflick(400,75,100,10,16)\nhold(1000,0,1,2000)\nhold(1500,5,1,2000)\nskyarea(2000,50,100,20,50,100,20,0,0,1000)\nskyarea(3000,50,100,20,50,100,20,0,0,1000)\nskyarea(5000,50,100,20,50,100,20,0,0,500)\n").ReadEvents();
            var sounds=new HitSoundPlan(soundSource);
            check(sounds.Shots.Count(c=>c.Kind==HitSoundKind.FloorHit)==1&&sounds.Shots.Count(c=>c.Kind==HitSoundKind.SideHit)==2,"060 central vs left/right Tap sound routing");
            check(sounds.Shots.Count(c=>c.Kind==HitSoundKind.Flick&&c.Start==400)==2,"060 simultaneous Flick attacks are both retained");
            check(sounds.Shots.Count(c=>c.Kind==HitSoundKind.SkyHit)==3,"060 default Sky attack at each segment head");
            check(sounds.Holds.Count(c=>c.Kind==HitSoundKind.FloorHold)==1&&sounds.Holds.First(c=>c.Kind==HitSoundKind.FloorHold).Start==1000&&sounds.Holds.First(c=>c.Kind==HitSoundKind.FloorHold).End==3500,"060 all six ground Hold spans share one union bed");
            check(sounds.Holds.Count(c=>c.Kind==HitSoundKind.SkyHold)==2&&sounds.Holds.Count(c=>c.Start<=2500&&c.End>2500)==2,"060 FLOOR + SKY are two simultaneous independent hold beds");
            check(new HitSoundPlan(soundSource,SkyAttackPolicy.ContinuousRegionStart).Shots.Count(c=>c.Kind==HitSoundKind.SkyHit)==3,"080 ungrouped segments remain independent heads (supersedes 060 geometric policy)");
            check(new HitSoundPlan(soundSource,SkyAttackPolicy.Off).Shots.All(c=>c.Kind!=HitSoundKind.SkyHit),"060 Sky attack disabled independently of continuous bed");
            check(new HitSoundPlan(soundSource,SkyAttackPolicy.EverySegment,true).Shots.Count(c=>c.Kind==HitSoundKind.SideHit)==4,"060 optional ground Hold head attack routing");
            check(HitSoundPlan.LowerBound(sounds.Shots,400)==3&&HitSoundPlan.LowerBound(sounds.Shots,400.001)==5,"060 seek lower-bound includes exact current shots, not past ones");
        }
    }
}
