using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    public struct CursorPose
    {
        public double X;
        public int SourceId,Conflicts;
        public string Phase;
        public CursorPose(double x,int id,string phase,int conflicts=0){X=x;SourceId=id;Phase=phase;Conflicts=conflicts;}
    }
    // Deterministic VISUAL rehearsal, NOT an OS-input bot or certified gameplay judge.
    // Same-time Flick objects form a finite-duration gesture. They cannot all be visited
    // at one mathematical instant: SwipeMs is a visual window, not a known judgement window.
    public sealed class AutoplayCursor
    {
        private SpcEvent[] skies=new SpcEvent[0];
        private readonly List<Cluster> clusters=new List<Cluster>();
        private readonly Dictionary<int,double> visualContacts=new Dictionary<int,double>();
        private sealed class Cluster
        {
            public double Time,End;public int Source,Count,Issues;public string Label;
            public double[] Route;public double[] Lengths;public double Length;
            public Dictionary<int,int> ContactIndices;
        }
        public double ApproachMs=140,SwipeMs=100,SwipeDistance=.055,ReturnMs=85;
        public int ClusterCount {get{return clusters.Count;}}
        public int MultiFlickClusterCount {get{return clusters.Count(c=>c.Count>1);}}
        public int ConstraintIssueCount {get;private set;}
        public void Rebuild(IEnumerable<SpcEvent> source)
        {
            var all=source.Where(e=>ChartMath.InvalidReason(e)==null).ToArray();
            skies=all.Where(e=>e.Kind==EventKind.SkyArea).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId).ToArray();
            clusters.Clear();visualContacts.Clear();ConstraintIssueCount=0;
            foreach(var group in all.Where(e=>e.Kind==EventKind.Flick).GroupBy(e=>e.TimeMs).OrderBy(g=>g.Key))
            {
                var notes=group.OrderBy(e=>e.SourceId).ToArray();int issues,skyId;
                SkyRange allowed=Allowed(group.Key,out skyId,out issues);
                bool left=notes.All(e=>e.Get(4)==16),right=notes.All(e=>e.Get(4)==4);
                var route=new List<double>();var contactIndices=new Dictionary<int,int>();
                if(left||right)
                {
                    // Input direction determines sweep order, NOT thin tail orientation.
                    notes=left?notes.OrderByDescending(e=>ChartMath.FlickRange(e).Center).ToArray():notes.OrderBy(e=>ChartMath.FlickRange(e).Center).ToArray();
                    var targets=new List<double>();
                    foreach(var e in notes)targets.Add(Target(e,allowed,ref issues));
                    double sign=left?-1:1,start=targets[0],end=targets[targets.Count-1];
                    if(notes.Length==1)
                    {
                        double ahead=Math.Max(allowed.Left,Math.Min(allowed.Right,end+sign*SwipeDistance));
                        // At a hard edge, begin slightly inside the same Flick footprint so
                        // an outward gesture remains visible instead of becoming a zero stroke.
                        if(Math.Abs(ahead-start)<1e-7)
                        {
                            var range=ChartMath.FlickRange(notes[0]);
                            start=Math.Max(Math.Max(allowed.Left,range.Left),Math.Min(Math.Min(allowed.Right,range.Right),start-sign*SwipeDistance));
                        }
                        Add(route,start);contactIndices[notes[0].SourceId]=route.Count-1;Add(route,ahead);
                    }
                    else
                    {
                        for(int ni=0;ni<targets.Count;ni++){Add(route,targets[ni]);contactIndices[notes[ni].SourceId]=route.Count-1;}
                        Add(route,Math.Max(allowed.Left,Math.Min(allowed.Right,end+sign*SwipeDistance)));
                    }
                }
                else
                {
                    // Mixed directions: visit each footprint with a correctly signed short
                    // stroke. Reposition legs are visual; no claim of feasible simultaneous input.
                    // Choose next nearest target deterministically, keep every original event.
                    var pending=notes.ToList();double last=allowed.Center;
                    while(pending.Count>0)
                    {
                        var next=pending.OrderBy(e=>Math.Abs(ChartMath.FlickRange(e).Center-last)).ThenBy(e=>e.SourceId).First();pending.Remove(next);
                        double target=Target(next,allowed,ref issues),sign=next.Get(4)==16?-1:1;
                        var footprint=ChartMath.FlickRange(next);
                        double l=Math.Max(allowed.Left,footprint.Left),r=Math.Min(allowed.Right,footprint.Right);
                        if(l>r){Add(route,target);contactIndices[next.SourceId]=route.Count-1;last=target;continue;}
                        double length=Math.Min(Math.Max(0,r-l),SwipeDistance);
                        double start=sign<0?Math.Min(r,Math.Max(l,target+length*.5)):Math.Max(l,Math.Min(r,target-length*.5));
                        double end=Math.Max(l,Math.Min(r,start+sign*length));
                        Add(route,start);Add(route,end);contactIndices[next.SourceId]=route.Count-1;last=end;
                    }
                    issues++; // Expose ambiguous mixed simultaneous semantics, never hide it.
                }
                if(route.Count==0)route.Add(allowed.Center);if(route.Count==1)route.Add(route[0]);
                var c=new Cluster{Time=group.Key,End=group.Key+Math.Max(1,SwipeMs),Source=notes[0].SourceId,Count=notes.Length,Issues=issues,
                    Label=left?"Flick sweep LEFT":right?"Flick sweep RIGHT":"Mixed Flick visual sequence",Route=route.ToArray(),Lengths=new double[route.Count],ContactIndices=contactIndices};
                for(int i=1;i<c.Route.Length;i++){c.Length+=Math.Abs(c.Route[i]-c.Route[i-1]);c.Lengths[i]=c.Length;}
                if(c.Length<1e-10)c.Issues++;
                clusters.Add(c);ConstraintIssueCount+=c.Issues;
            }
            for(int i=0;i+1<clusters.Count;i++)clusters[i].End=Math.Min(clusters[i].End,clusters[i+1].Time);
            foreach(var c in clusters)foreach(var pair in c.ContactIndices)
            {
                double ratio=c.Length>1e-10?c.Lengths[pair.Value]/c.Length:0;
                visualContacts[pair.Key]=c.Time+(c.End-c.Time)*InverseSmooth(ratio);
            }
        }
        // VISUAL contact only. Renderer may hold a simultaneous target at the judge
        // plane until the sweep reaches it; SPC times and scoring are never changed.
        public double VisualContactTime(SpcEvent note)
        {double t;return visualContacts.TryGetValue(note.SourceId,out t)?t:note.TimeMs;}
        private static double InverseSmooth(double value)
        {
            if(value<=0)return 0;if(value>=1)return 1;double lo=0,hi=1;
            for(int i=0;i<48;i++){double mid=(lo+hi)*.5;if(Smooth(mid)<value)lo=mid;else hi=mid;}
            return (lo+hi)*.5;
        }
        private static void Add(List<double> route,double x)
        {x=AirReachBounds.Clamp(x);if(route.Count==0||Math.Abs(route[route.Count-1]-x)>1e-10)route.Add(x);}
        private static double Target(SpcEvent note,SkyRange allowed,ref int issues)
        {
            var r=ChartMath.FlickRange(note);double l=Math.Max(r.Left,allowed.Left),h=Math.Min(r.Right,allowed.Right);
            if(l>h){issues++;return Math.Max(allowed.Left,Math.Min(allowed.Right,r.Center));}
            // Prefer center, but any reachable point in its footprint is a visual contact.
            return Math.Max(l,Math.Min(h,r.Center));
        }
        private SkyRange Allowed(double time,out int source,out int issues)
        {
            source=-1;issues=0;double left=0,right=1;SkyRange fallback=new SkyRange(0,1);
            foreach(var e in skies)
            {
                if(e.TimeMs>time)break;if(e.EndMs<=time)continue;
                var raw=ChartMath.SkyAt(e,time);var r=AirReachBounds.Clip(raw);
                if(raw.Width<0||r.Width<0){issues++;continue;}
                if(AirReachBounds.Violation(e)!=null)issues++;
                source=e.SourceId;fallback=r;left=Math.Max(left,r.Left);right=Math.Min(right,r.Right);
            }
            if(left<=right)return new SkyRange(left,right);
            // Incompatible simultaneous Sky constraints cannot be satisfied by one cursor.
            // Latest active segment is deterministic, warning is explicit. Duplicate equal
            // segments impose the same constraint and DO NOT create a spurious conflict.
            issues++;return fallback;
        }
        // A finished Sky is an air action too. Never resurrect the position of an
        // older Flick when the current Sky's half-open interval ends.
        private SpcEvent LatestSkyExit(double time)
        {
            SpcEvent latest=null;
            foreach(var sky in skies)
            {
                if(sky.TimeMs>time)break;
                if(sky.EndMs<=time && (latest==null || sky.EndMs>latest.EndMs ||
                    sky.EndMs==latest.EndMs && sky.SourceId>latest.SourceId))latest=sky;
            }
            return latest;
        }
        private SkyRange SkyExitRange(SpcEvent last)
        {
            double left=0,right=1;var fallback=AirReachBounds.Clip(ChartMath.SkyAt(last,last.EndMs));
            // Evaluate the limit from the LEFT, exactly at tail coordinates. Using a
            // fixed epsilon in time would introduce velocity-dependent endpoint error.
            foreach(var sky in skies)
            {
                if(sky.TimeMs>=last.EndMs)break;
                if(sky.EndMs<last.EndMs)continue;
                var range=AirReachBounds.Clip(ChartMath.SkyAt(sky,last.EndMs));
                if(range.Width<0)continue;
                left=Math.Max(left,range.Left);right=Math.Min(right,range.Right);
            }
            return left<=right?new SkyRange(left,right):fallback;
        }
        private static double Smooth(double u){u=ChartMath.Clamp01(u);return u*u*(3-2*u);}
        private double RouteAt(Cluster c,double t)
        {
            double s=Smooth((t-c.Time)/Math.Max(1e-8,c.End-c.Time))*c.Length;
            for(int i=1;i<c.Route.Length;i++)if(s<=c.Lengths[i])
            {double d=c.Lengths[i]-c.Lengths[i-1];return c.Route[i-1]+(c.Route[i]-c.Route[i-1])*(d>1e-10?(s-c.Lengths[i-1])/d:1);}
            return c.Route[c.Route.Length-1];
        }
        public CursorPose Evaluate(double timeMs)
        {
            if(double.IsNaN(timeMs)||double.IsInfinity(timeMs))return new CursorPose(.5,-1,"Idle");
            int skyId,issues;var allowed=Allowed(timeMs,out skyId,out issues);
            Cluster active=null,prev=null,next=null;
            foreach(var c in clusters)
            {if(c.Time>timeMs){next=c;break;}if(timeMs<c.End)active=c;else prev=c;}
            double x=skyId>=0?allowed.Center:.5;int id=skyId;string phase=skyId>=0?"Sky center":"Idle";
            if(active!=null)
            {x=RouteAt(active,timeMs);id=active.Source;phase=active.Label+(active.Count>1?" x"+active.Count:"");issues+=active.Issues;}
            else
            {
                var lastSky=skyId<0?LatestSkyExit(timeMs):null;
                bool skyIsLast=lastSky!=null && (prev==null || lastSky.EndMs>=prev.End);
                if(skyIsLast)
                {
                    var exit=SkyExitRange(lastSky);x=exit.Center;id=lastSky.SourceId;phase="Hold Sky exit";
                    // If the Sky ended while returning from a recent swipe, retain the
                    // actual constrained exit position rather than jumping to its center.
                    if(prev!=null && lastSky.EndMs-prev.End<ReturnMs)
                    {
                        double from=prev.Route[prev.Route.Length-1];
                        x=from+(x-from)*Smooth((lastSky.EndMs-prev.End)/Math.Max(1,ReturnMs));
                        x=Math.Max(exit.Left,Math.Min(exit.Right,x));
                    }
                }
                if(prev!=null && !skyIsLast)
                {
                    double from=prev.Route[prev.Route.Length-1];
                    x=skyId>=0?from+(x-from)*Smooth((timeMs-prev.End)/Math.Max(1,ReturnMs)):from;
                    if(skyId<0){phase="Hold last Flick";id=prev.Source;}
                }
                if(next!=null)
                {
                    double start=Math.Max(prev==null?double.NegativeInfinity:prev.End,next.Time-Math.Max(1,ApproachMs));
                    if(timeMs>=start&&next.Time>start)
                    {double u=Smooth((timeMs-start)/(next.Time-start));x+=(next.Route[0]-x)*u;phase="Approach Flick";id=next.Source;}
                }
                // In air-free gaps also approach the next Sky entry, deterministically.
                if(skyId<0&&(next==null||next.Time-timeMs>ApproachMs))
                {
                    var sky=skies.FirstOrDefault(e=>e.TimeMs>timeMs);
                    if(sky!=null&&sky.TimeMs-timeMs<ApproachMs)
                    {double target=AirReachBounds.Clip(ChartMath.SkyAt(sky,sky.TimeMs)).Center;x+=(target-x)*Smooth(1-(sky.TimeMs-timeMs)/Math.Max(1,ApproachMs));id=sky.SourceId;phase="Approach Sky";}
                }
            }
            double clamped=Math.Max(allowed.Left,Math.Min(allowed.Right,AirReachBounds.Clamp(x)));
            if(Math.Abs(clamped-x)>1e-6&&active!=null)phase+=" / boundary-limited";
            return new CursorPose(clamped,id,phase+(skyId>=0&&active!=null?" inside Sky":""),issues);
        }
    }
}
