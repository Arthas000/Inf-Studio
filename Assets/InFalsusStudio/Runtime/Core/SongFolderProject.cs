using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace InFalsusStudio.Core
{
    public sealed class SongAudioConflict : IOException
    {
        public readonly string[] Files;
        public readonly string DeclaredPrimary;
        public readonly bool CanUseDeclaredPrimary;
        public SongAudioConflict(string[] files,string primary,bool canUse):base("A song project requires ONE main .mp3/.ogg file. Found "+files.Length+": "+string.Join(", ",files.Select(Path.GetFileName).ToArray()))
        {Files=files;DeclaredPrimary=primary;CanUseDeclaredPrimary=canUse;}
    }
    public sealed class SongDifficulty
    {
        public int Index;
        public string Name,Code,Rating="?",Designer="",SourcePath="",SourceFormat="empty",SourceHash="",WorkingPath="",JacketPath="";
        public int ImportedObjects;
        public SpcDocument Document;
        public readonly List<string> Notices=new List<string>();
    }
    public sealed class SongFolderProject
    {
        public const string ManifestFile="infalsus.studio.json";
        public static readonly string[] Names={"Minimal","Evolved","Ultimate","Forbidden"};
        public static readonly string[] Codes={"MIN","EVO","ULT","FBD"};
        public string Root,Title,Artist,AudioPath,JacketPath="";
        public double AudioOffsetMs;
        public int ActiveDifficulty;
        public bool Existing;
        public readonly SongDifficulty[] Difficulties=new SongDifficulty[4];
        public readonly List<string> Notices=new List<string>();
        public readonly List<string> ExcludedSupplementary=new List<string>();
        public string ManifestPath {get{return Path.Combine(Root,ManifestFile);}}
        public static string Sha256(byte[] bytes)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        public static string FileHash(string file)
        {using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        private static string NormalizeRoot(string root)
        {if(string.IsNullOrWhiteSpace(root))throw new IOException("Choose a song folder, not a ZIP file.");string r=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);if(!Directory.Exists(r))throw new DirectoryNotFoundException(r);return r;}
        public string Relative(string full)
        {
            if(string.IsNullOrEmpty(full))return "";string p=Path.GetFullPath(full),prefix=Root+Path.DirectorySeparatorChar;
            if(!p.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Project path is outside song folder: "+p);
            return p.Substring(prefix.Length).Replace('\\','/');
        }
        public string Resolve(string relative,bool allowExportPrefix=false)
        {
            if(string.IsNullOrWhiteSpace(relative))return "";
            string s=relative.Replace('\\','/');
            if(s.StartsWith("/")||s.Contains(":")||s.Split('/').Any(x=>x==".."))throw new IOException("Unsafe external/traversal metadata path: "+relative);
            string direct=Path.GetFullPath(Path.Combine(Root,s));
            if(allowExportPrefix&&!File.Exists(direct))
            {
                // Supplied extraction paths are Songs/014_enigma/..., while users select
                // the 014_enigma folder itself. Also works when that folder was renamed.
                var parts=s.Split('/');if(parts.Length>=3&&parts[0].Equals("Songs",StringComparison.OrdinalIgnoreCase))s=string.Join("/",parts.Skip(2).ToArray());
                else if(parts.Length>=2&&parts[0].Equals(new DirectoryInfo(Root).Name,StringComparison.OrdinalIgnoreCase))s=string.Join("/",parts.Skip(1).ToArray());
            }
            string path=Path.GetFullPath(Path.Combine(Root,s));Relative(path);
            // Do not follow metadata through directory junctions/symlinks out of the song root.
            string parent=Path.GetDirectoryName(path);
            while(!string.IsNullOrEmpty(parent)&&!parent.Equals(Root,StringComparison.OrdinalIgnoreCase))
            {if(Directory.Exists(parent)&&(File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new IOException("Metadata path crosses a directory link: "+parent);parent=Path.GetDirectoryName(parent);}
            if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Metadata file is a link: "+path);
            return path;
        }
        private static bool MusicFile(string path)
        {string ext=Path.GetExtension(path);return ext.Equals(".mp3",StringComparison.OrdinalIgnoreCase)||ext.Equals(".ogg",StringComparison.OrdinalIgnoreCase);}
        public static SongFolderProject Prepare(string folder,bool permitDeclaredSupplementary=false)
        {
            var p=new SongFolderProject{Root=NormalizeRoot(folder),Title=new DirectoryInfo(NormalizeRoot(folder)).Name,Artist="Unknown artist"};
            string[] audio=Directory.GetFiles(p.Root).Where(MusicFile).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            if(audio.Length==0)throw new SongAudioConflict(audio,"",false);
            if(File.Exists(p.ManifestPath))p.ReadManifest();else p.DiscoverSources();
            string primary=p.AudioPath??"";bool validPrimary=audio.Any(x=>x.Equals(primary,StringComparison.OrdinalIgnoreCase));
            string[] other=audio.Where(x=>!x.Equals(primary,StringComparison.OrdinalIgnoreCase)).ToArray();
            bool allDeclared=validPrimary&&other.All(x=>p.ExcludedSupplementary.Any(y=>x.Equals(y,StringComparison.OrdinalIgnoreCase)));
            if(audio.Length!=1)
            {
                if(!allDeclared||!p.Existing&&!permitDeclaredSupplementary)throw new SongAudioConflict(audio,primary,allDeclared);
                p.Notices.Add("Explicit main-audio selection: "+Path.GetFileName(primary)+". Declared supplementary excluded: "+string.Join(", ",other.Select(Path.GetFileName).ToArray()));
            }
            else
            {
                if(!string.IsNullOrEmpty(primary)&&!validPrimary)throw new IOException("Declared main audio does not exist. Refusing to substitute a different file.");
                p.AudioPath=audio[0];
            }
            if(new FileInfo(p.AudioPath).Length>256L*1024*1024)throw new IOException("Audio exceeds the prototype 256 MiB import limit.");
            for(int i=0;i<4;i++)
            {
                var d=p.Difficulties[i];
                if(!p.Existing&&File.Exists(d.WorkingPath))throw new IOException("Orphaned workspace exists without a manifest. Back it up or recover it before opening: "+d.WorkingPath);
                if(p.Existing)
                {
                    if(!File.Exists(d.WorkingPath))throw new IOException("Missing workspace chart; not replacing it with an empty chart: "+d.WorkingPath);
                    d.Document=SpcDocument.Load(d.WorkingPath);
                    if(!string.IsNullOrEmpty(d.SourcePath)&&File.Exists(d.SourcePath)&&d.SourceHash.Length>0&&d.SourceHash!=FileHash(d.SourcePath))
                        d.Notices.Add("Source file changed since initial import. Keeping the edited workspace; no automatic overwrite.");
                }
                else if(d.SourcePath.Length==0)d.Document=SpcDocument.Parse("chart(100,4)\n");
                else
                {
                    if(!File.Exists(d.SourcePath))throw new IOException("Missing source chart: "+d.SourcePath);
                    d.SourceHash=FileHash(d.SourcePath);
                    if(d.SourceFormat=="icp-json")
                    {var result=IcpJsonImport.Load(d.SourcePath);d.Document=result.Document;d.ImportedObjects=result.ImportedNotes;d.Notices.AddRange(result.Warnings);}
                    else
                    {d.Document=SpcDocument.Load(d.SourcePath);d.ImportedObjects=d.Document.ReadEvents().Count(x=>x.IsNote);}
                }
                if(!d.Document.ReadEvents().Any(x=>x.Kind==EventKind.Chart))throw new InvalidDataException("No valid chart header in "+d.Name);
            }
            return p;
        }
        private SongDifficulty NewSlot(int i)
        {return new SongDifficulty{Index=i,Name=Names[i],Code=Codes[i],WorkingPath=Resolve(".ifstudio/Charts/"+i+"_"+Names[i]+".spc")};}
        private void DiscoverSources()
        {
            for(int i=0;i<4;i++)Difficulties[i]=NewSlot(i);
            string songFile=Path.Combine(Root,"song.json");
            if(File.Exists(songFile))
            {
                var song=ProjectJson.ReadFile(songFile);Title=Localized(ProjectJson.Get(song,"title"),Title);Artist=Localized(ProjectJson.Get(song,"artist"),Artist);
                var a=ProjectJson.Get(song,"audio") as Dictionary<string,object>;
                if(a!=null)AudioPath=Resolve(ProjectJson.Text(a,"path"),true);
                var supplementary=ProjectJson.Get(song,"supplementary_audio") as List<object>;
                if(supplementary!=null)foreach(var v in supplementary){string s=Resolve(ProjectJson.Text(ProjectJson.Object(v),"path"),true);if(s.Length>0)ExcludedSupplementary.Add(s);}
                JacketPath=Jacket(ProjectJson.Get(song,"jackets"));
                var records=ProjectJson.Get(song,"charts") as List<object>;
                if(records!=null)
                {
                    var used=new HashSet<int>();
                    foreach(var v in records)
                    {
                        var r=ProjectJson.Object(v);int mask=ProjectJson.Integer(r,"Difficulty",0);int index=mask==1?0:mask==2?1:mask==4?2:mask==8?3:-1;
                        if(index<0)throw new InvalidDataException("Unknown source Difficulty mask "+mask+" (expected 1/2/4/8).");
                        if(!used.Add(index))throw new InvalidDataException("Duplicate chart slot in song.json: "+index);
                        var d=Difficulties[index];d.Rating=ProjectJson.Text(r,"LevelSectionIndicator",ProjectJson.Number(r,"Rating",0).ToString(CultureInfo.InvariantCulture));d.Designer=ProjectJson.Text(r,"DisplayChartDesigner");
                        string specificJacket=Jacket(ProjectJson.Get(r,"jackets"));d.JacketPath=specificJacket.Length>0?specificJacket:JacketPath;
                        string binary=Resolve(ProjectJson.Text(r,"binary_path"),true),decoded=Resolve(ProjectJson.Text(r,"json_path"),true);
                        if(decoded.Length>0&&File.Exists(decoded)){d.SourcePath=decoded;d.SourceFormat="icp-json";}
                        else if(binary.Length>0&&File.Exists(binary))SetSource(d,binary);
                        else throw new IOException("Metadata chart has no readable source: "+d.Name+". Keep matching decoded JSON next to ICP1 binary.");
                    }
                    ActiveDifficulty=used.Contains(3)?3:used.OrderBy(x=>x).FirstOrDefault();
                    Notices.Add("Metadata difficulty masks 1/2/4/8 mapped to editor indices 0/1/2/3. note_count is objects, NOT final combo.");
                    return;
                }
                Notices.Add("song.json has no exported charts array; scanning plaintext chart filenames.");
            }
            if(JacketPath.Length==0)
                foreach(string name in new[]{"jacket_small.png","jacket_large.png","jacket.png"}){string f=Path.Combine(Root,name);if(File.Exists(f)){JacketPath=f;break;}}
            var candidates=Directory.GetFiles(Root,"*.spc").Concat(Directory.Exists(Path.Combine(Root,"Charts"))?Directory.GetFiles(Path.Combine(Root,"Charts"),"*.spc"):new string[0]).ToArray();
            foreach(string file in candidates)
            {
                string name=Path.GetFileNameWithoutExtension(file);int index=-1;
                for(int i=0;i<4;i++)if(name.Equals(i.ToString(),StringComparison.OrdinalIgnoreCase)||name.StartsWith(Names[i]+"_",StringComparison.OrdinalIgnoreCase)||name.StartsWith(i+"_",StringComparison.OrdinalIgnoreCase)||name.Equals(Names[i],StringComparison.OrdinalIgnoreCase))index=i;
                if(index<0&&candidates.Length==1)index=0;
                if(index<0)throw new IOException("Ambiguous chart filename. Name charts 0.spc..3.spc or Minimal_.../Evolved_.../Ultimate_.../Forbidden_... .");
                if(Difficulties[index].SourcePath.Length>0)throw new IOException("Multiple sources for difficulty "+index);
                SetSource(Difficulties[index],file);
            }
            foreach(var d in Difficulties)d.JacketPath=JacketPath;
        }
        private void SetSource(SongDifficulty d,string file)
        {
            byte[] first=new byte[4];using(var f=File.OpenRead(file))f.Read(first,0,4);
            if(IcpJsonImport.IsBinary(first))
            {string decoded=Path.ChangeExtension(file,".json");if(!File.Exists(decoded))throw new IOException("ICP1 binary cannot be read as text. Missing decoded JSON: "+decoded);d.SourcePath=decoded;d.SourceFormat="icp-json";}
            else{d.SourcePath=file;d.SourceFormat="plaintext-spc";}
        }
        private string Jacket(object value)
        {
            var dict=value as Dictionary<string,object>;if(dict==null)return "";
            foreach(string size in new[]{"small","large"}){var info=ProjectJson.Get(dict,size) as Dictionary<string,object>;if(info==null)continue;string path=Resolve(ProjectJson.Text(info,"path"),true);if(path.Length>0&&File.Exists(path))return path;}
            return "";
        }
        private static string Localized(object value,string fallback)
        {if(value is string)return (string)value;var d=value as Dictionary<string,object>;return d==null?fallback:ProjectJson.Text(d,"default",fallback);}
        private void ReadManifest()
        {
            var m=ProjectJson.ReadFile(ManifestPath);
            if(ProjectJson.Text(m,"format")!="InFalsusStudioSong"||ProjectJson.Integer(m,"version")!=1)throw new InvalidDataException("Unsupported workspace manifest; no files were overwritten.");
            Existing=true;Title=ProjectJson.Text(m,"title",Title);Artist=ProjectJson.Text(m,"artist",Artist);AudioPath=Resolve(ProjectJson.Text(m,"audio"));JacketPath=Resolve(ProjectJson.Text(m,"jacket"));
            AudioOffsetMs=ProjectJson.Number(m,"audioOffsetMs");ActiveDifficulty=Math.Max(0,Math.Min(3,ProjectJson.Integer(m,"activeDifficulty")));
            var excludes=ProjectJson.Get(m,"excludedSupplementary") as List<object>;if(excludes!=null)foreach(var x in excludes){if(!(x is string))throw new InvalidDataException("Bad supplementary path.");ExcludedSupplementary.Add(Resolve((string)x));}
            var slots=ProjectJson.Array(ProjectJson.Get(m,"difficulties"));if(slots.Count!=4)throw new InvalidDataException("Workspace needs four difficulty slots.");
            foreach(var v in slots)
            {
                var r=ProjectJson.Object(v);int i=ProjectJson.Integer(r,"index");if(i<0||i>3||Difficulties[i]!=null)throw new InvalidDataException("Invalid/duplicate difficulty index.");
                var d=NewSlot(i);d.SourcePath=Resolve(ProjectJson.Text(r,"source"));d.SourceHash=ProjectJson.Text(r,"sourceSha256");d.SourceFormat=ProjectJson.Text(r,"sourceFormat","plaintext-spc");
                string work=Resolve(ProjectJson.Text(r,"working"));if(!work.Equals(d.WorkingPath,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Unexpected workspace chart path. Refusing an arbitrary overwrite target.");
                d.Rating=ProjectJson.Text(r,"rating","?");d.Designer=ProjectJson.Text(r,"designer");d.JacketPath=Resolve(ProjectJson.Text(r,"jacket"));d.ImportedObjects=ProjectJson.Integer(r,"importedObjects");
                var warn=ProjectJson.Get(r,"notices") as List<object>;if(warn!=null)d.Notices.AddRange(warn.OfType<string>());Difficulties[i]=d;
            }
        }
        public void CommitInitialWorkspace()
        {
            if(Existing)return;
            // Called only AFTER audio and chart decoding have succeeded. Sources stay immutable.
            if(File.Exists(ManifestPath))throw new IOException("Workspace appeared since preparation. Reopen to avoid overwriting someone else's project.");
            var created=new List<string>();
            try
            {
                foreach(var d in Difficulties)
                {
                    if(File.Exists(d.WorkingPath))throw new IOException("Working chart appeared during import: "+d.WorkingPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(d.WorkingPath));d.Document.Save(d.WorkingPath);created.Add(d.WorkingPath);
                }
                WriteManifest();Existing=true;
            }
            catch{foreach(var f in created)try{File.Delete(f);}catch{}throw;}
        }
        public void WriteManifest()
        {
            Resolve(ManifestFile);
            WriteAtomic(ManifestPath,ManifestText());
        }
        public string ManifestText()
        {
            var slots=new List<object>();foreach(var d in Difficulties)slots.Add(new Dictionary<string,object>{
                {"index",d.Index},{"name",d.Name},{"rating",d.Rating},{"designer",d.Designer},{"source",Relative(d.SourcePath)},
                {"sourceFormat",d.SourceFormat},{"sourceSha256",d.SourceHash},{"working",Relative(d.WorkingPath)},{"jacket",Relative(d.JacketPath)},
                {"importedObjects",d.ImportedObjects},{"notices",d.Notices.ToArray()}});
            var m=new Dictionary<string,object>{{"format","InFalsusStudioSong"},{"version",1},{"title",Title},{"artist",Artist},{"audio",Relative(AudioPath)},
                {"audioOffsetMs",AudioOffsetMs},{"jacket",Relative(JacketPath)},{"activeDifficulty",ActiveDifficulty},
                {"excludedSupplementary",ExcludedSupplementary.Select(Relative).ToArray()},{"difficulties",slots}};
            return ProjectJson.Write(m);
        }
        public static void WriteAtomic(string path,string text)
        {
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllText(temp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
