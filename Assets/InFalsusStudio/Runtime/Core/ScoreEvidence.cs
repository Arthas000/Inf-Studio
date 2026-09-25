using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace InFalsusStudio.Core
{
    public sealed class ScoreObservation
    {public double TimeMs;public long Score;public int Combo;}
    // No invented Hold/Sky tick formula. Object count is NEVER used as total combo.
    // Optional measured observations can be replayed, visibly labelled as recorded data.
    public sealed class ScoreEvidence
    {
        public int TotalCombo=-1;
        public string ChartHash="",Note="";
        public readonly List<ScoreObservation> Observations=new List<ScoreObservation>();
        public bool Matches(byte[] chart){return ChartHash==SongFolderProject.Sha256(chart);}
        public long? Maximum {get{return TotalCombo>=0?(long?)(100000000L+TotalCombo):null;}}
        public ScoreObservation At(double ms)
        {ScoreObservation last=null;foreach(var o in Observations){if(o.TimeMs>ms)break;last=o;}return last;}
        public static ScoreEvidence Load(string file,byte[] chart)
        {
            return FromBytes(File.ReadAllBytes(file),chart);
        }
        public static ScoreEvidence FromBytes(byte[] bytes,byte[] chart)
        {
            var d=ProjectJson.Object(ProjectJson.Parse(new System.Text.UTF8Encoding(false,true).GetString(bytes)));
            if(ProjectJson.Text(d,"format")!="InFalsusScoreEvidence"||ProjectJson.Integer(d,"version")!=1)throw new InvalidDataException("Unsupported score evidence format.");
            var e=new ScoreEvidence{TotalCombo=ProjectJson.Integer(d,"totalCombo",-1),ChartHash=ProjectJson.Text(d,"chartSha256"),Note=ProjectJson.Text(d,"note")};
            if(!e.Matches(chart))throw new InvalidDataException("Score evidence belongs to different chart bytes. Re-measure after editing; not using stale counts.");
            if(e.TotalCombo < -1)throw new InvalidDataException("totalCombo must be -1 (unknown) or a measured nonnegative integer.");
            var rows=ProjectJson.Get(d,"observations") as List<object>;double prevTime=-1;int prevCombo=-1;long prevScore=-1;
            if(rows!=null)foreach(var v in rows)
            {
                var r=ProjectJson.Object(v);
                if(!(ProjectJson.Get(r,"score") is double)||!(ProjectJson.Get(r,"timeMs") is double)||!(ProjectJson.Get(r,"combo") is double))
                    throw new InvalidDataException("An observation must explicitly contain timeMs, score and combo; missing values are not zero measurements.");
                double score=ProjectJson.Number(r,"score");var o=new ScoreObservation{TimeMs=ProjectJson.Number(r,"timeMs"),Combo=ProjectJson.Integer(r,"combo"),Score=(long)score};
                if(o.TimeMs<0||o.TimeMs<=prevTime||score<0||score!=o.Score||o.Score<prevScore||o.Combo<prevCombo||o.Combo<0||e.Maximum.HasValue&&o.Score>e.Maximum.Value||e.TotalCombo>=0&&o.Combo>e.TotalCombo)
                    throw new InvalidDataException("Invalid/nonmonotone perfect-play observation. Failed-run combo resets are not supported in this evidence replay.");
                e.Observations.Add(o);prevTime=o.TimeMs;prevCombo=o.Combo;prevScore=o.Score;
            }
            return e;
        }
        public static void WriteTemplate(string file,byte[] chart,int measuredTotal=-1)
        {
            if(measuredTotal < -1)throw new ArgumentOutOfRangeException("measuredTotal");
            var d=new Dictionary<string,object>{{"format","InFalsusScoreEvidence"},{"version",1},{"chartSha256",SongFolderProject.Sha256(chart)},
                {"totalCombo",measuredTotal},{"note","Enter measurements from the SAME game version and chart. No tick/scoring formula is assumed."},{"observations",new object[0]}};
            SongFolderProject.WriteAtomic(file,ProjectJson.Write(d));
        }
        public static string Format(long score)
        {return Math.Max(0,score).ToString("000,000,000",CultureInfo.InvariantCulture).Replace(',','\'');}
    }
}
