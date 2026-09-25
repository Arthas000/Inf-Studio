using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace InFalsusStudio.Core
{
    public enum NoteHandle { Body, Head, Tail, HeadLeft, HeadRight, TailLeft, TailRight, LeftEase, RightEase }

    // Pure editing operations. All positions are chart coordinates, never mesh coordinates.
    // Call through EditHistory.Execute/Preview so a rejected edit cannot partially change a chart.
    public static class EditOperations
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        public static string Number(double x) { return AuthoredNumber.Format(x); }
        private static SpcEvent Find(SpcDocument d, int id)
        {
            var e = d.ReadEvents().FirstOrDefault(x => x.SourceId == id);
            if (e == null) throw new ArgumentException("The selected event no longer exists.");
            return e;
        }
        private static void Finite(double x) { if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentException("Non-finite edit."); }
        public static double Quantize(double x, double step) { return step > 0 ? Math.Round(x/step,MidpointRounding.AwayFromZero)*step : x; }
        public static double Center(SpcEvent e, bool tail = false)
        {
            if (e.Kind == EventKind.SkyArea) return e.Get(tail?4:1)/e.Get(tail?5:2);
            if (e.Kind == EventKind.Flick) return e.Get(1)/e.Get(2);
            return (e.Lane-1+e.GroundWidth*.5)/4;
        }
        public static void Move(SpcDocument doc, IEnumerable<int> ids, double deltaMs, int laneDelta, double airDelta)
        {
            Finite(deltaMs); Finite(airDelta); var selected = new HashSet<int>(ids);
            var all = doc.ReadEvents().Where(e => selected.Contains(e.SourceId)).ToArray();
            if(airDelta!=0)
            {
                double bounded=AirReachBounds.ClampDelta(all,airDelta);
                if(all.Any(e=>e.Kind==EventKind.Tap||e.Kind==EventKind.Hold)&&Math.Abs(bounded-airDelta)>1e-8)
                    throw new ArgumentException("Mixed selection would exceed the air boundary; movement refused as one transaction.");
                airDelta=bounded;
            }
            foreach (var e in all)
            {
                if (e.Kind == EventKind.Chart || e.Kind == EventKind.Unknown || e.Kind == EventKind.Beam) continue;
                if (e.TimeMs + deltaMs < -1e-7) throw new ArgumentException("Move would place an event before 0 ms; no event was changed.");
                if (e.IsNote && ChartMath.InvalidReason(e)!=null) throw new ArgumentException("Fix the invalid event in the raw inspector first.");
                if (e.Kind==EventKind.Tap || e.Kind==EventKind.Hold)
                {
                    int lane=e.Lane+laneDelta; double w=e.GroundWidth;
                    if (lane<0||lane>5||((lane==0||lane==5)?w!=1:lane+w>5))
                        throw new ArgumentException("The whole selection must fit. Move refused instead of shrinking notes.");
                    AuthoredNumber.Set(doc,e.SourceId,e.Kind==EventKind.Tap?2:1,lane);
                }
                else if (e.Kind==EventKind.Flick && airDelta!=0)
                    RationalAirCoordinates.WriteEndpoint(doc,e,false,e.Get(1)/e.Get(2)+airDelta,Math.Abs(e.Get(3)/e.Get(2)));
                else if (e.Kind==EventKind.SkyArea && airDelta!=0)
                {
                    RationalAirCoordinates.WriteEndpoint(doc,e,false,e.Get(1)/e.Get(2)+airDelta,Math.Abs(e.Get(3)/e.Get(2)));
                    RationalAirCoordinates.WriteEndpoint(doc,e,true,e.Get(4)/e.Get(5)+airDelta,Math.Abs(e.Get(6)/e.Get(5)));
                }
                if(deltaMs!=0)AuthoredNumber.Set(doc,e.SourceId,0,Math.Max(0,e.TimeMs+deltaMs));
            }
        }
        // Mouse-time edits round each changed head once, from the gesture ORIGINAL.
        // Width-only gestures never call this. With grid ON, both ends use the same grid;
        // durations may vary by 1 ms across rounded beat intervals. With grid OFF, keep duration.
        public static void PointerMove(SpcDocument doc,IEnumerable<int> ids,double deltaMs,int laneDelta,double airDelta,AuthoringGrid grid=null,int division=4)
        {
            var selected=new HashSet<int>(ids);
            var previous=doc.ReadEvents().Where(e=>selected.Contains(e.SourceId)).ToArray();
            Move(doc,selected,deltaMs,laneDelta,airDelta);
            if(deltaMs!=0)foreach(var e in previous)
            {
                if(e.Kind==EventKind.Chart||e.Kind==EventKind.Beam||e.Kind==EventKind.Unknown)continue;
                double head=grid!=null?grid.Nearest(e.TimeMs+deltaMs,division).TimeMs:AuthoringGrid.RoundMs(e.TimeMs+deltaMs);
                AuthoredNumber.Set(doc,e.SourceId,0,head);
                if(grid!=null&&(e.Kind==EventKind.Hold||e.Kind==EventKind.SkyArea))
                {
                    double tail=grid.Nearest(e.EndMs+deltaMs,division).TimeMs;
                    if(tail<=head)throw new ArgumentException("Grid would collapse this long note. Use a finer division or explicitly edit source text.");
                    AuthoredNumber.Set(doc,e.SourceId,e.Kind==EventKind.Hold?3:9,tail-head);
                }
            }
        }
        public static void Endpoint(SpcDocument doc, int id, bool tail, double timeMs, double centerNorm, int groundLane)
        {
            Finite(timeMs); Finite(centerNorm); var e=Find(doc,id);
            if(AirReachBounds.IsAir(e))
            {
                var range=e.Kind==EventKind.Flick?ChartMath.FlickRange(e):ChartMath.SkyAt(e,tail?e.EndMs:e.TimeMs);
                centerNorm=AirReachBounds.CenterWithin(centerNorm,range.Width);
            }
            if (timeMs<0) throw new ArgumentException("Time must be nonnegative.");
            if (e.Kind==EventKind.Hold || e.Kind==EventKind.SkyArea)
            {
                double head=tail?e.TimeMs:timeMs, end=tail?timeMs:e.EndMs;
                if(end-head<.001) throw new ArgumentException("End must follow start. Choose a later beat line or explicitly edit the source text.");
                AuthoredNumber.Set(doc,id,0,head);AuthoredNumber.Set(doc,id,e.Kind==EventKind.Hold?3:9,end-head);
                if(e.Kind==EventKind.SkyArea)RationalAirCoordinates.WriteEndpoint(doc,e,tail,centerNorm,Math.Abs(e.Get(tail?6:3)/e.Get(tail?5:2)));
            }
            else if(e.Kind==EventKind.Flick)
            {AuthoredNumber.Set(doc,id,0,timeMs);RationalAirCoordinates.WriteEndpoint(doc,e,false,centerNorm,Math.Abs(e.Get(3)/e.Get(2)));}
            else if(e.Kind==EventKind.Tap)
            {
                if(groundLane<0||groundLane>5||((groundLane==0||groundLane==5)?e.GroundWidth!=1:groundLane+e.GroundWidth>5))
                    throw new ArgumentException("Tap does not fit that lane.");
                AuthoredNumber.Set(doc,id,0,timeMs);AuthoredNumber.Set(doc,id,2,groundLane);
            }
        }
        public static void AirEdge(SpcDocument doc, int id, bool tail, bool right, double boundaryNorm)
        {
            Finite(boundaryNorm);var e=Find(doc,id);int ci,si,wi;
            if(e.Kind==EventKind.Flick){ci=1;si=2;wi=3;}
            else if(e.Kind==EventKind.SkyArea){ci=tail?4:1;si=tail?5:2;wi=tail?6:3;}
            else throw new ArgumentException("Not an air note.");
            if(e.Get(si)<=0)throw new ArgumentException("Invalid divisor.");
            AuthoredNumber.AirEdge(doc,e,tail,right,AirReachBounds.Clamp(boundaryNorm));
            AirReachBounds.RequireChanged(doc,new[]{id});
        }
        // Central edge indices use [0,4]. Outer single-width lanes have no width handles.
        public static void GroundEdge(SpcDocument doc, int id, bool right, int boundary)
        {
            var e=Find(doc,id);if(e.Lane==0||e.Lane==5)throw new ArgumentException("Side lanes are always one lane wide.");
            int left=e.Lane-1,r=left+(int)e.GroundWidth;
            if(right)r=boundary;else left=boundary;
            if(left<0||r>4||r<=left)throw new ArgumentException("Ground width must cover 1-4 central lanes.");
            AuthoredNumber.Set(doc,id,e.Kind==EventKind.Tap?2:1,left+1);AuthoredNumber.Set(doc,id,e.Kind==EventKind.Tap?1:2,r-left);
        }
        public static void Mirror(SpcDocument doc, IEnumerable<int> ids)
        {
            var set=new HashSet<int>(ids);
            foreach(var e in doc.ReadEvents().Where(e=>set.Contains(e.SourceId)&&e.IsNote).ToArray())
            {
                if(ChartMath.InvalidReason(e)!=null)throw new ArgumentException("Invalid event cannot be mirrored.");
                if(e.Kind==EventKind.Tap||e.Kind==EventKind.Hold)
                    AuthoredNumber.Set(doc,e.SourceId,e.Kind==EventKind.Tap?2:1,e.Lane==0?5:e.Lane==5?0:6-e.Lane-e.GroundWidth);
                else if(e.Kind==EventKind.Flick)
                {AuthoredNumber.Set(doc,e.SourceId,1,e.Get(2)-e.Get(1));AuthoredNumber.Set(doc,e.SourceId,4,e.Get(4)==4?16:4);}
                else
                {
                    AuthoredNumber.Set(doc,e.SourceId,1,e.Get(2)-e.Get(1));AuthoredNumber.Set(doc,e.SourceId,4,e.Get(5)-e.Get(4));
                    AuthoredNumber.Set(doc,e.SourceId,7,e.Get(8));AuthoredNumber.Set(doc,e.SourceId,8,e.Get(7));
                }
            }
        }
        public static void SetEase(SpcDocument doc, IEnumerable<int> ids, bool right, int code)
        {
            if(code<0||code>2)throw new ArgumentException("Only easing codes 0,1,2 are implemented.");
            var set=new HashSet<int>(ids);foreach(var e in doc.ReadEvents())
                if(set.Contains(e.SourceId)&&e.Kind==EventKind.SkyArea)AuthoredNumber.Set(doc,e.SourceId,right?8:7,GeometricSkyEase.Describe(e,right).Pinned?0:code);
        }
        public static int Add(SpcDocument doc, EventKind kind, double start, double end, int lane, int width,
                              double center, double endCenter, double normalizedWidth, int direction=16, int split=24)
        {
            Finite(start);Finite(end);Finite(center);Finite(endCenter);Finite(normalizedWidth);
            if(start<0)throw new ArgumentException("Time must be nonnegative.");
            if((kind==EventKind.Hold||kind==EventKind.SkyArea)&&end-start<.001)throw new ArgumentException("End must follow start.");
            string line;
            if(kind==EventKind.Tap||kind==EventKind.Hold)
            {
                if(lane<0||lane>5||width<1||((lane==0||lane==5)?width!=1:lane+width>5))throw new ArgumentException("This width does not fit the starting lane.");
                line=kind==EventKind.Tap?"tap("+Number(start)+","+width+","+lane+")":"hold("+Number(start)+","+lane+","+width+","+Number(end-start)+")";
            }
            else
            {
                if(split<=0||normalizedWidth<=0)throw new ArgumentException("Positive split and width required.");
                center=AirReachBounds.CenterWithin(center,normalizedWidth);
                endCenter=AirReachBounds.CenterWithin(endCenter,normalizedWidth);
                double sx,ss,sw,ex,es,ew;
                if(!RationalAirCoordinates.Encode(center,normalizedWidth,split,out sx,out ss,out sw)||
                   !RationalAirCoordinates.Encode(endCenter,normalizedWidth,split,out ex,out es,out ew))
                    throw new ArgumentException("Coordinate ratio exceeds the supported denominator; no partial note was created.");
                if(kind==EventKind.Flick)
                {
                    if(direction!=4&&direction!=16)throw new ArgumentException("Known Flick directions are 4/16.");
                    line="flick("+Number(start)+","+Number(sx)+","+Number(ss)+","+Number(sw)+","+direction+")";
                }
                else if(kind==EventKind.SkyArea)
                    line="skyarea("+Number(start)+","+Number(sx)+","+Number(ss)+","+Number(sw)+","+Number(ex)+","+Number(es)+","+Number(ew)+",0,0,"+Number(end-start)+","+NextGroup(doc)+")";
                else throw new ArgumentException("Unsupported placement tool.");
            }
            return doc.Append(line);
        }
        // A continuation is explicit user authoring, not a claim about game group semantics.
        public static int ContinueSky(SpcDocument doc,int id,double endTime)
        {
            var e=Find(doc,id);if(e.Kind!=EventKind.SkyArea)throw new ArgumentException("Select a SkyArea.");
            Finite(endTime);if(ChartMath.InvalidReason(e)!=null)throw new ArgumentException("Fix invalid SkyArea before continuing it.");
            if(endTime<=e.EndMs)throw new ArgumentException("Continuation end must follow current tail.");
            AirReachBounds.Require(ChartMath.SkyAt(e,e.EndMs));
            string[] a=doc.ArgumentTokens(id);var b=(string[])a.Clone();
            b[0]=Number(e.EndMs);b[1]=a[4];b[2]=a[5];b[3]=a[6];
            b[7]="0";b[8]="0";b[9]=Number(endTime-e.EndMs);
            return doc.Append("skyarea("+string.Join(",",b)+")");
        }
        public static int NextGroup(SpcDocument doc)
        {
            int max=-1;foreach(var e in doc.ReadEvents().Where(e=>e.Kind==EventKind.SkyArea))
            {double g=e.Get(10,-1);if(!double.IsNaN(g)&&g>=0&&g==Math.Round(g)&&g<=int.MaxValue)max=Math.Max(max,(int)g);}
            if(max>=int.MaxValue-2)throw new ArgumentException("No available group ID.");return max+1;
        }
    }

    public sealed class NoteClipboard
    {
        private readonly List<string> lines = new List<string>();
        public double StartMs {get;private set;}
        public double EndMs {get;private set;}
        public int Count {get{return lines.Count;}}
        public void Copy(SpcDocument doc,IEnumerable<int> ids)
        {
            var set=new HashSet<int>(ids);var notes=doc.ReadEvents().Where(e=>set.Contains(e.SourceId)&&e.IsNote).ToArray();
            lines.Clear();if(notes.Length==0)return;StartMs=notes.Min(e=>e.TimeMs);EndMs=notes.Max(e=>e.EndMs);
            foreach(var e in notes)lines.Add(doc.SourceLine(e.SourceId));
        }
        public int[] Paste(SpcDocument doc,double targetTime,bool newGroups)
        {
            if(Count==0)return new int[0];if(double.IsNaN(targetTime)||double.IsInfinity(targetTime)||targetTime<0)throw new ArgumentException("Paste time must be nonnegative.");
            double dt=targetTime-StartMs;var result=new List<int>();Dictionary<int,int> groupMap=new Dictionary<int,int>();int group=-1;
            foreach(string text in lines)
            {
                int id=doc.Append(text);var e=doc.ReadEvents().First(x=>x.SourceId==id);
                AuthoredNumber.Set(doc,id,0,e.TimeMs+dt);
                if(newGroups&&e.Kind==EventKind.SkyArea)
                {
                    double raw=e.Get(10,-1);if(!double.IsNaN(raw)&&raw>=0&&raw==Math.Round(raw)&&raw<int.MaxValue)
                    {
                        int old=(int)raw;if(!groupMap.ContainsKey(old)){if(group<0)group=EditOperations.NextGroup(doc);groupMap.Add(old,checked(group++));}
                        AuthoredNumber.Set(doc,id,10,groupMap[old]);
                    }
                }
                result.Add(id);
            }
            return result.ToArray();
        }
    }

    public static class EditTimeResolver
    {
        // In non-monotone geometry, retain proximity to the selected event/previous drag frame.
        // Return candidate count for UI; never pretend inverse mapping is globally unique.
        public static bool Resolve(ScrollTimeline timeline,double position,double preferred,double limit,
                                   out double time,out int candidates,out bool interval)
        {
            var all=timeline.Inverse(position,0,limit);candidates=all.Count;time=preferred;interval=false;
            double best=double.PositiveInfinity;
            foreach(var c in all)
            {
                double t=Math.Max(c.Start,Math.Min(c.End,preferred)),error=Math.Abs(t-preferred);
                if(error<best){best=error;time=t;interval=c.IsInterval;}
            }
            return all.Count>0;
        }
    }
}
