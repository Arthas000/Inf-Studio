using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // Executes real C# assertions when invoked from Unity's menu or Tests/CoreChecks.csproj.
    // The delivery report explicitly distinguishes these provided tests from tests run in this session.
    public static class CoreSelfTests
    {
        public static string Run(byte[] suppliedChart=null)
        {
            int count=0;var report=new StringBuilder();
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new InvalidOperationException("FAIL: "+name);count++;report.AppendLine("PASS "+name);};
            Func<double,double,bool> near=(a,b)=>Math.Abs(a-b)<1e-8;
            foreach(int code in new[]{0,1,2}){check(near(ChartMath.Ease(code,0),0),"ease "+code+" start");check(near(ChartMath.Ease(code,1),1),"ease "+code+" end");}
            check(near(ChartMath.Ease(1,.5),Math.Sqrt(.5)),"SineOut midpoint");
            check(near(ChartMath.Ease(2,.5),1-Math.Sqrt(.5)),"SineIn midpoint");
            string raw="chart(130.00000, 4)\r\n  tap(1000.125,  2 , 3) # kept\nbeam(foo,bar)\r\nstrange(12,\"raw\")\n";
            var d=SpcDocument.Parse(raw,true);check(d.ToText()==raw,"mixed line endings unchanged");check(d.ToBytes().Take(3).SequenceEqual(new byte[]{239,187,191}),"BOM survives");
            check(SpcDocument.FromBytes(d.ToBytes()).ToBytes().SequenceEqual(d.ToBytes()),"byte-level round trip");
            d.SetNumber(1,0,1000.5);check(d.SourceLine(1)=="  tap(1000.5,  2 , 3) # kept","only edited token changes");
            check(d.SourceLine(2)=="beam(foo,bar)","opaque event kept");
            var h=new EditHistory(SpcDocument.Parse(raw));h.Execute(x=>x.SetNumber(1,1,1));check(h.Undo()&&h.Document.ToText()==raw,"undo exact");check(h.Redo()&&h.Document.SourceLine(1).Contains("  1 ,"),"redo exact");
            var optional=SpcDocument.Parse("chart(130,4)\nbpm(1,130, ,7)\n");optional.SetNumber(1,2,4);check(optional.SourceLine(1)=="bpm(1,130, 4,7)","blank optional token whitespace");
            var shape=SpcDocument.Parse("chart(130,4)\nskyarea(1000,6,24,6,18,24,6,1,2,2000)\n").ReadEvents()[1];
            var mid=ChartMath.SkyAt(shape,2000);check(near(mid.Width,.0428932188134524),"independent boundaries narrow midpoint");
            var groundDoc=SpcDocument.Parse("chart(130,4)\ntap(1000,4,1)\nhold(1000,2,3,500)\ntap(1,2,0)\ntap(1,2,4)\n").ReadEvents();
            check(groundDoc[1].Lane==1&&groundDoc[1].GroundWidth==4,"tap parameter order");check(groundDoc[2].Lane==2&&groundDoc[2].GroundWidth==3&&groundDoc[2].EndMs==1500,"hold parameter order");
            check(ChartMath.ValidGround(groundDoc[1])&&ChartMath.ValidGround(groundDoc[2]),"wide central notes valid");check(!ChartMath.ValidGround(groundDoc[3])&&!ChartMath.ValidGround(groundDoc[4]),"invalid side and central span rejected");
            var speeds=SpcDocument.Parse("chart(130,4)\ntrack(1000,-1)\ntrack(2000,1)\n").ReadEvents();var timeline=new ScrollTimeline(speeds);
            check(near(timeline.PositionAt(500),500)&&near(timeline.PositionAt(1500),500)&&near(timeline.PositionAt(2500),500),"reverse maps three times to one depth");
            check(timeline.Inverse(500,0,3000).Count==3,"inverse retains all three candidates");
            var stop=new ScrollTimeline(SpcDocument.Parse("chart(130,4)\ntrack(1000,0)\ntrack(2000,1)\n").ReadEvents());
            check(stop.Inverse(1000,0,3000).Any(x=>x.IsInterval&&x.Start==1000&&x.End==2000),"stop inverse is interval");
            var duplicate=new ScrollTimeline(SpcDocument.Parse("chart(130,4)\ntrack(1000,2)\ntrack(1000,3)\n").ReadEvents());check(near(duplicate.PositionAt(2000),4000),"equal-time track last source wins");
            var camera=CameraCalibration.Reference();ProjectedPoint gl=camera.Project(-2,0,0),gr=camera.Project(2,0,0),sj=camera.Project(0,camera.AirHeight,0),sl=camera.Project(-camera.SideOuterX,camera.SideRise,0);
            check(near(gl.X,434)&&near(gr.X,1614)&&near(gl.Y,1029),"ground judgement calibrated pixels");check(near(sj.Y,734),"air judgement calibrated row");check(near(sl.X,124)&&near(sl.Y,840),"side outer calibrated endpoint");
            check(near(camera.Project(-2,0,30).X+camera.Project(2,0,30).X,2048),"left right symmetry");check(Math.Abs(camera.Project(0,0,1e9).Y-35)<1e-4,"vanishing row");
            if(suppliedChart!=null)
            {
                var file=SpcDocument.FromBytes(suppliedChart);var events=file.ReadEvents();check(file.ToBytes().SequenceEqual(suppliedChart),"supplied chart byte-exact");
                check(file.LineCount==1138,"sample 1138 lines");check(events.Count(x=>x.Kind==EventKind.Tap)==600,"sample 600 taps");check(events.Count(x=>x.Kind==EventKind.Hold)==26,"sample 26 holds");
                check(events.Count(x=>x.Kind==EventKind.Flick)==304,"sample 304 flicks");check(events.Count(x=>x.Kind==EventKind.SkyArea)==190,"sample 190 sky segments");check(events.Count(x=>x.Kind==EventKind.Track)==17,"sample 17 track events at end of file");
                check(events.Where(x=>x.Kind==EventKind.Tap||x.Kind==EventKind.Hold).All(ChartMath.ValidGround),"all sample ground spans valid");
                var sky=events.Where(x=>x.Kind==EventKind.SkyArea).Take(2).ToArray();check(near(ChartMath.SkyAt(sky[0],1846).Width,.01),"opening width 1 percent (not 5 percent)");
                check(near(ChartMath.SkyAt(sky[0],2769).Width,1),"opening first segment ends full width");check(near(ChartMath.SkyAt(sky[1],2769).Width,1)&&near(ChartMath.SkyAt(sky[1],3692).Width,.01),"opening second segment independent divisors");
                var ts=new ScrollTimeline(events);check(near(ts.Delta(22400,22154),246*.30000001192092896),"appended track events affect side holds");
            }
            EditingSelfTests.Run(check);
            AuthoringSelfTests.Run(check);
            NavigationSelfTests.Run(check);
            SongProjectSelfTests.Run(check);
            Release05SelfTests.Run(check);
            Release051SelfTests.Run(check);
            Release06SelfTests.Run(check,suppliedChart);
            Release07SelfTests.Run(check);
            Release08SelfTests.Run(check);
            Release09SelfTests.Run(check);
            report.AppendLine("TOTAL: "+count+" checks passed.");return report.ToString();
        }
    }
}
