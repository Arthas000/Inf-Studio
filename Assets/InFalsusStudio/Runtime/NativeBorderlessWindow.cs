using System;
using System.Runtime.InteropServices;
using System.Text;

namespace InFalsusStudio
{
    /// <summary>Only subclasses THIS PROCESS's visible Unity standalone window.
    /// Restores the prior WndProc/style. No Unity API is called from the native callback.
    /// Windows x64 Mono is the supported/test target; never runs inside Unity Editor.</summary>
    internal sealed class NativeBorderlessWindow : IDisposable
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IntPtr hwnd,previousProc,previousStyle,callbackPointer;
        private WndProc callback;
        private int border=8;
        private bool hookInstalled;
        private const int GWL_STYLE=-16,GWLP_WNDPROC=-4;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate IntPtr WndProc(IntPtr h,uint msg,IntPtr wp,IntPtr lp);
        private delegate bool EnumProc(IntPtr h,IntPtr p);
        [StructLayout(LayoutKind.Sequential)]private struct RECT{public int L,T,R,B;}
        [StructLayout(LayoutKind.Sequential)]private struct POINT{public int X,Y;}
        [StructLayout(LayoutKind.Sequential)]private struct MINMAXINFO{public POINT Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
        [DllImport("user32.dll")]private static extern bool EnumWindows(EnumProc cb,IntPtr p);
        [DllImport("user32.dll")]private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")]private static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("kernel32.dll")]private static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll")]private static extern void SetLastError(uint error);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassNameW(IntPtr h,StringBuilder name,int size);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]private static extern IntPtr Get64(IntPtr h,int index);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW",SetLastError=true)]private static extern int Get32(IntPtr h,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]private static extern IntPtr Set64(IntPtr h,int index,IntPtr value);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)]private static extern int Set32(IntPtr h,int index,int value);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CallWindowProcW(IntPtr proc,IntPtr h,uint msg,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr DefWindowProcW(IntPtr h,uint msg,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll")]private static extern bool GetWindowRect(IntPtr h,out RECT rect);
        [DllImport("user32.dll",SetLastError=true)]private static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hh,uint flags);
        [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr h);
        private static IntPtr Get(IntPtr h,int i){return IntPtr.Size==8?Get64(h,i):new IntPtr(Get32(h,i));}
        private static IntPtr Set(IntPtr h,int i,IntPtr v)
        {
            SetLastError(0);IntPtr old=IntPtr.Size==8?Set64(h,i,v):new IntPtr(Set32(h,i,v.ToInt32()));
            int error=Marshal.GetLastWin32Error();if(old==IntPtr.Zero&&error!=0)throw new System.ComponentModel.Win32Exception(error);
            return old;
        }
        public void Enable(int width,int height)
        {
            Restore();uint own=GetCurrentProcessId();IntPtr found=IntPtr.Zero;
            EnumProc probe=(h,p)=>{uint pid;GetWindowThreadProcessId(h,out pid);if(pid!=own||!IsWindowVisible(h))return true;
                var cls=new StringBuilder(128);GetClassNameW(h,cls,128);if(cls.ToString()!="UnityWndClass")return true;found=h;return false;};
            EnumWindows(probe,IntPtr.Zero);GC.KeepAlive(probe);
            if(found==IntPtr.Zero)throw new InvalidOperationException("No visible standalone Unity window in this process.");
            hwnd=found;previousStyle=Get(hwnd,GWL_STYLE);previousProc=Get(hwnd,GWLP_WNDPROC);
            if(previousProc==IntPtr.Zero){hwnd=IntPtr.Zero;throw new InvalidOperationException("Missing previous window procedure.");}
            try{border=Math.Max(6,(int)Math.Round(8.0*GetDpiForWindow(hwnd)/96.0));}catch(EntryPointNotFoundException){border=8;}
            callback=WindowProcedure;callbackPointer=Marshal.GetFunctionPointerForDelegate(callback);
            try
            {
                Set(hwnd,GWLP_WNDPROC,callbackPointer);hookInstalled=true;
                // Keep WS_THICKFRAME/SYSMENU/min/max capabilities; remove visible caption.
                long style=(previousStyle.ToInt64()&~0x00C00000L)|0x00040000L|0x00080000L|0x00030000L;
                Set(hwnd,GWL_STYLE,new IntPtr(style));
                if(!SetWindowPos(hwnd,IntPtr.Zero,0,0,width,height,0x0020u|0x0004u|0x0002u|0x0010u))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            catch{Restore();throw;}
        }
        private IntPtr WindowProcedure(IntPtr h,uint msg,IntPtr wp,IntPtr lp)
        {
            try
            {
                if(msg==0x0083u)return IntPtr.Zero; // WM_NCCALCSIZE: entire client, no frame painting.
                if(msg==0x0084u) // WM_NCHITTEST: signed monitor coordinates, not low-word unsigned.
                {
                    RECT r;if(GetWindowRect(h,out r))
                    {
                        long bits=lp.ToInt64();int x=unchecked((short)(bits&65535)),y=unchecked((short)((bits>>16)&65535));
                        bool left=x<r.L+border,right=x>=r.R-border,top=y<r.T+border,bottom=y>=r.B-border;
                        int hit=top?(left?13:right?14:12):bottom?(left?16:right?17:15):left?10:right?11:1;
                        return new IntPtr(hit);
                    }
                }
                if(msg==0x0024u&&lp!=IntPtr.Zero) // min client workspace on resize
                {IntPtr result=previousProc!=IntPtr.Zero?CallWindowProcW(previousProc,h,msg,wp,lp):DefWindowProcW(h,msg,wp,lp);var mm=(MINMAXINFO)Marshal.PtrToStructure(lp,typeof(MINMAXINFO));mm.MinTrackSize=new POINT{X=960,Y=540};Marshal.StructureToPtr(mm,lp,false);return result;}
            }
            catch { /* Never allow a managed exception to cross the native WndProc boundary. */ }
            return previousProc!=IntPtr.Zero?CallWindowProcW(previousProc,h,msg,wp,lp):DefWindowProcW(h,msg,wp,lp);
        }
        public void Restore()
        {
            if(hwnd==IntPtr.Zero)return;
            if(IsWindow(hwnd))
            {
                // Do not overwrite another component's later subclass. Keep delegate alive if chained.
                if(hookInstalled&&Get(hwnd,GWLP_WNDPROC)!=callbackPointer)
                    throw new InvalidOperationException("Another component changed WndProc; cannot safely remove borderless hook.");
                if(hookInstalled&&previousProc!=IntPtr.Zero)Set(hwnd,GWLP_WNDPROC,previousProc);
                Set(hwnd,GWL_STYLE,previousStyle);SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x0020u|0x0004u|0x0002u|0x0001u|0x0010u);
            }
            hwnd=IntPtr.Zero;previousProc=IntPtr.Zero;callbackPointer=IntPtr.Zero;callback=null;hookInstalled=false;
        }
        public void Dispose(){try{Restore();}catch{if(callback!=null)RetainedCallbacks.Add(callback);}}
        private static readonly System.Collections.Generic.List<WndProc> RetainedCallbacks=new System.Collections.Generic.List<WndProc>();
#else
        public void Enable(int width,int height){throw new PlatformNotSupportedException("Borderless resizable window requires the Windows standalone build.");}
        public void Restore(){}
        public void Dispose(){}
#endif
    }
}
