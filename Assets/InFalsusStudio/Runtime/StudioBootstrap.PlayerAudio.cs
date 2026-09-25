using System;
using System.Collections;
using System.IO;
using UnityEngine;
#if INFALSUS_USE_UNITY_WEBREQUEST_AUDIO
using UnityEngine.Networking;
#endif
namespace InFalsusStudio
{
    public sealed partial class StudioBootstrap
    {
        // Defined per BuildPlayerOptions, only after the build preflight verifies both modules.
        // The normal Editor import remains compilable when optional WebRequest modules are disabled.
        private IEnumerator DecodePlayerAudio(string file,Action<AudioClip,string> complete)
        {
#if INFALSUS_USE_UNITY_WEBREQUEST_AUDIO
            string full="",validation=null;AudioType type=AudioType.UNKNOWN;
            try
            {
                full=Path.GetFullPath(file);var info=new FileInfo(full);
                if(!info.Exists||info.Length<=0||info.Length>512L*1024*1024)throw new IOException("Audio missing/empty or over 512 MiB.");
                string ext=info.Extension.ToLowerInvariant();
                type=ext==".ogg"?AudioType.OGGVORBIS:ext==".mp3"?AudioType.MPEG:AudioType.UNKNOWN;
                if(type==AudioType.UNKNOWN)throw new IOException("Project music must be MP3 or OGG.");
            }
            catch(Exception ex){validation=ex.Message;}
            if(validation!=null){complete(null,validation);yield break;}
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(full).AbsoluteUri,type))
            {
                request.timeout=60;
                var handler=request.downloadHandler as DownloadHandlerAudioClip;
                if(handler!=null){handler.streamAudio=false;handler.compressed=false;}
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){complete(null,request.error);yield break;}
                AudioClip clip=null;string error=null;
                try{clip=DownloadHandlerAudioClip.GetContent(request);if(clip==null||clip.samples<=0)throw new IOException("Decoder returned no samples.");}
                catch(Exception ex){error=ex.Message;}
                complete(clip,error);
            }
#else
            complete(null,"Player audio modules are not enabled. Build using Tools/InFalsus Studio/Build Windows Editor 0.9, not an unchecked old Build button.");
            yield break;
#endif
        }
    }
}
