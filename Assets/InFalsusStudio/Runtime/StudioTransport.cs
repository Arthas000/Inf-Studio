using System;
using UnityEngine;

namespace InFalsusStudio
{
    public sealed class StudioTransport
    {
        private readonly AudioSource source;
        private double baseMs,anchorDsp;
        public bool Playing {get;private set;}
        public int Revision {get;private set;}
        public double PlaybackStartMs {get{return baseMs;}}
        public double DspTimeAtChart(double chartMs){return anchorDsp+(chartMs-baseMs)/(1000*Rate);}
        public double ChartTimeAtDsp(double dsp){return baseMs+(dsp-anchorDsp)*1000*Rate;}
        public double Rate {get;private set;}
        public double AudioOffsetMs {get;private set;}
        public float Volume {get{return source.volume;}set{source.volume=Mathf.Clamp01(value);}}
        public bool MusicVirtual {get{return source.isVirtual;}}
        public bool MusicPlaying {get{return source.isPlaying;}}
        public int MusicPriority {get{return source.priority;}}
        public AudioClip Clip {get{return source.clip;}}
        public double TimeMs {get{return Playing?baseMs+Math.Max(0,AudioSettings.dspTime-anchorDsp)*1000*Rate:baseMs;}}
        public StudioTransport(AudioSource audioSource){source=audioSource;source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.priority=InFalsusStudio.Core.AudioVoiceBudget.MusicPriority;Rate=1;}
        public void SetClip(AudioClip clip){bool run=Playing;Pause();source.clip=clip;if(run)Play();}
        public void SetRate(double rate){bool run=Playing;Pause();Rate=Math.Max(.25,Math.Min(2,rate));if(run)Play();}
        public void SetAudioOffset(double ms){bool run=Playing;Pause();AudioOffsetMs=ms;if(run)Play();}
        public void Play()
        {
            if(Playing)return;source.priority=InFalsusStudio.Core.AudioVoiceBudget.MusicPriority;Revision++;source.Stop();anchorDsp=AudioSettings.dspTime+.08;Playing=true;
            if(source.clip==null)return;
            double audioMs=baseMs+AudioOffsetMs;if(audioMs>=source.clip.length*1000)return;
            double delay=audioMs<0?-audioMs/(1000*Rate):0;
            source.pitch=(float)Rate;
            source.timeSamples=(int)Math.Min(source.clip.samples-1,Math.Max(0,audioMs*.001*source.clip.frequency));
            source.PlayScheduled(anchorDsp+delay);
        }
        public void Pause(){if(Playing)baseMs=TimeMs;Playing=false;Revision++;source.Stop();}
        public void Seek(double ms){bool run=Playing;Pause();baseMs=Math.Max(0,ms);if(run)Play();}
        public void Toggle(){if(Playing)Pause();else Play();}
    }
}
