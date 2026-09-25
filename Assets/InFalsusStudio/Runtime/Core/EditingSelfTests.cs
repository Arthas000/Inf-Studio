using System;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // Real C# regression suite, invoked by CoreSelfTests.Run in Unity or dotnet.
    // Provided with the package; no claim that this ran without a C# runtime.
    public static class EditingSelfTests
    {
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-7;}
        private static SpcEvent Get(SpcDocument d,int id){return d.ReadEvents().First(e=>e.SourceId==id);}
        private static bool Throws(Action a){try{a();return false;}catch(ArgumentException){return true;}catch(InvalidDataException){return true;}}
        public static void Run(Action<bool,string> check)
        {
            string source="chart(120.000, 4)\r\n tap(1000.00,1,1) # untouched\n"
                         +"hold(2000,2,2,500)\r\nbeam(foo,bar)\n";
            var d=SpcDocument.Parse(source,true);int tap=1,hold=2;
            var clone=d.Clone();clone.Delete(tap);
            check(clone.SourceIndex(hold)==1&&Get(clone,hold).Kind==EventKind.Hold,"EDIT stable identity after deletion");
            check(clone.SourceLine(3)=="beam(foo,bar)"&&d.ToText()==source,"EDIT clone owns lines; opaque line retained");
            int appended=clone.Append("tap(3000,1,1)");
            check(appended==4&&clone.SourceIndex(appended)==3,"EDIT append identity does not reuse deleted ID");
            var same=d.Clone();same.SetNumber(tap,0,1000);
            check(same.ToBytes().SequenceEqual(d.ToBytes()),"EDIT numeric no-op preserves original token spelling");
            var h=new EditHistory(d.Clone());h.Execute(x=>x.SetNumber(tap,0,1000));
            check(h.UndoCount==0,"EDIT no-op creates no undo step");
            h.Begin();h.Preview(x=>EditOperations.Move(x,new[]{tap},100,0,0));
            h.Preview(x=>EditOperations.Move(x,new[]{tap},250,0,0));
            check(Near(Get(h.Document,tap).TimeMs,1250)&&h.UndoCount==0,"EDIT preview starts from gesture original, not previous preview");
            string valid=h.Document.ToText();
            check(Throws(()=>h.Preview(x=>EditOperations.Move(x,new[]{tap},-2000,0,0)))&&h.Document.ToText()==valid,"EDIT rejected preview retains last valid document");
            check(h.Commit()&&h.UndoCount==1,"EDIT one gesture is one undo step");
            check(h.Undo()&&h.Document.ToBytes().SequenceEqual(d.ToBytes()),"EDIT gesture undo restores byte exact document");
            check(h.Redo()&&Near(Get(h.Document,tap).TimeMs,1250),"EDIT gesture redo restores result");
            h.Begin();h.Preview(x=>x.Delete(hold));h.Cancel();
            check(h.Document.Contains(hold)&&h.UndoCount==1,"EDIT escape/cancel does not delete or create history");
            var bounds=new EditHistory(SpcDocument.Parse("chart(120,4)\ntap(1000,1,1)\ntap(1200,2,3)\n"));
            string before=bounds.Document.ToText();
            check(Throws(()=>bounds.Execute(x=>EditOperations.Move(x,new[]{1,2},100,1,.25)))&&bounds.Document.ToText()==before,"EDIT invalid group move is atomic, no width shrink");
            bounds.Execute(x=>EditOperations.Move(x,new[]{1,2},50,-1,-.25));
            check(Get(bounds.Document,1).Lane==0&&Get(bounds.Document,2).Lane==2&&Get(bounds.Document,2).GroundWidth==2,"EDIT valid group move preserves widths and spacing");
            check(Near(Get(bounds.Document,2).TimeMs-Get(bounds.Document,1).TimeMs,200),"EDIT group relative timing preserved");
            var endpoints=SpcDocument.Parse("chart(120,4)\nhold(1000,2,2,1000)\n");
            EditOperations.Endpoint(endpoints,1,false,1250,.5,2);
            check(Near(Get(endpoints,1).TimeMs,1250)&&Near(Get(endpoints,1).EndMs,2000),"EDIT Hold head keeps tail time fixed");
            EditOperations.Endpoint(endpoints,1,true,2300,.5,2);
            check(Near(Get(endpoints,1).TimeMs,1250)&&Near(Get(endpoints,1).DurationMs,1050)&&Get(endpoints,1).Lane==2,"EDIT Hold tail keeps head/lane fixed");
            check(Throws(()=>EditOperations.Endpoint(endpoints,1,true,100,.5,2)),"EDIT reversed hold endpoint rejected");
            EditOperations.GroundEdge(endpoints,1,false,0);
            check(Get(endpoints,1).Lane==1&&Get(endpoints,1).GroundWidth==3,"EDIT ground left edge preserves right boundary");
            EditOperations.GroundEdge(endpoints,1,true,4);
            check(Get(endpoints,1).Lane==1&&Get(endpoints,1).GroundWidth==4,"EDIT ground right edge gives four-lane width");
            var side=SpcDocument.Parse("chart(120,4)\ntap(1000,1,0)\n");
            check(Throws(()=>EditOperations.GroundEdge(side,1,true,3)),"EDIT side note cannot resize");
            var sky=SpcDocument.Parse("chart(120,4)\nskyarea(1000,100,200,2,1,2,2,1,2,1000,7,123) # keep\n");
            EditOperations.AirEdge(sky,1,false,true,.6);
            SkyRange r=ChartMath.SkyAt(Get(sky,1),1000);
            check(Near(r.Left,.495)&&Near(r.Right,.6)&&Get(sky,1).Get(2)==200,"EDIT Sky edge keeps opposite edge and original divisor");
            check(sky.SourceLine(1).EndsWith(",7,123) # keep"),"EDIT edge preserves unknown extra arguments and suffix");
            EditOperations.Endpoint(sky,1,true,2500,.75,0);
            check(Near(Get(sky,1).Get(4),1)&&Get(sky,1).Get(5)==2&&Near(Get(sky,1).DurationMs,1500),"EDIT tail retains tail divisor and full-width center clamps at hard reach bounds");
            var flick=SpcDocument.Parse("chart(120,4)\nflick(1000,6,24,12,16,88)\n");
            EditOperations.AirEdge(flick,1,false,false,.125);
            check(Near(ChartMath.FlickRange(Get(flick,1)).Right,.5)&&Near(Get(flick,1).Get(3),9),"EDIT Flick width edge preserves opposite boundary");
            EditOperations.Mirror(flick,new[]{1});
            check(Get(flick,1).Get(4)==4&&Get(flick,1).Get(5)==88,"EDIT Flick mirror changes direction and keeps extras");
            for(int left=0;left<3;left++)for(int right=0;right<3;right++)
            {
                var original=SpcDocument.Parse("chart(120,4)\nskyarea(1000,6,24,4,16,32,8,"+left+","+right+",2000,2)\n");
                var mirror=original.Clone();EditOperations.Mirror(mirror,new[]{1});bool good=true;
                for(int i=0;i<=20;i++)
                {var a=ChartMath.SkyAt(Get(original,1),1000+i*100);var b=ChartMath.SkyAt(Get(mirror,1),1000+i*100);good&=Near(b.Left,1-a.Right)&&Near(b.Right,1-a.Left);}
                check(good,"EDIT mirrored Sky swaps boundary easing "+left+"/"+right);
            }
            var widths=SpcDocument.Parse("chart(120,4)\ntap(0,1,0)\ntap(0,1,5)\ntap(0,3,1)\n");
            EditOperations.Mirror(widths,new[]{1,2,3});
            check(Get(widths,1).Lane==5&&Get(widths,2).Lane==0&&Get(widths,3).Lane==2&&Get(widths,3).GroundWidth==3,"EDIT mirror floor lanes, including wide notes");
            var cont=SpcDocument.Parse("chart(120,4)\nskyarea(1846,100,200,2,1,2,2,1,1,923,0,123)\n");
            int next=EditOperations.ContinueSky(cont,1,3692);var aEnd=ChartMath.SkyAt(Get(cont,1),2769);var bStart=ChartMath.SkyAt(Get(cont,next),2769);
            check(Near(aEnd.Left,bStart.Left)&&Near(aEnd.Right,bStart.Right)&&Get(cont,next).Get(2)==2,"EDIT explicit Sky continuation joins both edges");
            check(Get(cont,next).Get(7)==0&&Get(cont,next).Get(8)==0&&Get(cont,next).Get(10)==0&&Get(cont,next).Get(11)==123,"EDIT continuation straight segment preserves group/extras");
            var originalClip=SpcDocument.Parse("chart(120,4)\nskyarea(1000,12,24,6,12,24,6,0,0,500,3,99)\n"
                +"skyarea(1500,12,24,6,12,24,4,0,0,500,3)\nflick(2000,6,24,4,16,77)\nskyarea(3000,1,2,1,1,2,1,0,0,500)\n");
            var clipboard=new NoteClipboard();clipboard.Copy(originalClip,new[]{1,2,3,4});var target=originalClip.Clone();int[] pasted=clipboard.Paste(target,5000,true);
            check(pasted.Length==4&&pasted.All(x=>x>4)&&Near(Get(target,pasted[0]).TimeMs,5000)&&Near(Get(target,pasted[2]).TimeMs,6000),"EDIT paste new IDs and relative times");
            check(Get(target,pasted[0]).Get(10)==4&&Get(target,pasted[1]).Get(10)==4&&Get(target,pasted[0]).Get(11)==99,"EDIT paste coherently remaps existing groups and preserves extras");
            check(target.ArgumentTokens(pasted[3]).Length==10&&Get(target,pasted[2]).Get(5)==77,"EDIT paste does not invent groups for ungrouped notes");
            int[] sameGroups=clipboard.Paste(target,10000,false);
            check(Get(target,sameGroups[0]).Get(10)==3,"EDIT paste group remapping can be disabled");
            var add=SpcDocument.Parse("chart(120,4)\n");
            int tId=EditOperations.Add(add,EventKind.Tap,500,1000,1,4,.5,.5,.25);
            int hId=EditOperations.Add(add,EventKind.Hold,500,1500,2,3,.5,.5,.25);
            int fId=EditOperations.Add(add,EventKind.Flick,500,1000,0,1,.25,.25,.125,4,24);
            int sId=EditOperations.Add(add,EventKind.SkyArea,500,1500,0,1,.25,.75,.01,16,200);
            check(Get(add,tId).Lane==1&&Get(add,tId).GroundWidth==4&&Get(add,hId).Lane==2&&Get(add,hId).DurationMs==1000,"EDIT authoring Tap/Hold parameter order");
            check(Get(add,fId).Get(4)==4&&Near(ChartMath.SkyAt(Get(add,sId),500).Width,.01),"EDIT authoring Flick direction and narrow Sky widths");
            check(Throws(()=>EditOperations.Add(add,EventKind.Tap,100,100,4,2,.5,.5,.25)),"EDIT overwide new note rejected");
            check(Near(EditOperations.Quantize(.3125,1.0/24),.3333333333333333),"EDIT X-grid midpoint rounds away from zero");
            var beats=new BeatTimeline(SpcDocument.Parse("chart(120,4)\nbpm(1000,240)\n").ReadEvents());
            check(Near(beats.Snap(1400,4),1375)&&Near(beats.BeatAt(beats.TimeAt(5)),5),"EDIT BPM-aware beat/time conversion");
            var reverse=new ScrollTimeline(SpcDocument.Parse("chart(120,4)\ntrack(1000,-1)\ntrack(2000,1)\n").ReadEvents());
            double resolved;int count;bool interval;
            check(EditTimeResolver.Resolve(reverse,500,1480,4000,out resolved,out count,out interval)&&Near(resolved,1500)&&count==3&&!interval,"EDIT reverse picks nearest context among all candidates");
            var stop=new ScrollTimeline(SpcDocument.Parse("chart(120,4)\ntrack(1000,0)\ntrack(2000,1)\n").ReadEvents());
            check(EditTimeResolver.Resolve(stop,1000,1550,4000,out resolved,out count,out interval)&&Near(resolved,1550)&&interval,"EDIT stop retains preferred time inside plateau");
            check(!EditTimeResolver.Resolve(new ScrollTimeline(new SpcEvent[0]),-1,0,4000,out resolved,out count,out interval),"EDIT inverse outside nonnegative chart time rejected");
            var narrow=SpcDocument.Parse("chart(120,4)\nskyarea(1000,6,24,6,18,24,6,1,2,2000)\n");
            double u;double minimum=ChartMath.SkyMinimumWidth(Get(narrow,1),out u);
            check(Near(minimum,.0428932188134524)&&Near(u,.5),"EDIT continuous Sky minimum (not mesh sampling)");
            narrow.SetNumber(1,3,2.4);narrow.SetNumber(1,6,2.4);
            check(ChartMath.SkyMinimumWidth(Get(narrow,1),out u)<0,"EDIT positive endpoints can still cross in the middle");
            WaveTests(check);
        }
        private static byte[] Wav(int bits,int format,byte[] data,int channels=1)
        {
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));w.Write(36+data.Length+(data.Length&1));w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);w.Write((ushort)format);w.Write((ushort)channels);w.Write(48000);w.Write(48000*channels*bits/8);
                w.Write((ushort)(channels*bits/8));w.Write((ushort)bits);w.Write(Encoding.ASCII.GetBytes("data"));w.Write(data.Length);w.Write(data);
                if((data.Length&1)!=0)w.Write((byte)0);return stream.ToArray();
            }
        }
        private static void WaveTests(Action<bool,string> check)
        {
            var pcm8=WaveReader.Decode(Wav(8,1,new byte[]{0,128,255}));
            check(pcm8.Frames==3&&pcm8.Frequency==48000&&Near(pcm8.Samples[0],-1)&&Near(pcm8.Samples[1],0)&&Near(pcm8.Samples[2],127/128.0),"WAV PCM8 with odd data chunk padding");
            var pcm16=WaveReader.Decode(Wav(16,1,new byte[]{0,128,0,0,255,127}));
            check(Near(pcm16.Samples[0],-1)&&Near(pcm16.Samples[2],32767/32768.0),"WAV PCM16 signs and scaling");
            var pcm24=WaveReader.Decode(Wav(24,1,new byte[]{0,0,128,0,0,0,255,255,127}));
            check(Near(pcm24.Samples[0],-1)&&Near(pcm24.Samples[2],8388607/8388608.0),"WAV PCM24 sign extension");
            var pcm32=WaveReader.Decode(Wav(32,1,new byte[]{0,0,0,128,0,0,0,0}));
            check(Near(pcm32.Samples[0],-1)&&Near(pcm32.Samples[1],0),"WAV PCM32 signs and scaling");
            var stereo=WaveReader.Decode(Wav(16,1,new byte[]{0,128,255,127},2));
            check(stereo.Channels==2&&stereo.Frames==1,"WAV interleaved stereo frame count");
            byte[] floats=BitConverter.GetBytes(float.NaN).Concat(BitConverter.GetBytes(2f)).Concat(BitConverter.GetBytes(-.5f)).ToArray();
            var pcmFloat=WaveReader.Decode(Wav(32,3,floats));
            check(pcmFloat.Samples.SequenceEqual(new[]{0f,1f,-.5f}),"WAV float32 NaN suppression and amplitude clipping");
            check(Throws(()=>WaveReader.Decode(new byte[4])),"WAV rejects truncated input");
            check(Throws(()=>WaveReader.Decode(Wav(16,7,new byte[]{0,0}))),"WAV rejects unsupported compression instead of random samples");
            check(Throws(()=>WaveReader.Decode(Wav(16,1,new byte[]{0,0,0},2))),"WAV rejects bad block alignment");
            byte[] truncated=Wav(16,1,new byte[]{0,0});Array.Resize(ref truncated,truncated.Length-1);
            check(Throws(()=>WaveReader.Decode(truncated)),"WAV validates RIFF length");
        }
    }
}
