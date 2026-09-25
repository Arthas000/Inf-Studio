using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace InFalsusStudio.Core
{
    // Small dependency-free JSON reader for song manifests and already-decoded chart JSON.
    // Never executes a payload. Duplicate keys, non-finite numbers and excessive nesting fail.
    // Original input files are not rewritten; this writer only writes our own workspace manifest.
    public static class ProjectJson
    {
        private static readonly CultureInfo CI=CultureInfo.InvariantCulture;
        public static Dictionary<string,object> Object(object value)
        {var d=value as Dictionary<string,object>;if(d==null)throw new InvalidDataException("Expected a JSON object.");return d;}
        public static List<object> Array(object value)
        {var a=value as List<object>;if(a==null)throw new InvalidDataException("Expected a JSON array.");return a;}
        public static object Get(Dictionary<string,object> d,string key,object fallback=null)
        {object v;return d!=null&&d.TryGetValue(key,out v)?v:fallback;}
        public static string Text(Dictionary<string,object> d,string key,string fallback="")
        {var x=Get(d,key);return x==null?fallback:x as string??Convert.ToString(x,CI);}
        public static double Number(Dictionary<string,object> d,string key,double fallback=0)
        {object v=Get(d,key);if(v==null)return fallback;if(!(v is double))throw new InvalidDataException("Not a JSON number: "+key);return (double)v;}
        public static int Integer(Dictionary<string,object> d,string key,int fallback=0)
        {double n=Number(d,key,fallback);if(n<int.MinValue||n>int.MaxValue||n!=Math.Truncate(n))throw new InvalidDataException("Not an integer: "+key);return (int)n;}
        public static bool Boolean(Dictionary<string,object> d,string key,bool fallback=false)
        {object v=Get(d,key);if(v==null)return fallback;if(!(v is bool))throw new InvalidDataException("Not a boolean: "+key);return (bool)v;}
        public static object Parse(string text)
        {if(text==null)throw new ArgumentNullException("text");if(text.Length>32*1024*1024)throw new InvalidDataException("JSON exceeds 32 MiB text limit.");return new Reader(text).Read();}
        public static Dictionary<string,object> ReadFile(string file)
        {if(new FileInfo(file).Length>32*1024*1024)throw new InvalidDataException("JSON file too large.");return Object(Parse(File.ReadAllText(file,new UTF8Encoding(false,true))));}
        public static string Write(object value){var b=new StringBuilder();WriteValue(b,value,0);return b.ToString()+"\n";}
        private static void Quoted(StringBuilder b,string s)
        {
            b.Append('"');foreach(char c in s??"")switch(c)
            {case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u").Append(((int)c).ToString("x4",CI));else b.Append(c);break;}b.Append('"');
        }
        private static void WriteValue(StringBuilder b,object v,int depth)
        {
            if(depth>64)throw new InvalidDataException("JSON too deep.");
            if(v==null){b.Append("null");return;}if(v is string){Quoted(b,(string)v);return;}if(v is bool){b.Append((bool)v?"true":"false");return;}
            var d=v as IDictionary<string,object>;
            if(d!=null){b.Append('{');bool first=true;foreach(var kv in d){if(!first)b.Append(',');b.Append('\n').Append(' ',(depth+1)*2);Quoted(b,kv.Key);b.Append(": ");WriteValue(b,kv.Value,depth+1);first=false;}if(!first)b.Append('\n').Append(' ',depth*2);b.Append('}');return;}
            var seq=v as IEnumerable;
            if(seq!=null){b.Append('[');bool first=true;foreach(var item in seq){if(!first)b.Append(',');WriteValue(b,item,depth+1);first=false;}b.Append(']');return;}
            if(v is double||v is float||v is decimal||v is int||v is long||v is uint||v is short)
            {double n=Convert.ToDouble(v,CI);if(double.IsInfinity(n)||double.IsNaN(n))throw new InvalidDataException("Non-finite JSON.");b.Append(v is double?n.ToString("R",CI):Convert.ToString(v,CI));return;}
            throw new InvalidDataException("Unsupported JSON output type: "+v.GetType().Name);
        }
        private sealed class Reader
        {
            private readonly string s;private int p;
            public Reader(string text){s=text;p=text.Length>0&&text[0]=='\uFEFF'?1:0;}
            private Exception Error(string m){return new InvalidDataException(m+" at JSON character "+p+".");}
            private void Space(){while(p<s.Length&&(s[p]==' '||s[p]=='\t'||s[p]=='\r'||s[p]=='\n'))p++;}
            private bool Eat(char c){Space();if(p<s.Length&&s[p]==c){p++;return true;}return false;}
            public object Read(){var v=Value(0);Space();if(p!=s.Length)throw Error("Trailing content");return v;}
            private object Value(int depth)
            {
                if(depth>64)throw Error("Nesting too deep");Space();if(p>=s.Length)throw Error("Unexpected end");char c=s[p];
                if(c=='"')return String();
                if(c=='{'){p++;var d=new Dictionary<string,object>(StringComparer.Ordinal);if(Eat('}'))return d;do{Space();if(p>=s.Length||s[p]!='"')throw Error("Expected object key");string k=String();if(!Eat(':'))throw Error("Expected colon");if(d.ContainsKey(k))throw Error("Duplicate key "+k);d.Add(k,Value(depth+1));if(Eat('}'))return d;}while(Eat(','));throw Error("Expected comma or brace");}
                if(c=='['){p++;var a=new List<object>();if(Eat(']'))return a;do{a.Add(Value(depth+1));if(a.Count>500000)throw Error("Array too large");if(Eat(']'))return a;}while(Eat(','));throw Error("Expected comma or bracket");}
                if(c=='t'){Literal("true");return true;}if(c=='f'){Literal("false");return false;}if(c=='n'){Literal("null");return null;}
                int start=p;if(s[p]=='-')p++;if(p>=s.Length)throw Error("Invalid number");
                if(s[p]=='0')p++;else{if(s[p]<'1'||s[p]>'9')throw Error("Expected number");while(p<s.Length&&char.IsDigit(s[p]))p++;}
                if(p<s.Length&&s[p]=='.'){p++;int q=p;while(p<s.Length&&char.IsDigit(s[p]))p++;if(p==q)throw Error("Invalid fraction");}
                if(p<s.Length&&(s[p]=='e'||s[p]=='E')){p++;if(p<s.Length&&(s[p]=='+'||s[p]=='-'))p++;int q=p;while(p<s.Length&&char.IsDigit(s[p]))p++;if(p==q)throw Error("Invalid exponent");}
                double n;if(!double.TryParse(s.Substring(start,p-start),NumberStyles.Float,CI,out n)||double.IsNaN(n)||double.IsInfinity(n))throw Error("Non-finite number");return n;
            }
            private void Literal(string x){if(p+x.Length>s.Length||s.Substring(p,x.Length)!=x)throw Error("Invalid literal");p+=x.Length;}
            private string String()
            {
                p++;var b=new StringBuilder();while(p<s.Length){char c=s[p++];if(c=='"')return b.ToString();if(c<32)throw Error("Unescaped control");if(c!='\\'){b.Append(c);continue;}if(p>=s.Length)throw Error("Incomplete escape");c=s[p++];switch(c)
                {case '"':case '\\':case '/':b.Append(c);break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'u':if(p+4>s.Length)throw Error("Incomplete unicode");int n;if(!int.TryParse(s.Substring(p,4),NumberStyles.HexNumber,CI,out n))throw Error("Invalid unicode");b.Append((char)n);p+=4;break;default:throw Error("Invalid escape");}}
                throw Error("Unterminated string");
            }
        }
    }
}
