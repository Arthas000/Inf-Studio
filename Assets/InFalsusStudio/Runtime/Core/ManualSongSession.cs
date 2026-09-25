using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    /// <summary>
    /// Explicit-save boundary. Project fields and difficulty Documents are working memory.
    /// Only Save() writes existing project files. Switching slots/discarding/disposal do not.
    /// Byte snapshots are captured on open, not at Save time, so external edits are detected.
    /// </summary>
    public sealed class ManualSongSession
    {
        private readonly SongFolderProject project;
        private readonly Dictionary<string,byte[]> baseline=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,byte[]> pending=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
        private string savedTitle,savedArtist;
        private readonly string[] savedRatings=new string[4];
        private double savedOffset;
        private sealed class Write {public string Path;public byte[] Before,After;public bool Attempted;}
        public ManualSongSession(SongFolderProject value)
        {
            project=value??throw new ArgumentNullException("value");
            Remember(project.Resolve(SongFolderProject.ManifestFile));Remember(project.Resolve("song.json"));
            foreach(var d in project.Difficulties)
            {
                Remember(d.WorkingPath);
                Remember(project.Resolve(".ifstudio/Score/"+d.Index+".json"));
                if(d.Document==null||!Same(d.Document.ToBytes(),baseline[d.WorkingPath]))
                    throw new IOException("Working chart changed while opening: "+d.WorkingPath);
            }
            RememberMetadata();
        }
        private static byte[] Read(string path){return File.Exists(path)?File.ReadAllBytes(path):null;}
        private static bool Same(byte[] a,byte[] b){return a==null?b==null:b!=null&&a.SequenceEqual(b);}
        private void Remember(string path){baseline[path]=Read(path);}
        private void RememberMetadata()
        {
            savedTitle=project.Title;savedArtist=project.Artist;savedOffset=project.AudioOffsetMs;
            for(int i=0;i<4;i++)savedRatings[i]=project.Difficulties[i].Rating;
        }
        public byte[] SavedChartBytes(int index)
        {return (byte[])baseline[project.Difficulties[index].WorkingPath].Clone();}
        public bool MetadataDirty
        {get{return project.Title!=savedTitle||project.Artist!=savedArtist||project.AudioOffsetMs!=savedOffset||Enumerable.Range(0,4).Any(i=>project.Difficulties[i].Rating!=savedRatings[i]);}}
        public bool ChartDirty(int index)
        {var d=project.Difficulties[index];return d.Document!=null&&!Same(d.Document.ToBytes(),baseline[d.WorkingPath]);}
        public bool HasChanges
        {get{return MetadataDirty||Enumerable.Range(0,4).Any(ChartDirty)||pending.Any(p=>!Same(p.Value,baseline[p.Key]));}}
        public int[] DirtyDifficulties {get{return Enumerable.Range(0,4).Where(ChartDirty).ToArray();}}
        // Measured evidence edits also stay in memory until the ordinary Save command.
        public byte[] EvidenceBytes(int index)
        {
            string path=project.Resolve(".ifstudio/Score/"+index+".json");byte[] bytes;
            return pending.TryGetValue(path,out bytes)?bytes:baseline[path];
        }
        public void StageEvidence(int index,byte[] bytes)
        {
            if(index<0||index>3||bytes==null)throw new ArgumentException("Invalid evidence draft.");
            ProjectJson.Parse(new UTF8Encoding(false,true).GetString(bytes));
            pending[project.Resolve(".ifstudio/Score/"+index+".json")]=(byte[])bytes.Clone();
        }
        private static void Match(string path,byte[] bytes)
        {if(!Same(Read(path),bytes))throw new IOException("File changed outside this session; nothing is silently overwritten: "+path);}
        private void Add(List<Write> writes,string path,byte[] bytes)
        {
            project.Resolve(project.Relative(path));byte[] before=baseline[path];
            if(!Same(before,bytes))writes.Add(new Write{Path=path,Before=before,After=bytes});
        }
        /// <summary>Only call in response to explicit Save/Ctrl+S or a Save confirmation.</summary>
        public string Save(Action<int,string> beforeReplaceForTests=null)
        {
            var writes=new List<Write>();
            foreach(var d in project.Difficulties)
            {
                if(d.Document==null)throw new InvalidOperationException("Missing in-memory chart "+d.Index);
                if(!d.Document.ReadEvents().Any(e=>e.Kind==EventKind.Chart))throw new InvalidDataException("Chart header missing in difficulty "+d.Index);
                Add(writes,d.WorkingPath,d.Document.ToBytes());
            }
            string source=project.Resolve("song.json");byte[] original=baseline[source];
            bool sourceChange=project.Title!=savedTitle||project.Artist!=savedArtist||Enumerable.Range(0,4).Any(i=>project.Difficulties[i].Rating!=savedRatings[i]);
            if(sourceChange&&original!=null)
            {
                var patch=new LosslessJsonPatch(original);
                if(project.Title!=savedTitle)patch.DisplayText("title",project.Title);
                if(project.Artist!=savedArtist)patch.DisplayText("artist",project.Artist);
                for(int i=0;i<4;i++)if(project.Difficulties[i].Rating!=savedRatings[i])patch.Rating(i,project.Difficulties[i].Rating);
                Add(writes,source,patch.ToBytes());
            }
            foreach(var entry in pending)Add(writes,entry.Key,entry.Value);
            string manifest=project.Resolve(SongFolderProject.ManifestFile);
            Add(writes,manifest,new UTF8Encoding(false).GetBytes(project.ManifestText()));
            // Preflight the entire batch before creating a recovery directory or replacing anything.
            foreach(var w in writes)Match(w.Path,w.Before);
            if(writes.Count==0){RememberMetadata();pending.Clear();return "Already saved; no file writes needed.";}
            string backup=project.Resolve(".ifstudio/SaveHistory/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            var journal=new List<object>();
            for(int i=0;i<writes.Count;i++)
            {
                var w=writes[i];string prefix=i.ToString("D2",CultureInfo.InvariantCulture);
                if(w.Before!=null)File.WriteAllBytes(Path.Combine(backup,prefix+".before"),w.Before);
                File.WriteAllBytes(Path.Combine(backup,prefix+".after"),w.After);
                journal.Add(new Dictionary<string,object>{{"path",project.Relative(w.Path)},{"before",w.Before==null?null:prefix+".before"},{"after",prefix+".after"}});
            }
            File.WriteAllText(Path.Combine(backup,"files.json"),ProjectJson.Write(journal),new UTF8Encoding(false));
            try
            {
                for(int i=0;i<writes.Count;i++)
                {
                    var w=writes[i];if(beforeReplaceForTests!=null)beforeReplaceForTests(i,w.Path);
                    Match(w.Path,w.Before);Directory.CreateDirectory(Path.GetDirectoryName(w.Path));
                    w.Attempted=true;SongMetadataEdit.WriteBytesAtomic(w.Path,w.After);
                }
                foreach(var w in writes)Match(w.Path,w.After);
                File.WriteAllText(Path.Combine(backup,"COMMITTED.txt"),"Explicit project save. Multi-file recovery snapshots; not a power-loss-atomic transaction.");
            }
            catch(Exception error)
            {
                string failures="";
                foreach(var w in writes.AsEnumerable().Reverse().Where(w=>w.Attempted))
                {
                    try
                    {
                        byte[] current=Read(w.Path);if(Same(current,w.Before))continue;
                        if(!Same(current,w.After))throw new IOException("External modification; refusing rollback over it.");
                        if(w.Before==null)File.Delete(w.Path);else SongMetadataEdit.WriteBytesAtomic(w.Path,w.Before);
                    }
                    catch(Exception ex){failures+=" ["+project.Relative(w.Path)+": "+ex.Message+"]";}
                }
                throw new IOException("Save failed. Drafts remain unsaved in memory. Recovery: "+backup+". "+error.Message+failures,error);
            }
            foreach(var w in writes)baseline[w.Path]=(byte[])w.After.Clone();
            pending.Clear();RememberMetadata();
            return "Saved project (all modified difficulties + metadata). Backup: "+backup;
        }
    }
}
