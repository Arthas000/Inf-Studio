using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    // Provided executable C# regressions. Run via Tools/InFalsus Studio/Run CSharp Core Checks.
    // Merely shipping these assertions is NOT evidence of a Unity/C# test run.
    public static class NavigationSelfTests
    {
        private static AuthoringGrid Grid(string text)
        {return new AuthoringGrid(new BeatTimeline(SpcDocument.Parse(text).ReadEvents()));}
        public static void Run(Action<bool,string> check)
        {
            var wheel=new WheelStepAccumulator(3);
            check(wheel.Consume(-3)==1,"031 wheel UP advances one step at calibrated scale3");
            check(wheel.Consume(3)==-1,"031 wheel DOWN goes back one step");
            check(wheel.Consume(-6)==2&&wheel.Consume(9)==-3,"031 coalesced wheel events retain notch count");
            wheel.Reset();int count=0;for(int i=0;i<30;i++)count+=wheel.Consume(-.1);
            check(count==1,"031 fractional wheel deltas accumulate to one step, not thirty");
            wheel.Reset();check(wheel.Consume(-1)==0&&wheel.Consume(1)==0,"031 fractional opposing deltas cancel");
            wheel.Configure(1);check(wheel.Consume(-1)==1&&wheel.Consume(1)==-1,"031 scale1 input is supported without half-beat fallback");
            check(wheel.Consume(double.NaN)==0&&wheel.Consume(double.PositiveInfinity)==0,"031 non-finite device deltas ignored");

            var grid=Grid("chart(130,4)\n");
            foreach(int div in new[]{4,6,8})
            {
                var lines=grid.Between(0,1845,div);
                check(lines.Count==4*div,"031 130BPM one 4-beat bar has N*4 lines, N="+div);
                check(lines.Count(x=>x.IsBeat)==4&&lines.Count(x=>x.IsBar)==1,"031 beat/bar classification independent of division "+div);
                double t=0;
                for(int n=1;n<=256;n++)
                {
                    t=grid.Step(t,1,div);
                    double expected=AuthoringGrid.RoundMs(n*60000.0/(130*div));
                    check(t==expected,"031 adjacent navigation equals absolute index N="+div+" n="+n);
                }
                for(int n=255;n>=0;n--)t=grid.Step(t,-1,div);
                check(t==0,"031 back through same rounded grid returns exactly zero, N="+div);
                check(grid.Step(0,-10,div)==0,"031 scrolling before chart start clamps to zero, N="+div);
            }
            check(grid.Step(0,4,4)==462&&grid.Step(115,1,4)==231,"031 rounded period is not repeatedly added");
            check(grid.Step(1800,1,4)==1846&&grid.Step(1800,-1,4)==1731,"031 off-grid first step uses strict directional next line");
            check(grid.Step(0,0,4)==0,"031 zero wheel step leaves time unchanged");
            check(grid.Nearest(115384615.38461538,4).TimeMs==115384615,"031 millionth division has no rounded-period drift");
            var l4=grid.Between(0,6000,4).Select(x=>x.TimeMs).ToArray();
            var l8=grid.Between(0,6000,8).Select(x=>x.TimeMs).ToArray();
            check(l4.All(l8.Contains),"031 quarter-beat lines are also eighth-beat lines");
            var l6=grid.Between(0,6000,6);
            check(l6.Where(x=>x.IsBeat).All(x=>l4.Contains(x.TimeMs)),"031 sixth-beat and quarter-beat grids share whole beats");

            var reset=Grid("chart(120,4)\nbpm(1123,150,3)\nbpm(2507,100)\n");
            check(reset.Step(1000,1,4)==1123&&reset.Step(1123,1,4)==1223,"031 non-aligned timing starts a NEW local grid");
            check(reset.Step(1123,-1,4)==1000,"031 reverse navigation crosses timing boundary correctly");
            check(reset.Between(1123,2506,4).Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{1123d,2323d}),"031 new meter3 bars originate at timing event");
            check(reset.Between(2507,4307,4).Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{2507d,4307d}),"031 omitted meter inherits3; event still resets origin");
            var noReset=Grid("chart(120,4)\ntrack(1123,0)\ntrack(1567,-1)\ntrack(2222,2)\n");
            check(noReset.Between(0,3000,6).Select(x=>x.TimeMs).SequenceEqual(Grid("chart(120,4)\n").Between(0,3000,6).Select(x=>x.TimeMs)),"031 track events do NOT reset rhythm grids");
            var meterOnly=Grid("chart(120,4)\nbpm(1123,120,5)\n");
            check(meterOnly.Between(1123,3623,8).Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{1123d,3623d}),"031 same BPM with meter5 resets local bars");
            foreach(var line in reset.Between(0,9000,8))
                check(reset.Nearest(line.TimeMs,8).TimeMs==line.TimeMs,"031 rendered rounded reset-grid point also snaps to itself at "+line.TimeMs);

            foreach(int width in new[]{1,2,3,4})foreach(int lane in new[]{0,1,2,3,4,5})
            {
                int start,resolvedWidth;bool ok=GroundPlacement.TryResolve(lane,width,out start,out resolvedWidth);
                int expectedStart=lane==0||lane==5?lane:Math.Min(lane,5-width);
                int expectedWidth=lane==0||lane==5?1:width;
                check(ok&&start==expectedStart&&resolvedWidth==expectedWidth,"031 wide placement hit="+lane+" width="+width);
                foreach(EventKind kind in new[]{EventKind.Tap,EventKind.Hold})
                {
                    var draft=new PointPlacement(kind);check(draft.ChooseTime(1000,lane,width)&&draft.Lane==expectedStart&&draft.Width==expectedWidth,"031 actual draft resolution "+kind+" lane="+lane+" width="+width);
                    if(kind==EventKind.Hold)check(draft.ChooseTime(1500,1,1)&&draft.Lane==expectedStart&&draft.Width==expectedWidth,"031 Hold retains resolved start lane and width at end click");
                }
            }
            int dummyLane,dummyWidth;
            check(!GroundPlacement.TryResolve(6,1,out dummyLane,out dummyWidth)&&!GroundPlacement.TryResolve(1,5,out dummyLane,out dummyWidth),"031 invalid lane/central brush not silently invented");
            // Imported invalid spans remain raw; the resolver is only an authoring brush policy.
            var imported=SpcDocument.Parse("chart(130,4)\ntap(1000,3,3)\n");
            check(imported.SourceLine(1)=="tap(1000,3,3)"&&!ChartMath.ValidGround(imported.ReadEvents()[1]),"031 import is not auto-shifted by new placement policy");

            var menu=new RadialMenuState();menu.Press(0);
            check(menu.Pressed&&menu.Visible,"031 radial is visible immediately on right-down");
            check(!menu.Release(RadialItem.Blank).Valid&&!menu.InPointMode,"031 immediate blank release remains no-op");
            menu.Press(1);check(menu.Release(RadialItem.EnterPoint).Valid&&menu.InPointMode,"031 release may activate without initial delay");
            menu.Press(2);menu.Tick(2,RadialItem.Tap);
            check(!menu.Tick(2.49,RadialItem.Tap),"031 initial delay removal does not remove .5s submenu dwell");
            check(menu.Tick(2.5,RadialItem.Tap)&&menu.Pressed&&menu.Level==RadialLevel.TapWidth,"031 held submenu opens at .5s");
            check(menu.Release(RadialItem.Width3).Valid,"031 same held right button releases width3");

            foreach(bool right in new[]{false,true})
            {
                double outward=right?32:-32;
                check(SkyEaseHandle.FromDrag(0,right,0)==0,"031 neutral midpoint is Linear "+right);
                check(SkyEaseHandle.FromDrag(0,right,outward)==1,"031 outward maps SineOut independently per edge "+right);
                check(SkyEaseHandle.FromDrag(0,right,-outward)==2,"031 inward maps SineIn independently per edge "+right);
                check(SkyEaseHandle.FromDrag(1,right,0)==1&&SkyEaseHandle.FromDrag(2,right,0)==2,"031 grabbing existing mode never jumps to Linear "+right);
                check(SkyEaseHandle.FromDrag(1,right,-outward)==0&&SkyEaseHandle.FromDrag(1,right,-2*outward)==2,"031 existing Out can move through center to In "+right);
            }
            const string source="chart(130,4)\r\nskyarea(1846.123456789,100,200,2,1,2,2,0,2,923,0,retained)\r\nbeam(opaque)\n";
            var history=new EditHistory(SpcDocument.Parse(source,true));byte[] original=history.Document.ToBytes();
            string[] before=history.Document.ArgumentTokens(1);
            history.Begin();history.Preview(d=>EditOperations.SetEase(d,new[]{1},false,1));history.Preview(d=>EditOperations.SetEase(d,new[]{1},false,2));
            check(history.Commit()&&history.UndoCount==1,"031 midpoint drag is one undo transaction");
            string[] after=history.Document.ArgumentTokens(1);
            check(before.Where((x,i)=>i!=7).SequenceEqual(after.Where((x,i)=>i!=7))&&after[7]=="2","031 midpoint changes ONLY selected easing token, not time/split/width/group/extras");
            check(history.Undo()&&history.Document.ToBytes().SequenceEqual(original),"031 midpoint undo is byte-exact including BOM/EOL");
            check(history.Redo()&&history.Document.ArgumentTokens(1)[7]=="2","031 midpoint redo retains mode");
        }
    }
}
