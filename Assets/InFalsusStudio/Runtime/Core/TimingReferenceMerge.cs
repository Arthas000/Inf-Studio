using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>Repair missing typed timing from USER-CHOSEN paired plaintext.
    /// This is NOT a guess at binary event IDs/byte payloads, and never replaces notes.</summary>
    public static class TimingReferenceMerge
    {
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;
        private static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
        private static bool Equal(double a,double b){return a==b||double.IsNaN(a)&&double.IsNaN(b);}
        public static string[] Prepare(SpcDocument target,SpcDocument reference)
        {
            var src=reference.ReadEvents();var dst=target.ReadEvents();
            var a=src.FirstOrDefault(e=>e.Kind==EventKind.Chart);var b=dst.FirstOrDefault(e=>e.Kind==EventKind.Chart);
            if(a==null||b==null||a.Get(0)!=b.Get(0)||a.Get(1)!=b.Get(1))throw new InvalidDataException("Reference chart header differs; do not mix songs/difficulties.");
            if(src.Any(e=>e.Name=="icp_unknown_note")||dst.Any(e=>e.Name=="icp_unknown_note"))throw new InvalidDataException("Unresolved note records: cannot establish paired-chart identity.");
            if(TickCountResearch.SemanticSignature(src.Where(e=>e.IsNote))!=TickCountResearch.SemanticSignature(dst.Where(e=>e.IsNote)))
                throw new InvalidDataException("Known note content does not match the reference (timing/geometry/group/direction). Reference merge cancelled; use the correct unedited pair.");
            var missing=new List<string>();var known=dst.Where(e=>e.Kind==EventKind.Bpm).ToList();
            foreach(var ev in src.Where(e=>e.Kind==EventKind.Bpm).OrderBy(e=>e.TimeMs).ThenBy(e=>e.SourceId))
            {
                if(!Finite(ev.TimeMs)||ev.TimeMs<0||!Finite(ev.Get(1))||ev.Get(1)<=0||
                    ev.Args.Length>=3&&Finite(ev.Get(2))&&ev.Get(2)!=-1&&ev.Get(2)<=0)
                    throw new InvalidDataException("Reference contains unsupported BPM/meter. No partial merge.");
                var at=known.Where(e=>e.TimeMs==ev.TimeMs).ToArray();
                if(at.Any(e=>!Equal(e.Get(1),ev.Get(1))||!Equal(e.Get(2,-1),ev.Get(2,-1))||!Equal(e.Get(3,-1),ev.Get(3,-1))))
                    throw new InvalidDataException("Existing BPM conflicts at "+ev.TimeMs.ToString("0.###",CI)+" ms. Resolve explicitly in the timing editor.");
                if(at.Length==0){missing.Add(reference.SourceLine(ev.SourceId));known.Add(ev);}
            }
            return missing.ToArray();
        }
        public static int Apply(SpcDocument target,SpcDocument reference)
        {
            // Validate ALL events before adding even one. Call inside History.Execute for one undo.
            var missing=Prepare(target,reference);
            foreach(string raw in missing)target.Append(raw);
            return missing.Length;
        }
        public static string DescribeOpaque(SpcDocument doc,SpcEvent ev)
        {
            if(ev.Name!="icp_event")return "";
            var tok=doc.ArgumentTokens(ev.SourceId);
            string first="ICP event: source line "+(doc.SourceIndex(ev.SourceId)+1)+", t="+ev.TimeMs.ToString("0.###",CI)+", type="+ev.Get(1).ToString(CI)+". ";
            try
            {
                if(tok.Length!=3||tok[2].Length>512*1024)return first+"Raw payload retained.";
                string json=new UTF8Encoding(false,true).GetString(Convert.FromBase64String(tok[2].Trim()));
                var d=ProjectJson.Object(ProjectJson.Parse(json));
                string preview=string.Join(", ",d.Keys.OrderBy(k=>k,StringComparer.Ordinal).Take(16).ToArray());
                return first+"Decoded JSON keys: "+preview+". Not interpreted from its numeric type alone; import paired plaintext BPM if available.";
            }
            catch(Exception ex)when(ex is FormatException||ex is InvalidDataException||ex is DecoderFallbackException)
            {return first+"Payload could not be displayed; original retained.";}
        }
    }
}
