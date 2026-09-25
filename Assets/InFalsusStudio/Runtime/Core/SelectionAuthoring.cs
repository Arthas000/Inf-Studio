using System;
using System.Collections.Generic;
using System.Linq;

namespace InFalsusStudio.Core
{
    // All methods modify a supplied IN-MEMORY document; the caller owns the one undo transaction.
    public static class SelectionAuthoring
    {
        public static int[] InRange(IEnumerable<SpcEvent> events,double start,double end)
        {
            double a=Math.Min(start,end),b=Math.Max(start,end);
            return events.Where(e=>e.IsNote).Where(e=>{
                double t=e.Kind==EventKind.Hold||e.Kind==EventKind.SkyArea?e.EndMs:e.TimeMs;
                return t>=a-1e-7&&t<=b+1e-7;
            }).Select(e=>e.SourceId).ToArray();
        }
        public static bool SameType(IEnumerable<SpcEvent> source,out EventKind kind)
        {
            kind=EventKind.Unknown;bool first=true;
            foreach(var n in source){if(!n.IsNote)return false;if(first){kind=n.Kind;first=false;}else if(n.Kind!=kind)return false;}
            return !first;
        }
        public static void Field(SpcDocument doc,IEnumerable<int> ids,EventKind kind,string key,string value)
        {
            var set=new HashSet<int>(ids);var notes=doc.ReadEvents().Where(n=>set.Contains(n.SourceId)).ToArray();
            if(notes.Length!=set.Count||notes.Any(n=>!n.IsNote||n.Kind!=kind))throw new ArgumentException("Selection changed / mixed note types.");
            foreach(var n in notes)
            {
                var f=NoteProperties.Capture(doc,n);
                if(key=="bothWidths"&&kind==EventKind.SkyArea){f.Values["sw"]=value;f.Values["ew"]=value;}
                else if(f.Values.ContainsKey(key))f.Values[key]=value;
                else throw new ArgumentException("Field is not shared by these notes: "+key);
                if((kind==EventKind.Tap||kind==EventKind.Hold)&&(key=="width"||key=="lane"))
                {
                    int lane,w;
                    if(!int.TryParse(f.Values["lane"],out lane)||!int.TryParse(f.Values["width"],out w))throw new ArgumentException("Lane / width must be integers.");
                    int resolved,width;
                    if(!GroundPlacement.TryResolve(lane,w,out resolved,out width))throw new ArgumentException("Invalid ground width / lane.");
                    f.Values["lane"]=resolved.ToString();f.Values["width"]=width.ToString();
                }
                f.Apply(doc);
            }
        }
        public static void Align(SpcDocument doc,IEnumerable<int> ids,AuthoringGrid grid,int division)
        {
            if(grid==null)throw new ArgumentNullException("grid");var set=new HashSet<int>(ids);
            var notes=doc.ReadEvents().Where(n=>n.IsNote&&set.Contains(n.SourceId)).ToArray();
            // Validate all endpoints before the first write: no collapsed note is silently removed.
            foreach(var n in notes)if((n.Kind==EventKind.Hold||n.Kind==EventKind.SkyArea)&&grid.Nearest(n.EndMs,division).TimeMs<=grid.Nearest(n.TimeMs,division).TimeMs)
                throw new ArgumentException("Grid would collapse a long note. Increase subdivision; selection left unchanged.");
            foreach(var n in notes)
            {
                double head=grid.Nearest(n.TimeMs,division).TimeMs;
                AuthoredNumber.Set(doc,n.SourceId,0,head);
                if(n.Kind==EventKind.Hold||n.Kind==EventKind.SkyArea)AuthoredNumber.Set(doc,n.SourceId,n.Kind==EventKind.Hold?3:9,grid.Nearest(n.EndMs,division).TimeMs-head);
            }
        }
    }
    // No deletion occurs during cut preview. Cancel just drops this object; the original
    // document, selection and undo stack remain exactly where they were.
    public sealed class FloatingNotes
    {
        private readonly NoteClipboard clipboard=new NoteClipboard();
        private readonly Dictionary<int,string> originals=new Dictionary<int,string>();
        private readonly SpcDocument small;
        private readonly int[] smallIds;
        public readonly int[] SourceIds;
        public readonly bool Cut;
        public readonly double Anchor;
        public int Count {get{return SourceIds.Length;}}
        public FloatingNotes(SpcDocument doc,IEnumerable<int> ids,bool cut)
        {
            Cut=cut;var set=new HashSet<int>(ids);var notes=doc.ReadEvents().Where(n=>n.IsNote&&set.Contains(n.SourceId)).ToArray();
            if(notes.Length==0)throw new ArgumentException("Select notes first.");
            SourceIds=notes.Select(n=>n.SourceId).ToArray();Anchor=notes.Min(n=>n.TimeMs);clipboard.Copy(doc,SourceIds);
            var text=new System.Text.StringBuilder("chart(100,4)\n");
            foreach(var n in notes){string raw=doc.SourceLine(n.SourceId);originals[n.SourceId]=raw;text.Append(raw).Append('\n');}
            small=SpcDocument.Parse(text.ToString());smallIds=small.ReadEvents().Where(n=>n.IsNote).Select(n=>n.SourceId).ToArray();
        }
        public IList<SpcEvent> Preview(double target)
        {
            var d=small.Clone();EditOperations.Move(d,smallIds,target-Anchor,0,0);
            return d.ReadEvents().Where(n=>n.IsNote).ToArray();
        }
        public int[] Place(SpcDocument doc,double target,bool remapGroups)
        {
            if(double.IsNaN(target)||double.IsInfinity(target)||target<0)throw new ArgumentException("Invalid placement time.");
            if(!Cut)return clipboard.Paste(doc,target,remapGroups);
            foreach(var kv in originals)if(!doc.Contains(kv.Key)||doc.SourceLine(kv.Key)!=kv.Value)throw new InvalidOperationException("Cut source changed. Cancel and select again.");
            EditOperations.Move(doc,SourceIds,target-Anchor,0,0);return (int[])SourceIds.Clone();
        }
    }
}
