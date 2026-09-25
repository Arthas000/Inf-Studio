using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InFalsusStudio.Core
{
    // Patch scalar TOKEN SPANS, never deserialize/serialize the complete source file.
    // Important: song.json contains signed 64-bit texture IDs which cannot round-trip
    // through double. Unknown numbers, ordering, indentation, CRLF and BOM stay exact.
    public sealed class LosslessJsonPatch
    {
        private struct Span { public int Start,End; }
        private readonly string text;
        private readonly Dictionary<string,Span> spans=new Dictionary<string,Span>(StringComparer.Ordinal);
        private readonly Dictionary<string,string> replacements=new Dictionary<string,string>(StringComparer.Ordinal);
        private int position;
        public LosslessJsonPatch(byte[] bytes)
        {
            if(bytes==null||bytes.Length>32*1024*1024)throw new InvalidDataException("Metadata is absent or too large.");
            text=new UTF8Encoding(false,true).GetString(bytes);ProjectJson.Parse(text);
            position=text.Length>0&&text[0]=='\uFEFF'?1:0;Value("",0);
        }
        private static string Child(string parent,string key){return parent+"/"+key.Replace("~","~0").Replace("/","~1");}
        private void Space(){while(position<text.Length&&char.IsWhiteSpace(text[position]))position++;}
        private string ReadString()
        {
            int start=position++;while(position<text.Length){char c=text[position++];if(c=='\\'){position++;continue;}if(c=='"')return (string)ProjectJson.Parse(text.Substring(start,position-start));}
            throw new InvalidDataException("Unterminated JSON string.");
        }
        private void Value(string path,int depth)
        {
            if(depth>64)throw new InvalidDataException("Metadata nesting too deep.");Space();int start=position;char c=text[position];
            if(c=='{')
            {
                position++;Space();while(text[position]!='}')
                {string key=ReadString();Space();position++;Value(Child(path,key),depth+1);Space();if(text[position]!=',')break;position++;Space();}position++;
            }
            else if(c=='[')
            {
                position++;Space();int index=0;while(text[position]!=']')
                {Value(Child(path,(index++).ToString(CultureInfo.InvariantCulture)),depth+1);Space();if(text[position]!=',')break;position++;Space();}position++;
            }
            else if(c=='"')ReadString();
            else{while(position<text.Length&&text[position]!=','&&text[position]!=']'&&text[position]!='}'&&!char.IsWhiteSpace(text[position]))position++;}
            spans.Add(path,new Span{Start=start,End=position});
        }
        public bool Contains(string path){return spans.ContainsKey(path);}
        public object Get(string path)
        {Span s;return spans.TryGetValue(path,out s)?ProjectJson.Parse(text.Substring(s.Start,s.End-s.Start)):null;}
        public void Set(string path,object value)
        {
            if(!spans.ContainsKey(path))throw new InvalidDataException("Unsupported source metadata path: "+path);
            if(value!=null&&!(value is string)&&!(value is bool)&&!(value is int)&&!(value is double))throw new ArgumentException("Only scalar metadata patches are allowed.");
            string json=ProjectJson.Write(value).TrimEnd('\r','\n');
            var old=Get(path);if(old is string&&value is string&&(string)old==(string)value)return;
            if(old is double&&value is int&&(double)old==(int)value)return;
            replacements[path]=json;
        }
        public void DisplayText(string key,string value)
        {
            string root="/"+key;object old=Get(root);
            if(old is string){Set(root,value);return;}
            string oldDefault=Get(root+"/default") as string;
            if(oldDefault==null)throw new InvalidDataException("No recognized "+key+" text field in song.json. No files changed.");
            Set(root+"/default",value);
            // Update mirror translations, not deliberately distinct translations or blanks.
            foreach(string path in spans.Keys.Where(p=>p.StartsWith(root+"/localized/",StringComparison.Ordinal)).ToArray())
                if(Get(path) is string&&(string)Get(path)==oldDefault)Set(path,value);
        }
        public void Rating(int difficulty,string value)
        {
            int number;if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out number)||number<0||number>99)
                throw new ArgumentException("Difficulty level must be an integer from 0 to 99. MIN/EVO/ULT/FBD is not editable.");
            bool found=false;
            foreach(string list in new[]{"/charts","/song_info/ChartInfos"})
            {
                if(!Contains(list))continue;int hits=0;
                for(int i=0;Contains(list+"/"+i);i++)
                {
                    string row=list+"/"+i;object flag=Get(row+"/Difficulty");
                    if(!(flag is double)||(double)flag!=(1<<difficulty))continue;
                    if(++hits>1)throw new InvalidDataException("Duplicate difficulty entry in source metadata.");
                    if(Contains(row+"/Rating"))Set(row+"/Rating",number);
                    if(Contains(row+"/LevelSectionIndicator"))Set(row+"/LevelSectionIndicator",value);
                    found=true;
                }
            }
            if(!found)throw new InvalidDataException("The selected difficulty has no recognized source metadata entry.");
        }
        public byte[] ToBytes()
        {
            var edits=replacements.Select(r=>new{Part=spans[r.Key],Value=r.Value}).OrderByDescending(r=>r.Part.Start).ToArray();
            var result=new StringBuilder(text);int previous=text.Length;
            foreach(var e in edits)
            {if(e.Part.End>previous)throw new InvalidOperationException("Overlapping JSON token patches.");result.Remove(e.Part.Start,e.Part.End-e.Part.Start);result.Insert(e.Part.Start,e.Value);previous=e.Part.Start;}
            string value=result.ToString();ProjectJson.Parse(value);return new UTF8Encoding(false,true).GetBytes(value);
        }
    }
}
