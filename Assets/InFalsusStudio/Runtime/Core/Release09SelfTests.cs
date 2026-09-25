using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>Run via Unity CoreSelfTests, or the existing net8 Tests/CoreChecks project.</summary>
    public static class Release09SelfTests
    {
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
        private static bool Throws(Action action){try{action();return false;}catch{return true;}}
        public static void Run(Action<bool,string> check)
        {
            string head="chart(174,4)\n";
            string n1="skyarea(1000.25,1,2,1,1,2,1,0,0,500,7)\n";
            string n2="skyarea(2000,1,2,1,1,2,1,0,0,500,7)\n";
            string n3="skyarea(2000,1,2,1,1,2,1,0,0,300,8)\n";
            var d=SpcDocument.Parse(head+n2+n3+n1+"flick(1500,1,2,1,16)\n");var ev=d.ReadEvents();var bytes=d.ToBytes();
            check(SkyGroupNavigation.Members(ev,7).Select(e=>e.TimeMs).SequenceEqual(new[]{1000.25,2000d}),"090 group sorted by source start time, not document order");
            check(SkyGroupNavigation.Neighbor(ev,7,0,1).TimeMs==1000.25,"090 next preserves off-grid source time");
            check(SkyGroupNavigation.Neighbor(ev,7,1000.25,1).TimeMs==2000,"090 strict next excludes current timestamp");
            check(SkyGroupNavigation.Neighbor(ev,7,2000,-1).TimeMs==1000.25,"090 strict previous");
            check(SkyGroupNavigation.Neighbor(ev,7,2000,1)==null,"090 no implicit wrap");
            check(SkyGroupNavigation.Members(ev,99).Length==0&&SkyGroupNavigation.Members(ev,-1).Length==0,"090 empty and ungrouped not fabricated");
            check(d.ToBytes().SequenceEqual(bytes),"090 group browse does not edit chart");
            foreach(double rate in new[]{1d,.75,.5,.25})
            {
                check(KeySoundClock.Pitch==1,"090 sample pitch 1 at chart rate "+rate);
                check(Near(KeySoundClock.OneShotEnd(100,3.564),103.564),"090 attack DSP duration independent from rate "+rate);
                check(KeySoundClock.LoopSample(1500,rate,48000,288000)==(int)((1.5/rate*48000)%288000),"090 sustain restore phase uses real seconds "+rate);
            }
            check(KeySoundClock.LoopSample(1000,0,48000,288000)==0,"090 invalid sample clock rate guarded");
            check(KeySoundClock.LoopSample(double.NaN,1,48000,288000)==0,"090 invalid elapsed time guarded");
            var png=new byte[24];png[0]=137;png[1]=80;png[2]=78;png[3]=71;png[12]=73;png[13]=72;png[14]=68;png[15]=82;png[18]=8;png[22]=4;
            check(!Throws(()=>LocalImageSize.Validate(png,8192,33554432)),"090 PNG allocation preflight 2048x1024");
            png[16]=127;check(Throws(()=>LocalImageSize.Validate(png,8192,33554432)),"090 reject enormous image before decode");
            check(Throws(()=>LocalImageSize.Validate(new byte[2],8192,33554432)),"090 malformed image preflight");
            var crop=BackgroundFit.Cover(2,16d/9);
            check(Near(crop.Width,8d/9)&&Near(crop.X,1d/18)&&crop.Height==1,"090 supplied 2048x1024 background covers 16:9 by horizontal crop");
            var match=BackgroundFit.Cover(16d/9,16d/9);check(match.X==0&&match.Y==0&&match.Width==1&&match.Height==1,"090 matching background aspect exact full UV");
            var portrait=BackgroundFit.Cover(1,16d/9);check(Near(portrait.Height,9d/16)&&portrait.Width==1,"090 portrait texture cropped vertically");
            check(Throws(()=>BackgroundFit.Cover(0,1)),"090 invalid background aspect rejected");
            string bpm="bpm(137931,174.0,99.0)\nbpm(140000,174.0,4.0)\nbpm(140690,174.0,4.0)\n";
            var reference=SpcDocument.Parse(head+n1+n2+bpm);
            var target=SpcDocument.Parse(head+n1+n2+"icp_event(137931,1,e30=)\n");
            var original=target.ToBytes();var history=new EditHistory(target);
            check(TimingReferenceMerge.Prepare(target,reference).Length==3,"090 paired plaintext exposes three missing timing events");
            check(target.ToBytes().SequenceEqual(original),"090 Prepare never mutates destination");
            int added=0;history.Execute(x=>added=TimingReferenceMerge.Apply(x,reference));
            check(added==3&&history.UndoCount==1,"090 repair is one undo transaction");
            check(history.Document.ReadEvents().Count(e=>e.Kind==EventKind.Bpm)==3,"090 repaired typed BPM events");
            check(history.Document.ToText().Contains("icp_event(137931,1,e30=)"),"090 opaque payload is never silently deleted");
            check(TimingReferenceMerge.Prepare(history.Document,reference).Length==0,"090 repeated repair is idempotent");
            check(history.Undo()&&history.Document.ToBytes().SequenceEqual(original),"090 repair undo byte exact");
            var conflict=target.Clone();conflict.Append("bpm(140000,173,4)");var cb=conflict.ToBytes();
            check(Throws(()=>TimingReferenceMerge.Apply(conflict,reference))&&conflict.ToBytes().SequenceEqual(cb),"090 late conflict rejects entire repair before first write");
            var changed=target.Clone();changed.SetNumber(1,0,1001);
            check(Throws(()=>TimingReferenceMerge.Prepare(changed,reference)),"090 edited/wrong chart not matched by filename alone");
            var wrong=SpcDocument.Parse("chart(175,4)\n"+n1+n2);check(Throws(()=>TimingReferenceMerge.Prepare(wrong,reference)),"090 mismatched header rejected");
            var equal=SpcDocument.Parse(head+"skyarea(1000.25,50,100,50,50,100,50,0,0,500,7)\n"+n2);
            check(TimingReferenceMerge.Prepare(equal,reference).Length==3,"090 rational equivalent paired notes accepted");
            var unknown=target.ReadEvents().First(x=>x.Name=="icp_event");check(TimingReferenceMerge.DescribeOpaque(target,unknown).Contains("137931"),"090 opaque report exposes original time/type without interpretation");
            var grid=new AuthoringGrid(new BeatTimeline(reference.ReadEvents()));
            var bars=grid.BarsBetween(137930,142100).Select(x=>x.TimeMs).ToArray();
            check(bars.Contains(137931)&&bars.Contains(140000)&&bars.Contains(140690),"090 coldsea meter99 and both new timing origins remain visible");
            // Positive named decoded schema may be consumed; generic byte payloads stay inert.
            string json="{\"format\":\"ICP1\",\"version\":1,\"bpm\":174,\"beats_per_bar\":4,\"notes\":[],\"events\":[{\"type\":88,\"timestamp_ms\":1000,\"bpm\":174,\"beats_per_bar\":99},{\"type\":88,\"timestamp_ms\":2000,\"payload\":\"AAAA\"}]}";
            var imported=IcpJsonImport.Convert(ProjectJson.Object(ProjectJson.Parse(json))).Document.ReadEvents();
            check(imported.Any(x=>x.Kind==EventKind.Bpm&&x.Get(2)==99),"090 only explicitly named BPM/meter accepted");
            check(imported.Any(x=>x.Name=="icp_event"&&x.TimeMs==2000),"090 anonymous binary fields still preserved, never guessed");
        }
    }
}
