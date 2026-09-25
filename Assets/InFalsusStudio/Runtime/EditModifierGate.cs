using System;
using UnityEngine;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif
namespace InFalsusStudio
{
    internal sealed class EditModifierGate
    {
        public bool Held {get;private set;}
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        private bool unavailable;
#endif
        public void Reset(){Held=false;}
        public void Observe(Event e)
        {
            if(e.rawType==EventType.KeyDown&&(e.keyCode==KeyCode.LeftAlt||e.keyCode==KeyCode.RightAlt))Held=true;
            if(e.rawType==EventType.KeyUp&&(e.keyCode==KeyCode.LeftAlt||e.keyCode==KeyCode.RightAlt))Held=false;
            if(!Application.isFocused){Held=false;return;}
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if(!unavailable)try{Held=(GetAsyncKeyState(0x12)&0x8000)!=0;}
            catch(DllNotFoundException){unavailable=true;}catch(EntryPointNotFoundException){unavailable=true;}
#else
            Held=e.alt;
#endif
        }
    }
}
