using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace InFalsusStudio
{
    /// <summary>Local dialogs. Editor uses EditorUtility; Windows Player uses system dialogs.
    /// No editor assemblies, external helper exe, shell commands or runtime downloads in Player.</summary>
    internal static class StudioFileDialogs
    {
        public static string Folder(string title,string directory="")
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFolderPanel(title,directory??"","");
#elif UNITY_STANDALONE_WIN
            return OnSta(()=>NativeFolder(title));
#else
            throw new PlatformNotSupportedException("Native folder selection is implemented for Windows. Use the folder path field on other platforms.");
#endif
        }
        public static string Open(string title,string directory,string extension)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanel(title,directory??"",extension??"");
#elif UNITY_STANDALONE_WIN
            return OnSta(()=>NativeFile(false,title,directory,"",extension));
#else
            throw new PlatformNotSupportedException("Native file selection is available on Windows.");
#endif
        }
        public static string Save(string title,string directory,string name,string extension)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.SaveFilePanel(title,directory??"",name??"",extension??"");
#elif UNITY_STANDALONE_WIN
            return OnSta(()=>NativeFile(true,title,directory,name,extension));
#else
            throw new PlatformNotSupportedException("Native file save selection is available on Windows.");
#endif
        }
        public static bool Confirm(string title,string text,string yes="OK",string no="Cancel")
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.DisplayDialog(title,text,yes,no);
#elif UNITY_STANDALONE_WIN
            return MessageBoxW(GetActiveWindow(),text+"\n\n"+yes+" = Yes / "+no+" = No",title,0x124)==6;
#else
            return false;
#endif
        }
        // 0 Save, 1 Cancel, 2 Discard. Default is CANCEL, never silently save/discard.
        public static int SaveDecision(string text)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorUtility.DisplayDialogComplex("Unsaved changes",text,"Save","Cancel","Discard");
#elif UNITY_STANDALONE_WIN
            int result=MessageBoxW(GetActiveWindow(),text+"\n\n是/Yes = 保存 Save\n否/No = 放弃 Discard\n取消/Cancel = 返回", "未保存 / Unsaved changes",0x223);
            return result==6?0:result==7?2:1;
#else
            return 1;
#endif
        }
        public static void Error(string title,string text)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayDialog(title,text,"OK");
#elif UNITY_STANDALONE_WIN
            MessageBoxW(GetActiveWindow(),text,title,0x10);
#endif
        }
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll")]private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int MessageBoxW(IntPtr hwnd,string text,string title,uint flags);
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileNameW([In,Out] OpenFileName ofn);
        [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSaveFileNameW([In,Out] OpenFileName ofn);
        [DllImport("comdlg32.dll")]private static extern uint CommDlgExtendedError();
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private sealed class OpenFileName
        {
            public int size;public IntPtr owner,instance;public string filter;public IntPtr customFilter;
            public int maxCustomFilter,filterIndex;public StringBuilder file;public int maxFile;
            public IntPtr fileTitle;public int maxFileTitle;public string initialDirectory,title;public uint flags;
            public short fileOffset,fileExtension;public string defaultExtension;public IntPtr userData,hook,templateName,reserved;
            public int reserved2,flagsEx;
        }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private struct BrowseInfo
        {
            public IntPtr owner,root,displayName;[MarshalAs(UnmanagedType.LPWStr)]public string title;
            public uint flags;public IntPtr callback,param;public int image;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr SHBrowseForFolderW(ref BrowseInfo info);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)][return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool SHGetPathFromIDListW(IntPtr pidl,StringBuilder path);
        private static string OnSta(Func<string> work)
        {
            string result="";Exception error=null;
            // Unity engine calls stay on the main thread. Only Win32 dialog work goes on STA.
            var t=new Thread(()=>{try{result=work()??"";}catch(Exception e){error=e;}});
            t.IsBackground=true;t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();
            if(error!=null)throw new IOException("Windows dialog failed.",error);return result;
        }
        private static string NativeFile(bool save,string title,string directory,string name,string extension)
        {
            string ext=(extension??"").TrimStart('.');string pattern=string.IsNullOrEmpty(ext)?"*.*":"*."+ext;
            var f=new OpenFileName{size=Marshal.SizeOf(typeof(OpenFileName)),file=new StringBuilder(32768),maxFile=32768,
                filter="Selected files ("+pattern+")\0"+pattern+"\0All files\0*.*\0\0",filterIndex=1,
                initialDirectory=Directory.Exists(directory)?directory:null,title=title,defaultExtension=ext,
                flags=0x80000u|0x8u|0x800u|(save?0x2u:0x1000u)};
            if(!string.IsNullOrEmpty(name))f.file.Append(name);
            bool ok=save?GetSaveFileNameW(f):GetOpenFileNameW(f);
            if(ok)return f.file.ToString();uint err=CommDlgExtendedError();
            if(err!=0)throw new IOException("File dialog error 0x"+err.ToString("X"));return "";
        }
        private static string NativeFolder(string title)
        {
            IntPtr buffer=Marshal.AllocHGlobal(1024),pidl=IntPtr.Zero;
            try
            {
                var b=new BrowseInfo{title=title,displayName=buffer,flags=0x1u|0x10u|0x40u};
                pidl=SHBrowseForFolderW(ref b);if(pidl==IntPtr.Zero)return "";
                var path=new StringBuilder(32768);
                if(!SHGetPathFromIDListW(pidl,path))throw new IOException("Select a local filesystem directory.");
                return path.ToString();
            }
            finally{if(pidl!=IntPtr.Zero)Marshal.FreeCoTaskMem(pidl);Marshal.FreeHGlobal(buffer);}
        }
#endif
    }
}
