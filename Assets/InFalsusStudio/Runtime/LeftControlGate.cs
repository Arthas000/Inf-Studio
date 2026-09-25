using System;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif

namespace InFalsusStudio
{
    // Event.control merges both Ctrl keys; that is NOT sufficient for a left-only gate.
    // Poll only the left key on Windows (the requested target), without depending on InputSystem.
    internal sealed class LeftControlGate
    {
        public bool Held {get;private set;}
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private bool nativeUnavailable;
#endif
        public void Reset(){Held=false;}
        public void Observe(Event e)
        {
            if(e.rawType==EventType.KeyDown&&e.keyCode==KeyCode.LeftControl)Held=true;
            if(e.rawType==EventType.KeyUp&&e.keyCode==KeyCode.LeftControl)Held=false;
            if(!Application.isFocused){Held=false;return;}
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if(!nativeUnavailable)
            {
                try { Held=(GetAsyncKeyState(0xA2)&0x8000)!=0; }
                catch(DllNotFoundException){nativeUnavailable=true;}
                catch(EntryPointNotFoundException){nativeUnavailable=true;}
            }
#else
            // Non-Windows fallback uses explicit key events. Never reinterpret right Ctrl as left.
            if(!e.control&&e.rawType!=EventType.KeyDown)Held=false;
#endif
        }
    }
}
