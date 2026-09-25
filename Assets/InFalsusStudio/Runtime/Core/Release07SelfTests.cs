using System;
using System.Linq;
using System.Collections.Generic;

namespace InFalsusStudio.Core
{
    // Production C# regression entry; must be executed with Unity or .NET, not confused
    // with the independent Python reference tests supplied in Tools.
    public static class Release07SelfTests
    {
        private static bool Rejects(Action a){try{a();return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
        public static void Run(Action<bool,string> check)
        {
            var h=new EditHistory(SpcDocument.Parse("chart(130,4)\r\ntap(1000.125000,4,1,88)\r\ntap(1234,3,2)\r\nopaque(9223372036854775807)\r\n"));
            string before=h.Document.ToText();int[] ids=h.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
            h.Execute(d=>SelectionAuthoring.Field(d,ids,EventKind.Tap,"width","2"));
            check(h.Document.ReadEvents().Where(n=>n.IsNote).All(n=>n.GroundWidth==2),"070 live batch width sets every selected Tap");
            check(h.Document.SourceLine(ids[0])=="tap(1000.125000,2,1,88)","070 changing a field preserves raw time and extension token");
            check(h.UndoCount==1&&h.Undo()&&h.Document.ToText()==before,"070 batch change is one reversible memory transaction");
            var side=new EditHistory(SpcDocument.Parse("chart(100,4)\ntap(1000,1,0)\ntap(1000,1,5)\ntap(1000,1,4)\n"));
            var si=side.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
            side.Execute(d=>SelectionAuthoring.Field(d,si,EventKind.Tap,"width","3"));
            var sn=side.Document.ReadEvents().Where(n=>n.IsNote).ToArray();
            check(sn[0].GroundWidth==1&&sn[1].GroundWidth==1&&sn[2].GroundWidth==3&&sn[2].Lane==2,"070 side widths fixed, central wide edit fits without shrinking");
            var flick=new EditHistory(SpcDocument.Parse("chart(100,4)\nflick(1000,6,24,4,16)\nflick(2000,18,24,6,4)\n"));
            var fi=flick.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
            flick.Execute(d=>SelectionAuthoring.Field(d,fi,EventKind.Flick,"direction","4"));
            flick.Execute(d=>SelectionAuthoring.Field(d,fi,EventKind.Flick,"width","50"));
            flick.Execute(d=>SelectionAuthoring.Field(d,fi,EventKind.Flick,"start","3000"));
            check(flick.Document.ReadEvents().Where(n=>n.IsNote).All(n=>n.Get(4)==4&&n.Get(3)==12&&n.Get(2)==24&&n.TimeMs==3000),"070 batch Flick common fields update independently");
            var sky=new EditHistory(SpcDocument.Parse("chart(100,4)\nskyarea(1000,25,100,10,75,100,10,0,0,1000)\nskyarea(3000,75,100,20,25,100,20,0,0,500)\n"));
            var ski=sky.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
            sky.Execute(d=>SelectionAuthoring.Field(d,ski,EventKind.SkyArea,"bothWidths","20"));
            sky.Execute(d=>SelectionAuthoring.Field(d,ski,EventKind.SkyArea,"leftEase","1"));
            check(sky.Document.ReadEvents().Where(n=>n.IsNote).All(n=>n.Get(3)==20&&n.Get(6)==20&&n.Get(7)==1&&n.Get(8)==0),"070 batch Sky widths / one-side ease, other shape retained");
            EventKind kind;check(SelectionAuthoring.SameType(sky.Document.ReadEvents().Where(n=>n.IsNote),out kind)&&kind==EventKind.SkyArea,"070 homogeneous selection panel");
            check(!SelectionAuthoring.SameType(sky.Document.ReadEvents().Where(n=>n.IsNote).Concat(flick.Document.ReadEvents().Where(n=>n.IsNote)),out kind),"070 mixed selection hides property panel");
            var invalid=new EditHistory(SpcDocument.Parse("chart(100,4)\nflick(1000,10,100,10,16)\nflick(1000,90,100,10,4)\n"));
            string ib=invalid.Document.ToText();var ii=invalid.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
            check(Rejects(()=>invalid.Execute(d=>SelectionAuthoring.Field(d,ii,EventKind.Flick,"width","90")))&&invalid.Document.ToText()==ib&&invalid.UndoCount==0,"070 invalid batch rejects atomically");

            var range=SpcDocument.Parse("chart(100,4)\ntap(999,1,1)\ntap(1000,1,1)\nflick(2000,50,100,10,4)\nhold(500,1,1,1500)\nhold(1500,1,1,1000)\nskyarea(500,50,100,10,50,100,10,0,0,1000)\n");
            var rs=SelectionAuthoring.InRange(range.ReadEvents(),1000,2000);
            check(rs.SequenceEqual(new[]{2,3,4,6}),"070 inclusive range with Hold/Sky END membership, not overlap or head");
            check(SelectionAuthoring.InRange(range.ReadEvents(),2000,1000).SequenceEqual(rs),"070 reverse range click endpoints normalized");

            var original=SpcDocument.Parse("chart(130,4)\r\ntap(115,1,1)\r\ntap(231,1,2,77)\r\nunknown(keep)\r\n");
            string bytes=original.ToText();var noteIds=original.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
            var copy=new FloatingNotes(original,noteIds,false);var ghost=copy.Preview(231);
            check(original.ToText()==bytes&&ghost.Select(n=>n.TimeMs).SequenceEqual(new[]{231.0,347.0}),"070 floating copy changes no source; relative timing is preserved");
            var dest=new EditHistory(original.Clone());int[] pasted=null;dest.Execute(d=>pasted=copy.Place(d,231,false));
            check(pasted.Length==2&&dest.Document.ReadEvents().Count(n=>n.IsNote)==4&&dest.UndoCount==1,"070 copy drop is one transaction");
            var grid=new AuthoringGrid(new BeatTimeline(dest.Document.ReadEvents()));dest.Execute(d=>SelectionAuthoring.Align(d,pasted,grid,4));
            check(dest.Document.ReadEvents().Where(n=>pasted.Contains(n.SourceId)).Select(n=>n.TimeMs).SequenceEqual(new[]{231.0,346.0}),"070 explicit grid-align fixes the copied 1ms discrepancy");
            check(dest.Undo()&&dest.Undo()&&dest.Document.ToText()==bytes,"070 copy/align undo exact original including CRLF/unknown");
            var cut=new FloatingNotes(original,noteIds,true);cut.Preview(4000);check(original.ToText()==bytes,"070 cut preview/cancel does not delete originals");
            var cutHistory=new EditHistory(original.Clone());int[] moved=null;cutHistory.Execute(d=>moved=cut.Place(d,4000,true));
            check(moved.SequenceEqual(noteIds)&&cutHistory.Document.ReadEvents().Count(n=>n.IsNote)==2,"070 cut drop moves originals, preserving source IDs");
            check(cutHistory.Undo()&&cutHistory.Document.ToText()==bytes,"070 cut drop undo restores original positions");
            var conflict=original.Clone();conflict.SetNumber(noteIds[0],1,2);check(Rejects(()=>cut.Place(conflict,5000,false)),"070 cut stale source conflict rejected");
            var longH=new EditHistory(SpcDocument.Parse("chart(120,4)\nhold(1001,1,1,501)\nskyarea(1751,50,100,20,50,100,20,0,0,498)\n"));
            var longIds=longH.Document.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();var lg=new AuthoringGrid(new BeatTimeline(longH.Document.ReadEvents()));
            longH.Execute(d=>SelectionAuthoring.Align(d,longIds,lg,4));var ln=longH.Document.ReadEvents().Where(n=>n.IsNote).ToArray();
            check(ln[0].TimeMs==1000&&ln[0].EndMs==1500&&ln[1].TimeMs==1750&&ln[1].EndMs==2250,"070 long-note heads AND tails align independently");
            var tiny=new EditHistory(SpcDocument.Parse("chart(120,4)\ntap(1001,1,1)\nhold(1001,1,1,1)\n"));string tb=tiny.Document.ToText();
            check(Rejects(()=>tiny.Execute(d=>SelectionAuthoring.Align(d,new[]{1,2},lg,4)))&&tiny.Document.ToText()==tb,"070 collapsed long note rejects whole alignment");

            var radial=new RadialMenuState();radial.Press(0,true);check(radial.Level==RadialLevel.Selection&&radial.Visible,"070 selected-note radial opens immediately");
            foreach(var item in new[]{RadialItem.Delete,RadialItem.Mirror,RadialItem.Copy,RadialItem.Cut,RadialItem.Align}){radial.Press(0,true);check(radial.Release(item).Valid,"070 selected radial release "+item);}
            radial.Press(0,true);check(!radial.Release(RadialItem.Blank).Valid,"070 blank context release has no action");
            radial.Press(0,true);check(radial.Release(RadialItem.EnterPoint).Valid&&radial.InPointMode,"070 selection context can return to point mode");
            radial.Press(1,false);radial.Tick(1,RadialItem.Tap);check(radial.Tick(1.501,RadialItem.Tap)&&radial.Level==RadialLevel.TapWidth,"070 type submenu still needs only the 0.5s dwell");

            var arbitrary=new AuthoringGrid(new BeatTimeline(SpcDocument.Parse("chart(120,4)\nbpm(1123,150,3)\n").ReadEvents()));
            foreach(int division in new[]{1,4,6,7,8,11,12,96,1024})
            {
                var lines=arbitrary.Between(1123,1523,division);
                check(lines.First().TimeMs==1123&&lines.Last().TimeMs==1523&&lines.Select(q=>q.TimeMs).Distinct().Count()==lines.Count,"070 arbitrary subdivision exact endpoints/dedup "+division);
                check(arbitrary.Nearest(1123,division).TimeMs==1123,"070 arbitrary subdivision retains timing origin "+division);
            }
            var twelfth=arbitrary.Between(1123,1523,12);check(twelfth.Any(q=>Math.Abs(q.LocalBeat-.25)<1e-8)&&twelfth.Any(q=>Math.Abs(q.LocalBeat-.5)<1e-8),"070 1/12 grid includes quarter/half reference lines");
            var connected=SpcDocument.Parse("chart(120,4)\nskyarea(1000,25,100,20,50,100,20,0,0,1000,1)\nskyarea(2000,50,100,20,60,100,20,0,0,1000,99)\nskyarea(3001,60,100,20,60,100,20,0,0,500,99)\nskyarea(2000,90,100,10,90,100,10,0,0,1000,1)\n").ReadEvents();
            var connections=new SkyConnections(connected);check(connections.Starts.SetEquals(new[]{1,2}),"080 first per explicit group, not geometric continuity (supersedes 070)");
            var plan=new HitSoundPlan(connected,SkyAttackPolicy.ContinuousRegionStart);check(plan.Shots.Where(q=>q.Kind==HitSoundKind.SkyHit).Select(q=>q.SourceId).OrderBy(x=>x).SequenceEqual(connections.Starts.OrderBy(x=>x)),"070 attack sounds and bracket heads share region starts");
            var buses=SpcDocument.Parse("chart(120,4)\nhold(1000,1,1,3000)\nskyarea(1500,50,100,20,50,100,20,0,0,3000)\n").ReadEvents();
            check(new HitSoundPlan(buses,SkyAttackPolicy.ContinuousRegionStart).Holds.Count(q=>q.Start<=2000&&q.End>2000)==2,"070 independent floor and Sky sustain buses still overlap");
            check(AudioVoiceBudget.MusicPriority<AudioVoiceBudget.HoldPriority&&AudioVoiceBudget.HoldPriority<AudioVoiceBudget.ShotPriority,"070 music has strictly highest priority");
            foreach(int real in new[]{1,2,4,8,16,32,64,128})check(AudioVoiceBudget.Shots(real)+AudioVoiceBudget.Holds(real)+1<=real&&AudioVoiceBudget.Shots(real)<=20,"070 sound budget reserves music at real voice count "+real);
        }
    }
}
