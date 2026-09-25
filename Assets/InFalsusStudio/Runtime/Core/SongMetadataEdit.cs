using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    public static class SongMetadataEdit
    {
        // Applying an editor field is NOT saving. Runtime HUD/Inspector use this method.
        public static string Stage(SongFolderProject project,int index,string title,string artist,string rating)
        {
            if(project==null||index<0||index>=4)throw new ArgumentException("Open a song project first.");
            if(title==null||title.Trim().Length==0||title.Length>256||artist==null||artist.Length>256)
                throw new ArgumentException("Title requires 1..256 characters; artist max 256.");
            if(title.IndexOfAny(new[]{'\r','\n','\0'})>=0||artist.IndexOfAny(new[]{'\r','\n','\0'})>=0)
                throw new ArgumentException("Title/artist must be one line.");
            int level;
            if(rating!=project.Difficulties[index].Rating&&(!int.TryParse(rating,NumberStyles.None,CultureInfo.InvariantCulture,out level)||level<0||level>99))
                throw new ArgumentException("Level must be an integer 0..99. Difficulty code cannot be changed.");
            project.Title=title;project.Artist=artist;project.Difficulties[index].Rating=rating;
            return "Metadata updated IN MEMORY. Save project / Ctrl+S writes files; exit without saving discards it.";
        }
        // Legacy explicit low-level save API, kept for regression tests, NOT called by runtime fields.
        // Kept only for legacy low-level tests. Runtime saves go through ManualSongSession.
        // Staged bytes + conflict checks + recovery copies prevent partial metadata loss.
        public static string Apply(SongFolderProject project,int index,string title,string artist,string rating)
        {
            if(project==null||index<0||index>=4)throw new ArgumentException("Open a song project first.");
            if(title==null||title.Trim().Length==0||title.Length>256||artist==null||artist.Length>256)
                throw new ArgumentException("Title requires 1..256 characters; artist may be empty, maximum 256 characters.");
            if(title.IndexOfAny(new[]{'\r','\n','\0'})>=0||artist.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new ArgumentException("Title/artist must be one line.");
            var difficulty=project.Difficulties[index];bool changeRating=rating!=difficulty.Rating;
            int level;if(changeRating&&(!int.TryParse(rating,NumberStyles.None,CultureInfo.InvariantCulture,out level)||level<0||level>99))
                throw new ArgumentException("Level must be an integer 0..99. The difficulty name cannot be changed.");
            if(title==project.Title&&artist==project.Artist&&!changeRating)return "Metadata unchanged.";
            string source=project.Resolve("song.json"),manifest=project.Resolve(SongFolderProject.ManifestFile);
            byte[] oldSource=File.Exists(source)?File.ReadAllBytes(source):null,oldManifest=File.Exists(manifest)?File.ReadAllBytes(manifest):null;
            byte[] newSource=null;
            if(oldSource!=null)
            {
                var patch=new LosslessJsonPatch(oldSource);
                if(title!=project.Title)patch.DisplayText("title",title);
                if(artist!=project.Artist)patch.DisplayText("artist",artist);
                if(changeRating)patch.Rating(index,rating);
                newSource=patch.ToBytes();
            }
            string oldTitle=project.Title,oldArtist=project.Artist,oldRating=difficulty.Rating;
            byte[] newManifest;
            project.Title=title;project.Artist=artist;difficulty.Rating=rating;
            try{newManifest=new UTF8Encoding(false).GetBytes(project.ManifestText());}
            finally{project.Title=oldTitle;project.Artist=oldArtist;difficulty.Rating=oldRating;}
            string recovery=project.Resolve(".ifstudio/MetadataHistory/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(recovery);
            if(oldSource!=null)File.WriteAllBytes(Path.Combine(recovery,"song.json.before"),oldSource);
            if(oldManifest!=null)File.WriteAllBytes(Path.Combine(recovery,"manifest.before"),oldManifest);
            if(newSource!=null)File.WriteAllBytes(Path.Combine(recovery,"song.json.after"),newSource);
            File.WriteAllBytes(Path.Combine(recovery,"manifest.after"),newManifest);
            bool sourceWritten=false,manifestWritten=false;
            try
            {
                Match(manifest,oldManifest);if(oldSource!=null)Match(source,oldSource);
                if(newSource!=null){sourceWritten=true;WriteBytesAtomic(source,newSource);}
                // Recheck manifest immediately before the second replacement.
                Match(manifest,oldManifest);manifestWritten=true;WriteBytesAtomic(manifest,newManifest);
                Match(manifest,newManifest);if(newSource!=null)Match(source,newSource);
                File.WriteAllText(Path.Combine(recovery,"COMMITTED.txt"),"Only authorized metadata tokens changed. Source charts/audio/jackets were not modified.");
                project.Title=title;project.Artist=artist;difficulty.Rating=rating;
                return oldSource!=null?"Saved manifest + matching song.json fields; original-byte backups in "+recovery:"Saved project manifest (no source song.json exists). Backup: "+recovery;
            }
            catch(Exception failure)
            {
                string rollback="";
                try{if(manifestWritten)RestoreIfOurWrite(manifest,oldManifest,newManifest);}catch(Exception ex){rollback+=" Manifest restore needs manual recovery: "+ex.Message;}
                try{if(sourceWritten)RestoreIfOurWrite(source,oldSource,newSource);}catch(Exception ex){rollback+=" song.json restore needs manual recovery: "+ex.Message;}
                throw new IOException("Metadata save failed: "+failure.Message+rollback+" Backups: "+recovery,failure);
            }
        }
        private static void RestoreIfOurWrite(string path,byte[] before,byte[] after)
        {
            byte[] current=File.Exists(path)?File.ReadAllBytes(path):null;
            if(before==null&&current==null||before!=null&&current!=null&&before.SequenceEqual(current))return;
            if(current==null||!current.SequenceEqual(after))throw new IOException("An external change prevents automatic rollback: "+path);
            if(before==null)File.Delete(path);else WriteBytesAtomic(path,before);
        }
        private static void Match(string path,byte[] expected)
        {
            if(expected==null){if(File.Exists(path))throw new IOException("File appeared during edit: "+path);}
            else if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(expected))throw new IOException("File changed outside the editor: "+path);
        }
        public static void WriteBytesAtomic(string path,byte[] bytes)
        {
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}
                if(File.Exists(path))File.Replace(temp,path,path+".bak",true);else File.Move(temp,path);
                Match(path,bytes);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
