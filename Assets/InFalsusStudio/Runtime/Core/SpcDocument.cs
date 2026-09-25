using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InFalsusStudio.Core
{
    public enum EventKind { Chart, Tap, Hold, Flick, SkyArea, Bpm, Track, Lane, Beam, Unknown }

    // SourceId is a stable, session-local document identity. It is NOT the current line index.
    // Initial IDs equal zero-based rows for backward compatibility; deletions never renumber IDs.
    public sealed class SpcEvent
    {
        public int SourceId;
        public EventKind Kind;
        public string Name;
        public double[] Args;
        public double TimeMs { get { return Kind == EventKind.Chart ? 0 : Get(0); } }
        public double DurationMs { get { return Kind == EventKind.Hold ? Get(3) : Kind == EventKind.SkyArea ? Get(9) : 0; } }
        public double EndMs { get { return TimeMs + Math.Max(0, DurationMs); } }
        public double Get(int index, double fallback = 0) { return index >= 0 && index < Args.Length ? Args[index] : fallback; }
        public bool IsNote { get { return Kind == EventKind.Tap || Kind == EventKind.Hold || Kind == EventKind.Flick || Kind == EventKind.SkyArea; } }
        public int Lane { get { return (int)Get(Kind == EventKind.Tap ? 2 : 1); } }
        public double GroundWidth { get { return Get(Kind == EventKind.Tap ? 1 : 2); } }
        public override string ToString() { return Name + " @ " + TimeMs.ToString("0.###", CultureInfo.InvariantCulture) + " ms"; }
    }

    // A concrete-syntax document: unchanged lines and all unedited argument tokens survive.
    // UTF-8 BOM and individual CRLF/LF endings are preserved by Load/Save.
    public sealed class SpcDocument
    {
        private sealed class Line { public int Id; public string Text; public string Eol; }
        private int nextId;
        private readonly List<Line> lines = new List<Line>();
        private static readonly Regex Call = new Regex(@"^(\s*)([A-Za-z_][A-Za-z_0-9]*)(\s*)\((.*)\)(.*)$", RegexOptions.CultureInvariant);
        public readonly List<string> Diagnostics = new List<string>();
        public bool HasUtf8Bom { get; private set; }
        public int LineCount { get { return lines.Count; } }
        public int NextId { get { return nextId; } }
        public int SourceIndex(int id) { return lines.FindIndex(l => l.Id == id); }
        public bool Contains(int id) { return SourceIndex(id) >= 0; }
        public int LineIdAt(int index) { return lines[index].Id; }

        public static SpcDocument Parse(string text, bool bom = false)
        {
            if (text == null) throw new ArgumentNullException("text");
            var d = new SpcDocument();
            if (text.Length > 0 && text[0] == '\uFEFF') { bom = true; text = text.Substring(1); }
            d.HasUtf8Bom = bom;
            int p = 0;
            while (p < text.Length)
            {
                int start = p;
                while (p < text.Length && text[p] != '\r' && text[p] != '\n') p++;
                string body = text.Substring(start, p - start), eol = "";
                if (p < text.Length) { eol += text[p++]; if (eol == "\r" && p < text.Length && text[p] == '\n') eol += text[p++]; }
                d.lines.Add(new Line { Id = d.nextId++, Text = body, Eol = eol });
            }
            return d;
        }
        public static SpcDocument Load(string path)
        {
            return FromBytes(File.ReadAllBytes(path));
        }
        public static SpcDocument FromBytes(byte[] b)
        {
            if (b == null) throw new ArgumentNullException("b");
            bool bom = b.Length >= 3 && b[0] == 239 && b[1] == 187 && b[2] == 191;
            return Parse(new UTF8Encoding(false, true).GetString(b, bom ? 3 : 0, b.Length - (bom ? 3 : 0)), bom);
        }
        public string ToText()
        {
            var b = new StringBuilder(); foreach (var l in lines) b.Append(l.Text).Append(l.Eol); return b.ToString();
        }
        public byte[] ToBytes()
        {
            byte[] text = new UTF8Encoding(false).GetBytes(ToText());
            if (!HasUtf8Bom) return text;
            byte[] result = new byte[text.Length + 3]; result[0] = 239; result[1] = 187; result[2] = 191;
            Buffer.BlockCopy(text, 0, result, 3, text.Length); return result;
        }
        public void Save(string path)
        {
            // Same-directory atomic replacement on the supported filesystem. Never delete the
            // existing destination first. A failed replacement leaves the prior file untouched.
            string full = Path.GetFullPath(path), temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, ToBytes());
                if (File.Exists(full)) File.Replace(temp, full, full + ".bak", true);
                else File.Move(temp, full);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public SpcDocument Clone()
        {
            var d = new SpcDocument { HasUtf8Bom = HasUtf8Bom, nextId = nextId };
            foreach (var l in lines) d.lines.Add(new Line { Id = l.Id, Text = l.Text, Eol = l.Eol });
            return d;
        }
        public string SourceLine(int sourceId) { int i = SourceIndex(sourceId); return i >= 0 ? lines[i].Text : ""; }
        public string[] ArgumentTokens(int sourceId)
        {
            Match m = Call.Match(SourceLine(sourceId)); return m.Success ? m.Groups[4].Value.Split(',') : new string[0];
        }
        public void SetNumber(int sourceId, int argIndex, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Non-finite number.");
            string[] old = ArgumentTokens(sourceId); double n;
            if (argIndex >= 0 && argIndex < old.Length && double.TryParse(old[argIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out n) && n == value) return;
            SetToken(sourceId, argIndex, value.ToString("G17", CultureInfo.InvariantCulture));
        }
        public void SetToken(int sourceId, int argIndex, string value)
        {
            int row = SourceIndex(sourceId);
            if (row < 0) throw new ArgumentOutOfRangeException("sourceId");
            double number;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number))
                throw new ArgumentException("Expected a finite invariant-culture number, e.g. 123.5.");
            var l = lines[row]; Match m = Call.Match(l.Text);
            if (!m.Success) throw new ArgumentException("Not a numeric SPC call.");
            string[] a = m.Groups[4].Value.Split(',');
            if (argIndex < 0 || argIndex >= a.Length) throw new ArgumentOutOfRangeException("argIndex");
            string old = a[argIndex]; int front = old.Length - old.TrimStart().Length, back = old.Length - old.TrimEnd().Length;
            // All-whitespace tokens are legal optional fields. Avoid counting the whitespace twice.
            if (front == old.Length) back = 0;
            a[argIndex] = old.Substring(0, front) + value.Trim() + (back > 0 ? old.Substring(old.Length - back) : "");
            l.Text = l.Text.Substring(0, m.Groups[4].Index) + string.Join(",", a) + l.Text.Substring(m.Groups[4].Index + m.Groups[4].Length);
        }
        public int Append(string call)
        {
            if (call.IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new ArgumentException("Append a single line.");
            string ending = lines.FirstOrDefault(x => x.Eol.Length > 0) != null ? lines.First(x => x.Eol.Length > 0).Eol : "\n";
            if (lines.Count > 0 && lines[lines.Count - 1].Eol.Length == 0) lines[lines.Count - 1].Eol = ending;
            int id = nextId++; lines.Add(new Line { Id = id, Text = call, Eol = ending }); return id;
        }
        public void ReplaceSourceLine(int sourceId,string text)
        {
            int i=SourceIndex(sourceId);if(i<0)throw new ArgumentOutOfRangeException("sourceId");
            if(text==null||text.IndexOfAny(new[]{'\r','\n'})>=0)throw new ArgumentException("Edit one SPC statement at a time.");
            if(!Call.IsMatch(text))throw new ArgumentException("Expected a single name(arguments) SPC statement.");
            lines[i].Text=text; // Preserve the line ID, line ending, BOM and every other token.
        }
        public void Delete(int sourceId) { int row = SourceIndex(sourceId); if (row >= 0) lines.RemoveAt(row); }
        public void SetOptionalNumber(int sourceId, int argIndex, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Non-finite number.");
            int row = SourceIndex(sourceId); if (row < 0) throw new ArgumentOutOfRangeException("sourceId");
            Match m = Call.Match(lines[row].Text); if (!m.Success) throw new ArgumentException("Not an SPC call.");
            var a = m.Groups[4].Value.Split(',').ToList();
            if (argIndex < a.Count) { SetNumber(sourceId, argIndex, value); return; }
            if (argIndex > 32) throw new ArgumentOutOfRangeException("argIndex");
            while (a.Count <= argIndex) a.Add(""); a[argIndex] = value.ToString("G17", CultureInfo.InvariantCulture);
            string old = lines[row].Text;
            lines[row].Text = old.Substring(0,m.Groups[4].Index)+string.Join(",",a)+old.Substring(m.Groups[4].Index+m.Groups[4].Length);
        }

        public List<SpcEvent> ReadEvents()
        {
            Diagnostics.Clear(); var events = new List<SpcEvent>();
            for (int i = 0; i < lines.Count; i++)
            {
                string raw = lines[i].Text; Match m = Call.Match(raw);
                if (!m.Success) { if (raw.Trim().Length > 0 && !raw.TrimStart().StartsWith("//",StringComparison.Ordinal) && !raw.TrimStart().StartsWith("#",StringComparison.Ordinal)) Diagnostics.Add("Line " + (i + 1) + ": opaque text preserved."); continue; }
                string name = m.Groups[2].Value; string[] tokens = m.Groups[4].Value.Split(',');
                EventKind kind;
                if (!Enum.TryParse(name, true, out kind)) kind = EventKind.Unknown;
                int required = RequiredCount(kind);
                var a = new double[tokens.Length]; bool bad = tokens.Length < required;
                for (int j = 0; j < tokens.Length; j++)
                {
                    double v;
                    if (!double.TryParse(tokens[j].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) || double.IsNaN(v) || double.IsInfinity(v))
                    { a[j] = double.NaN; if (j < required) bad = true; }
                    else a[j] = v;
                }
                if (kind == EventKind.Beam || kind == EventKind.Unknown || bad)
                {
                    Diagnostics.Add("Line " + (i + 1) + ": " + name + " is not rendered; raw text retained.");
                    kind = kind == EventKind.Beam ? kind : EventKind.Unknown;
                }
                events.Add(new SpcEvent { SourceId = lines[i].Id, Name = name, Kind = kind, Args = a });
            }
            if (!events.Any(x => x.Kind == EventKind.Chart)) Diagnostics.Add("No valid chart(bpm,beats) header.");
            return events;
        }
        private static int RequiredCount(EventKind k)
        {
            switch (k)
            {
                case EventKind.Chart: case EventKind.Bpm: case EventKind.Track: return 2;
                case EventKind.Tap: case EventKind.Lane: return 3;
                case EventKind.Hold: return 4;
                case EventKind.Flick: return 5;
                case EventKind.SkyArea: return 10;
                default: return 0;
            }
        }
        public static string[] FieldNames(EventKind k)
        {
            switch (k)
            {
                case EventKind.Tap: return new[] { "time_ms", "width", "lane" };
                case EventKind.Hold: return new[] { "time_ms", "lane", "width", "duration_ms" };
                case EventKind.Flick: return new[] { "time_ms", "center_x", "x_split", "width", "direction (4 R / 16 L)" };
                case EventKind.SkyArea: return new[] { "time_ms", "start_center", "start_split", "start_width", "end_center", "end_split", "end_width", "left_ease", "right_ease", "duration_ms", "group_id" };
                case EventKind.Chart: return new[] { "bpm", "beats_per_bar" };
                case EventKind.Track: return new[] { "time_ms", "speed" };
                case EventKind.Bpm: return new[] { "time_ms", "bpm", "beats_per_bar", "reserved" };
                case EventKind.Lane: return new[] { "time_ms", "lane", "enabled" };
                default: return new string[0];
            }
        }
    }

    // Preview is always rebuilt from the original gesture, not from the preceding mouse frame.
    // One drag = one undo entry; Escape/capture loss restores the pre-gesture document.
    public sealed class EditHistory
    {
        private readonly List<SpcDocument> undo = new List<SpcDocument>(), redo = new List<SpcDocument>();
        private SpcDocument transactionStart;
        public SpcDocument Document { get; private set; }
        public bool InTransaction { get { return transactionStart != null; } }
        public int Revision { get; private set; }
        public int UndoCount { get { return undo.Count; } }
        public int RedoCount { get { return redo.Count; } }
        public int Limit = 200;
        public Action<SpcDocument,SpcDocument> BeforeCommit;
        public EditHistory(SpcDocument d) { Document = d ?? throw new ArgumentNullException("d"); }
        private void PushUndo(SpcDocument doc)
        { undo.Add(doc); if (undo.Count > Math.Max(1,Limit)) undo.RemoveAt(0); redo.Clear(); }
        public void Execute(Action<SpcDocument> edit)
        {
            if (InTransaction) throw new InvalidOperationException("Finish or cancel the current drag first.");
            var next = Document.Clone(); edit(next); if(BeforeCommit!=null)BeforeCommit(Document,next);
            if (next.ToText() == Document.ToText() && next.HasUtf8Bom == Document.HasUtf8Bom) return;
            PushUndo(Document); Document = next; Revision++;
        }
        public void Begin()
        { if (InTransaction) throw new InvalidOperationException("Nested edit transaction."); transactionStart = Document; }
        public void Preview(Action<SpcDocument> edit)
        {
            if (!InTransaction) throw new InvalidOperationException("No edit transaction.");
            var next = transactionStart.Clone(); edit(next); if(BeforeCommit!=null)BeforeCommit(transactionStart,next); Document = next; Revision++;
        }
        public bool Commit()
        {
            if (!InTransaction) return false;
            var previous = transactionStart; transactionStart = null;
            if (previous.ToText() == Document.ToText()) { Document = previous; return false; }
            PushUndo(previous); Revision++; return true;
        }
        public void Cancel() { if (!InTransaction) return; Document = transactionStart; transactionStart = null; Revision++; }
        public bool Undo()
        { Cancel(); if (undo.Count == 0) return false; redo.Add(Document); Document = undo[undo.Count-1]; undo.RemoveAt(undo.Count-1); Revision++; return true; }
        public bool Redo()
        { Cancel(); if (redo.Count == 0) return false; undo.Add(Document); Document = redo[redo.Count-1]; redo.RemoveAt(redo.Count-1); Revision++; return true; }
    }
}
