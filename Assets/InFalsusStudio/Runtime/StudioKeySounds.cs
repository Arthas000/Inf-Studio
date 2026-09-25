using System;
using System.Collections.Generic;
using UnityEngine;
using InFalsusStudio.Core;

namespace InFalsusStudio
{
    /// <summary>
    /// Scheduled audio audition using the SAME DSP origin as the music transport.
    /// Refresh/seek/rate/loop/pause invalidate every pending voice. Holds use independent
    /// union buses; resuming inside a Hold restores its loop phase, not its attack.
    /// </summary>
    public sealed class StudioKeySounds : IDisposable
    {
        private sealed class Voice
        {public AudioSource Source;public double BusyUntil;public bool Loop;}
        private readonly List<Voice> voices=new List<Voice>();
        private readonly Dictionary<HitSoundKind,AudioClip> clips=new Dictionary<HitSoundKind,AudioClip>();
        private readonly Transform root;
        private readonly StudioTransport transport;
        private HitSoundPlan plan;
        private int shotIndex,holdIndex,lastRevision=-1;
        private bool running,wasEnabled;
        private double endLimit=double.PositiveInfinity;
        public bool Enabled=true;
        public bool AutoPlayEnabled=true;
        public int RealVoiceLimit {get;private set;}
        public int ShotBudget {get;private set;}
        public int HoldBudget {get;private set;}
        public bool MusicVirtual {get{return transport.MusicVirtual;}}
        private double lastBudgetCheck;
        public float MasterVolume=.65f,ShotVolume=.85f,HoldVolume=.35f;
        public int LateDropped {get;private set;}
        public int CapacityDropped {get;private set;}
        public string MissingClips {get;private set;}
        public const int MaxVoices=24;
        private const double LookAhead=.20;
        public StudioKeySounds(Transform parent,StudioTransport clock)
        {
            transport=clock;UpdateBudget();
            var go=new GameObject("Key sounds - independent Floor and Sky hold beds");go.transform.SetParent(parent,false);root=go.transform;
            Load(HitSoundKind.FloorHit,"FloorHit");Load(HitSoundKind.SideHit,"SideHit");Load(HitSoundKind.SkyHit,"SkyHit");
            Load(HitSoundKind.Flick,"Flick");Load(HitSoundKind.FloorHold,"FloorHold");Load(HitSoundKind.SkyHold,"SkyHold");
        }
        private void UpdateBudget()
        {
            RealVoiceLimit=Math.Max(1,AudioSettings.GetConfiguration().numRealVoices);
            ShotBudget=AudioVoiceBudget.Shots(RealVoiceLimit);HoldBudget=AudioVoiceBudget.Holds(RealVoiceLimit);
        }
        private void Load(HitSoundKind kind,string name)
        {
            var clip=Resources.Load<AudioClip>("InFalsusStudio/KeySounds/"+name);
            if(clip==null){MissingClips=(MissingClips??"")+name+" ";return;}
            clip.LoadAudioData();clips[kind]=clip;
        }
        public void Rebuild(HitSoundPlan value)
        {
            StopAll();plan=value;lastRevision=transport.Revision;
            // When editing changes a running chart, do not re-fire all earlier attacks.
            running=false;
        }
        public void StopAll()
        {foreach(var voice in voices){if(voice.Source!=null)voice.Source.Stop();voice.BusyUntil=0;}running=false;}
        public void Tick(double exclusiveEndMs=double.PositiveInfinity)
        {
            double dsp=AudioSettings.dspTime;
            if(dsp-lastBudgetCheck>1){UpdateBudget();lastBudgetCheck=dsp;}
            if(!Enabled||!AutoPlayEnabled||!transport.Playing||plan==null)
            {if(running)StopAll();wasEnabled=Enabled;lastRevision=transport.Revision;return;}
            if(!running||!wasEnabled||lastRevision!=transport.Revision||endLimit!=exclusiveEndMs)
            {
                bool newRun=lastRevision!=transport.Revision;
                StopAll();lastRevision=transport.Revision;wasEnabled=true;running=true;endLimit=exclusiveEndMs;
                double start=newRun?transport.PlaybackStartMs:transport.TimeMs;
                // A rebuild while already playing should begin at NOW, not at the old
                // transport anchor. Avoid replaying the entire passage after a UI edit.
                start=Math.Max(start,transport.TimeMs-40);
                shotIndex=HitSoundPlan.LowerBound(plan.Shots,start-1e-7);
                holdIndex=HitSoundPlan.LowerBound(plan.Holds,start-1e-7);
                foreach(var hold in plan.Holds)
                    if(hold.Start<start-1e-7&&hold.End>start)Schedule(hold,start,dsp);
            }
            double horizon=Math.Min(endLimit,transport.ChartTimeAtDsp(dsp+LookAhead));
            while(shotIndex<plan.Shots.Count && plan.Shots[shotIndex].Start<=horizon && plan.Shots[shotIndex].Start<endLimit)
            {
                var cue=plan.Shots[shotIndex++];Schedule(cue,cue.Start,dsp);
                // Same sample at exactly the same time shares a voice. This changes only
                // audition loudness/polyphony, NEVER note/combo/score semantics.
                while(shotIndex<plan.Shots.Count&&plan.Shots[shotIndex].Kind==cue.Kind&&Math.Abs(plan.Shots[shotIndex].Start-cue.Start)<1e-7)shotIndex++;
            }
            while(holdIndex<plan.Holds.Count && plan.Holds[holdIndex].Start<=horizon && plan.Holds[holdIndex].Start<endLimit)
            {var cue=plan.Holds[holdIndex++];Schedule(cue,cue.Start,dsp);}
            foreach(var voice in voices)
                if(voice.Source!=null)voice.Source.volume=Mathf.Clamp01(MasterVolume*(voice.Loop?HoldVolume:ShotVolume));
        }
        private void Schedule(HitSoundCue cue,double chartStart,double nowDsp)
        {
            if(chartStart>=endLimit)return;
            AudioClip clip;if(!clips.TryGetValue(cue.Kind,out clip)||clip==null||clip.samples<=0)return;
            double desired=transport.DspTimeAtChart(chartStart),start=Math.Max(nowDsp+.004,desired);
            if(!cue.Loop&&desired<nowDsp-.12){LateDropped++;return;}
            double end=cue.Loop?transport.DspTimeAtChart(cue.End):KeySoundClock.OneShotEnd(start,clip.length);
            if(!double.IsPositiveInfinity(endLimit))end=Math.Min(end,transport.DspTimeAtChart(endLimit));
            if(end<=start)return;
            Voice voice=null;
            foreach(var item in voices)if(item.Loop==cue.Loop&&item.BusyUntil<=nowDsp){voice=item;break;}
            if(voice==null)
            {
                int count=0;foreach(var item in voices)if(item.Loop==cue.Loop)count++;
                if(voices.Count>=MaxVoices||count>=(cue.Loop?HoldBudget:ShotBudget)){CapacityDropped++;return;}
                var go=new GameObject("Key sound voice "+voices.Count);go.transform.SetParent(root,false);
                var src=go.AddComponent<AudioSource>();src.playOnAwake=false;src.spatialBlend=0;src.dopplerLevel=0;src.priority=cue.Loop?AudioVoiceBudget.HoldPriority:AudioVoiceBudget.ShotPriority;
                voice=new Voice{Source=src,Loop=cue.Loop};voices.Add(voice);
            }
            AudioSource a=voice.Source;a.Stop();a.clip=clip;a.loop=cue.Loop;a.pitch=KeySoundClock.Pitch;
            a.volume=Mathf.Clamp01(MasterVolume*(cue.Loop?HoldVolume:ShotVolume));voice.Loop=cue.Loop;
            if(cue.Loop)
            {
                double audibleChart=transport.ChartTimeAtDsp(start);
                a.timeSamples=KeySoundClock.LoopSample(audibleChart-cue.Start,transport.Rate,clip.frequency,clip.samples);
            }
            else a.timeSamples=0;
            a.PlayScheduled(start);
            if(cue.Loop||!double.IsPositiveInfinity(endLimit))a.SetScheduledEndTime(end);
            voice.BusyUntil=end+.005;
        }
        public void Dispose()
        {StopAll();if(root!=null)UnityEngine.Object.Destroy(root.gameObject);voices.Clear();}
    }
}
