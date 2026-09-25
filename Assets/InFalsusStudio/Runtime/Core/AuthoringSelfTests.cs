using System;
using System.Linq;

namespace InFalsusStudio.Core
{
    public static class AuthoringSelfTests
    {
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-8;}
        private static AuthoringGrid Grid(string text){return new AuthoringGrid(new BeatTimeline(SpcDocument.Parse(text).ReadEvents()));}
        public static void Run(Action<bool,string> check)
        {
            var grid=Grid("chart(130,4)\n");
            check(grid.Nearest(1846,4).TimeMs==1846,"AUTHOR 130 BPM fourth beat rounds once to 1846 ms");
            check(grid.Nearest(1846.1538461538462,4).TimeMs==1846,"AUTHOR exact fractional beat selects same rendered integer line");
            check(grid.Nearest(1000000*60000.0/130/4,4).TimeMs==115384615,"AUTHOR millionth subdivision calculated by index, not accumulated 115ms");
            bool driftFree=true;double next=0;
            for(int i=1;i<=4096;i++)
            {next=grid.Adjacent(next,1,4);driftFree&=next==Math.Round(i*60000.0/130/4,MidpointRounding.AwayFromZero);}
            check(driftFree,"AUTHOR 4096 consecutive right steps do not accumulate rounding error");
            for(int i=4095;i>=0;i--)next=grid.Adjacent(next,-1,4);
            check(next==0,"AUTHOR stepping back returns exactly to zero");
            var lines=grid.Between(0,2000,4);
            check(lines.All(x=>x.TimeMs==Math.Round(x.TimeMs))&&lines.Select(x=>x.TimeMs).Distinct().Count()==lines.Count,"AUTHOR rendered line times are distinct integer ms");
            check(lines.All(x=>grid.Nearest(x.TimeMs,4).TimeMs==x.TimeMs),"AUTHOR every rendered line snaps to itself");
            check(lines.Count==18,"AUTHOR four subdivisions per beat, not four subdivisions per bar");
            var four=Grid("chart(120,4)\n").Between(0,4000,4);
            var three=Grid("chart(120,3)\n").Between(0,4000,4);
            check(four.Select(x=>x.TimeMs).SequenceEqual(three.Select(x=>x.TimeMs)),"AUTHOR changing integer meter preserves subdivision positions");
            check(four.Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{0d,2000d,4000d}),"AUTHOR four beats per bar");
            check(three.Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{0d,1500d,3000d}),"AUTHOR three beats per bar");
            var change=Grid("chart(120,4)\nbpm(4000,120,3)\n");
            check(change.Between(4000,7000,4).Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{4000d,5500d,7000d}),"AUTHOR local bar origin and meter after timing change");
            var shifted=Grid("chart(120,4)\nbpm(1123,150,3)\n");
            check(shifted.Nearest(1220,4).TimeMs==1223,"AUTHOR fractional global beat phase uses local timing-segment origin");
            check(shifted.Between(1000,1300,4).All(q=>shifted.Nearest(q.TimeMs,4).TimeMs==q.TimeMs),"AUTHOR BPM boundary display and snap share candidates");
            var inherited=Grid("chart(120,3)\nbpm(1500,240)\n");
            check(inherited.Between(1500,3000,4).Where(x=>x.IsBar).Select(x=>x.TimeMs).SequenceEqual(new[]{1500d,2250d,3000d}),"AUTHOR omitted meter inherits last positive meter");
            var dense=Grid("chart(60000,4)\n").Between(0,10,8);
            check(dense.Select(x=>x.TimeMs).Distinct().Count()==dense.Count,"AUTHOR coincident sub-millisecond grid ticks are deduplicated");
            var xgrid=new AirAuthoringGrid();
            check(xgrid.Ticks().Count()==21&&Near(xgrid.Snap(.526),.55),"AUTHOR default horizontal ruler 0..100 step 5");
            check(Near(xgrid.Snap(-1),0)&&Near(xgrid.Snap(2),1),"AUTHOR pointer X constrained to displayed ruler");
            xgrid.SetStep(3m);check(Near(xgrid.Ticks().Last(),1)&&Near(xgrid.Snap(.999),1),"AUTHOR step not dividing 100 keeps explicit right endpoint");
            check(AuthoredNumber.Format(1.0149999999999999)=="1.015","AUTHOR generated numbers have no binary-tail noise");
            var raw=SpcDocument.Parse("chart(130,4)\r\nskyarea(1846.1538461538462,100,200,2,1,2,2,1,2,923,7) # keep\nbeam(foo)\n",true);
            byte[] original=raw.ToBytes();
            var e=raw.ReadEvents()[1];EditOperations.AirEdge(raw,1,false,true,.55);
            var edge=raw.ReadEvents()[1];
            check(raw.ArgumentTokens(1)[0]=="1846.1538461538462","AUTHOR width edit does not snap or reformat time");
            check(edge.Get(2)==200&&edge.Get(5)==2&&raw.SourceLine(2)=="beam(foo)","AUTHOR width edit preserves both source divisors and unknown lines");
            check(Near(ChartMath.SkyAt(edge,edge.TimeMs).Left,.495)&&Near(edge.Get(1),104.5)&&Near(edge.Get(3),11),"AUTHOR edge arithmetic preserves opposite half-tick exactly");
            check(!raw.SourceLine(1).Contains("9999999"),"AUTHOR actual stored new coordinate tokens are clean, not just display labels");
            var noop=SpcDocument.FromBytes(original);AuthoredNumber.Set(noop,1,0,noop.ReadEvents()[1].TimeMs);
            check(noop.ToBytes().SequenceEqual(original),"AUTHOR semantic no-op retains imported long time token exactly");
            noop.ReplaceSourceLine(1,"skyarea(1846.1234567890123,100,200,2,1,2,2,1,2,923,7)");
            check(noop.ArgumentTokens(1)[0]=="1846.1234567890123","AUTHOR raw Apply intentionally bypasses all snapping/formatting");
            var hist=new EditHistory(SpcDocument.FromBytes(original));hist.Begin();
            hist.Preview(d=>EditOperations.AirEdge(d,1,false,true,.6));hist.Preview(d=>EditOperations.AirEdge(d,1,false,true,.55));
            check(hist.Commit()&&hist.UndoCount==1,"AUTHOR multiple edge previews yield one undo entry");
            check(hist.Undo()&&hist.Document.ToBytes().SequenceEqual(original),"AUTHOR undo restores exact original precision and BOM");
            var move=SpcDocument.FromBytes(original);EditOperations.PointerMove(move,new[]{1},2308-move.ReadEvents()[1].TimeMs,0,0);
            check(move.ReadEvents()[1].TimeMs==2308&&move.ArgumentTokens(1)[0]=="2308","AUTHOR mouse-time operation stores integer ms");
            var group=SpcDocument.Parse("chart(130,4)\ntap(0,1,1)\ntap(115,1,2)\nhold(0,1,1,115)\n");
            EditOperations.PointerMove(group,new[]{1,2,3},115,0,0,grid,4);
            var g=group.ReadEvents();
            check(g[1].TimeMs==115&&g[2].TimeMs==231&&g[3].EndMs==231,"AUTHOR group moved heads/tails use actual grid, not rounded-period addition");
            PlacementChecks(check);MenuChecks(check);
        }
        private static void PlacementChecks(Action<bool,string> check)
        {
            var sky=new PointPlacement(EventKind.SkyArea);
            check(sky.ChooseTime(1846,1,1)&&sky.Phase==PlacementPhase.StartX,"POINT Sky step 1 locks start time");
            check(!sky.ChooseTime(2000,1,1)&&sky.StartMs==1846,"POINT cannot change a locked time during X selection");
            check(sky.ChooseX(.5)&&sky.Phase==PlacementPhase.EndTime,"POINT Sky step 2 locks start X");
            check(!sky.ChooseTime(1846,1,1)&&sky.Phase==PlacementPhase.EndTime,"POINT zero-duration Sky rejected without corrupting draft");
            check(sky.ChooseTime(2769,1,1)&&sky.StartMs==1846&&sky.StartX==.5,"POINT browsing/end selection retains locked start");
            check(sky.ChooseX(.75)&&sky.Phase==PlacementPhase.Ready,"POINT Sky fourth click completes");
            var history=new EditHistory(SpcDocument.Parse("chart(130,4)\n"));
            history.Execute(d=>EditOperations.Add(d,EventKind.SkyArea,sky.StartMs,sky.EndMs,1,1,sky.StartX,sky.EndX,.1,16,100));
            check(history.Document.SourceLine(1)=="skyarea(1846,50,100,10,75,100,10,0,0,923,0)","POINT Sky default 10 percent, split 100, straight sides, integer times");
            check(history.UndoCount==1&&history.Undo()&&history.Document.LineCount==1,"POINT four clicks produce one undo, no half-created source lines");
            sky.Reset(EventKind.SkyArea);check(!sky.Pending&&sky.Phase==PlacementPhase.StartTime,"POINT repeated placement starts clean with same kind");
            var tap=new PointPlacement(EventKind.Tap);
            check(tap.ChooseTime(1000,0,4)&&tap.Width==1,"POINT Tap width4 brush ignored on side 0");
            tap.Reset(EventKind.Tap);check(tap.ChooseTime(1000,5,4)&&tap.Width==1,"POINT Tap width4 brush ignored on side 5");
            tap.Reset(EventKind.Tap);check(tap.ChooseTime(1000,1,4)&&tap.Width==4,"POINT Tap width4 works on central starting lane1");
            tap.Reset(EventKind.Tap);check(tap.ChooseTime(1000,3,4)&&tap.Lane==1&&tap.Width==4,"POINT central wide brush shifts its start left, never shrinks");
            var hold=new PointPlacement(EventKind.Hold);hold.ChooseTime(1000,5,3);
            check(hold.Width==1&&hold.Phase==PlacementPhase.EndTime&&hold.ChooseTime(2000,1,4)&&hold.Lane==5,"POINT side Hold keeps initial lane and one-track width");
            var flick=new PointPlacement(EventKind.Flick);
            check(flick.ChooseTime(3692,1,1)&&flick.Phase==PlacementPhase.StartX&&flick.ChooseX(.25)&&flick.Phase==PlacementPhase.Ready,"POINT Flick requires time then X, not fake duration");
            var empty=new EditHistory(SpcDocument.Parse("chart(130,4)\n"));var draft=new PointPlacement(EventKind.SkyArea);draft.ChooseTime(1000,1,1);draft.ChooseX(.5);draft.Reset(EventKind.SkyArea);
            check(empty.UndoCount==0&&empty.Document.LineCount==1,"POINT cancellation before final click never touches the document");
        }
        private static void MenuChecks(Action<bool,string> check)
        {
            var m=new RadialMenuState();m.Press(0);m.Tick(.2,RadialItem.EnterPoint);
            check(m.Visible&&m.Release(RadialItem.EnterPoint).Valid&&m.InPointMode,"MENU right menu and release work without initial delay");
            m=new RadialMenuState();
            m.Press(1);m.Tick(1.3,RadialItem.EnterPoint);
            check(m.Release(RadialItem.EnterPoint).Valid&&m.InPointMode&&!m.Pressed,"MENU release on Point enters mode but does NOT immediately open next menu");
            m.Press(2);m.Tick(2.3,RadialItem.Tap);
            var direct=m.Release(RadialItem.Tap);
            check(direct.Valid&&direct.Level==RadialLevel.Point&&direct.Item==RadialItem.Tap,"MENU direct right release on Tap uses current default");
            m.Press(3);m.Tick(3.3,RadialItem.Tap);
            check(!m.Tick(3.79,RadialItem.Tap)&&m.Level==RadialLevel.Point,"MENU no submenu before 0.5s hover");
            check(m.Tick(3.81,RadialItem.Tap)&&m.Level==RadialLevel.TapWidth&&m.Pressed,"MENU width submenu opens while SAME right hold is maintained");
            check(!m.Release(RadialItem.Blank).Valid&&m.InPointMode,"MENU releasing at child center/blank is a no-op");
            m.Press(4);m.Tick(4.3,RadialItem.Hold);m.Tick(4.81,RadialItem.Hold);
            var hold=m.Release(RadialItem.Width4);
            check(hold.Valid&&hold.Level==RadialLevel.HoldWidth,"MENU Hold has independent width submenu");
            m.Press(5);m.Tick(5.3,RadialItem.Flick);m.Tick(5.81,RadialItem.Flick);
            check(!RadialMenuState.Allowed(m.Level,RadialItem.Width2)&&m.Release(RadialItem.Left).Valid,"MENU Flick submenu accepts only left/right, not widths");
            m.Press(6);m.Tick(6.3,RadialItem.Sky);m.Tick(7,RadialItem.Sky);
            check(m.Level==RadialLevel.Point&&m.Release(RadialItem.Sky).Valid,"MENU Sky has no hover submenu");
            m.Press(8);m.Tick(8.3,RadialItem.Tap);m.Tick(8.6,RadialItem.Blank);m.Tick(8.7,RadialItem.Tap);
            check(!m.Tick(9.1,RadialItem.Tap),"MENU leaving a button resets its hover timer");m.Cancel();
            m.Press(10);m.Tick(10.3,RadialItem.NoCreate);check(m.Release(RadialItem.NoCreate).Valid&&m.InPointMode,"MENU No creation stays in Point mode");
            m.Press(11);m.Tick(11.3,RadialItem.Exit);check(m.Release(RadialItem.Exit).Valid&&!m.InPointMode,"MENU Exit returns Home mode");
            m.Press(12);m.Tick(12.3,RadialItem.Blank);check(!m.Release(RadialItem.Blank).Valid&&!m.InPointMode,"MENU Home blank release never switches mode or grid");
            m.Press(13);m.Tick(13.3,RadialItem.Grid);check(m.Release(RadialItem.Grid).Valid&&!m.InPointMode,"MENU Home grid toggle is an action, not a mode transition");
        }
    }
}
