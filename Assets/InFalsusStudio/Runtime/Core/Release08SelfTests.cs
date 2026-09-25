using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    public static class Release08SelfTests
    {
        private static SpcEvent Find(SpcDocument d,int id){return d.ReadEvents().First(n=>n.SourceId==id);}
        private static bool Near(double a,double b){return Math.Abs(a-b)<1e-9;}
        private static string Sky(int t,int end,int x,int group){return "skyarea("+t+","+x+",100,20,"+x+",100,20,0,0,"+(end-t)+","+group+")";}
        public static void Run(Action<bool,string> check)
        {
            var grouped=SpcDocument.Parse("chart(120,4)\n"+Sky(1000,1500,25,7)+"\n"+Sky(2500,3000,75,7)+"\n"+Sky(1500,2000,25,8)+"\n");
            var heads=SkyGroups.Heads(grouped.ReadEvents());
            check(heads.SetEquals(new[]{1,3}),"080 same group across a time/space gap has exactly one head; touching different source groups are not reclassified on LOAD");
            var sounds=new HitSoundPlan(grouped.ReadEvents(),SkyAttackPolicy.ContinuousRegionStart);
            check(sounds.Shots.Where(n=>n.Kind==HitSoundKind.SkyHit).Select(n=>n.SourceId).OrderBy(x=>x).SequenceEqual(heads.OrderBy(x=>x)),"080 bracket and hit policy share group-first IDs");
            check(sounds.Holds.Count==2,"080 group gap does not loop sustain across silence");
            var h=new EditHistory(grouped.Clone());h.BeforeCommit=SkyGroups.NormalizeAuthoredChanges;
            byte[] input=h.Document.ToBytes();int created=-1;
            h.Execute(d=>created=EditOperations.Add(d,EventKind.SkyArea,3000,3500,1,1,.75,.65,.2,16,100));
            check(Find(h.Document,created).Get(10)==7,"080 new touching overlap inherits predecessor group");
            check(h.UndoCount==1&&h.Undo()&&h.Document.ToBytes().SequenceEqual(input),"080 implicit group join is in the SAME undo transaction");
            check(h.Redo()&&Find(h.Document,created).Get(10)==7,"080 redo restores group join");
            h.Execute(d=>EditOperations.Endpoint(d,created,false,3100,.75,1));
            check(Find(h.Document,created).Get(10)==7,"080 breaking time connection does NOT split group");
            int separate=-1;
            h.Execute(d=>separate=EditOperations.Add(d,EventKind.SkyArea,3500,4000,1,1,.2,.2,.1,16,100));
            check(Find(h.Document,separate).Get(10)==9,"080 touching TIME with disjoint SPACE gets max+1 group");
            h.Execute(d=>EditOperations.Endpoint(d,separate,false,3500,.65,1));
            check(Find(h.Document,separate).Get(10)==7,"080 moving successor into predecessor footprint joins immediately");
            h.Execute(d=>EditOperations.Endpoint(d,separate,false,3500,.2,1));
            check(Find(h.Document,separate).Get(10)==7,"080 breaking spatial overlap also retains group");
            var upstream=new EditHistory(SpcDocument.Parse("chart(120,4)\n"+Sky(1000,2000,20,2)+"\n"+Sky(2000,3000,75,9)+"\n"+Sky(3000,4000,75,9)+"\n"));
            upstream.BeforeCommit=SkyGroups.NormalizeAuthoredChanges;
            upstream.Execute(d=>EditOperations.Endpoint(d,1,true,2000,.75,1));
            check(Find(upstream.Document,2).Get(10)==2&&Find(upstream.Document,3).Get(10)==2,"080 editing predecessor propagates group through unchanged connected successors");
            int uc=upstream.UndoCount;upstream.Begin();upstream.Preview(d=>EditOperations.Endpoint(d,2,false,2000,.65,1));upstream.Cancel();
            check(upstream.UndoCount==uc&&Find(upstream.Document,2).Get(10)==2,"080 cancelled group-aware drag leaves no undo entry");
            var ungrouped=SpcDocument.Parse("chart(120,4)\nskyarea(1000,50,100,10,50,100,10,0,0,500)\nskyarea(2000,50,100,10,50,100,10,0,0,500,-1)\n");
            check(SkyGroups.Heads(ungrouped.ReadEvents()).Count==2,"080 missing/-1 IDs are independent, not one giant group");
            var early=SpcDocument.Parse("chart(120,4)\n"+Sky(2000,2500,50,8)+"\n"+Sky(1000,1500,50,8)+"\n");
            check(SkyGroups.Heads(early.ReadEvents()).SetEquals(new[]{2}),"080 first means chronological start, not arbitrary source order");
            var empty=SpcDocument.Parse("chart(100,4)\n");int id=EditOperations.Add(empty,EventKind.SkyArea,1000,2000,1,1,.5,.5,.1,16,100);
            check(Find(empty,id).Get(10)==0,"080 first authored Sky uses group zero");
            foreach(int n in new[]{1,2,3,7,12,24,100,333,4096})
            {
                var grid=new AirAuthoringGrid();grid.SetDivision(n);
                check(grid.Ticks().Count()==n+1&&grid.Ticks().First()==0&&grid.Ticks().Last()==1,"080 1/N grid endpoints/count "+n);
                for(int k=0;k<=n;k+=Math.Max(1,n/13))
                {double x=k/(double)n;check(Near(grid.Snap(x),x),"080 idempotent exact grid rational "+k+"/"+n);}
            }
            var thirds=new AirAuthoringGrid();thirds.SetDivision(3);
            double center=thirds.Snap(.32),px,ps,pw;
            check(RationalAirCoordinates.Encode(center,.1,100,out px,out ps,out pw)&&px==10&&ps==30&&pw==3,"080 third with 10% width encodes as x10/split30/width3, not recurring decimals");
            var rd=SpcDocument.Parse("chart(120,4)\nskyarea(1000.1234567890123,50,100,10,1,2,1,0,0,1000,4)\n");
            RationalAirCoordinates.WriteEndpoint(rd,Find(rd,1),false,center,.1);
            var re=Find(rd,1);
            check(Near(re.Get(1)/re.Get(2),1d/3)&&Near(re.Get(3)/re.Get(2),.1)&&re.Get(5)==2,"080 only edited endpoint denominator changes; tail split preserved");
            check(rd.ArgumentTokens(1)[0]=="1000.1234567890123","080 mouse coordinate change does not round source time");
            RationalAirCoordinates.WriteEndpoint(rd,re,false,center,.1);
            check(rd.ArgumentTokens(1)[1]=="10"&&rd.ArgumentTokens(1)[2]=="30","080 repeated identical grid edit does not accumulate fraction noise");
            var comments=SpcDocument.Parse("chart(100,4)\r\n// source note id 1\n# importer\nopaque(123)\nnot a call\n");string original=comments.ToText();comments.ReadEvents();
            check(comments.ToText()==original&&!comments.Diagnostics.Any(v=>v.StartsWith("Line 2:")||v.StartsWith("Line 3:")),"080 comments silent but byte preserved");
            check(comments.Diagnostics.Count>0,"080 genuine unsupported data notices retained");
            var draft=new PointPlacement(EventKind.SkyArea);var menu=new RadialMenuState{InPointMode=true};
            draft.ChooseTime(1000,1,1);check(draft.Pending,"080 RMB cancellation covers StartX phase");draft.Reset(EventKind.SkyArea);
            check(!draft.Pending&&menu.InPointMode&&!menu.Pressed,"080 cancellation preserves Point mode without opening a menu");
            draft.ChooseTime(1000,1,1);draft.ChooseX(.5);draft.ChooseTime(2000,1,1);draft.ChooseX(.5);draft.Reset(EventKind.SkyArea);
            menu.Press(0,false);check(menu.Level==RadialLevel.Point,"080 after successful placement next RMB opens note-type menu");
            menu.Release(RadialItem.NoCreate);check(menu.InPointMode&&!menu.Pressed,"080 NoCreate leaves point mode but closes menu");
        }
    }
}
