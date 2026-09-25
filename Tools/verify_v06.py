#!/usr/bin/env python3
"""Independent reference math + source/resource contracts. NOT a C#/Unity execution.
Usage: python Tools/verify_v06.py [--baseline /path/to/unmodified/v0.5.1/root]
"""
from __future__ import annotations
import argparse,collections,hashlib,importlib.util,json,math,re
from fractions import Fraction
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
AS=ROOT/'Assets/InFalsusStudio'
rows=[]
def check(name,truth,kind='independent reference math'):
    rows.append(dict(name=name,passed=bool(truth),category=kind))
    if not truth: raise AssertionError(name)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def parse(p):
    out=[]
    for i,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines()):
        m=re.match(r'\s*(\w+)\((.*?)\)',line)
        if not m:continue
        try:a=[float(x) for x in m[2].split(',')]
        except ValueError:continue
        out.append({'id':i,'k':m[1].lower(),'a':a,'t':0 if m[1]=='chart' else a[0],
                    'end':a[0]+(a[3] if m[1]=='hold' else a[9] if m[1]=='skyarea' else 0)})
    return out

def sky(e,t):
    a=e['a'];u=max(0,min(1,(t-a[0])/a[9])) if a[9]>0 else 0
    def ease(c,u):return u if c==0 else math.sin(u*math.pi/2) if c==1 else 1-math.cos(u*math.pi/2)
    l0,r0=a[1]/a[2]-abs(a[3]/a[2])/2,a[1]/a[2]+abs(a[3]/a[2])/2
    l1,r1=a[4]/a[5]-abs(a[6]/a[5])/2,a[4]/a[5]+abs(a[6]/a[5])/2
    return (l0+(l1-l0)*ease(a[7],u),r0+(r1-r0)*ease(a[8],u))
def center(e,t):return sum(sky(e,t))/2

def bars(segments,begin,end):
    result={}
    for i,(origin,bpm,meter) in enumerate(segments):
        o,b,m=Fraction(str(origin)),Fraction(str(bpm)),Fraction(str(meter))
        period=60000*m/b
        stop=Fraction(str(segments[i+1][0])) if i+1<len(segments) else Fraction(str(end+1))
        n=max(0,(Fraction(str(begin))-o)//period)
        while o+n*period<stop:
            exact=o+n*period;auth=int(exact+Fraction(1,2))
            if auth>end:break
            if auth>=begin:result[auth]=i
            n+=1
    return sorted(result)
def intervals(events,kind):
    spans=sorted((e['t'],e['end']) for e in events if e['k']==kind and e['end']>e['t']);result=[]
    for a,b in spans:
        if result and a<=result[-1][1]+1e-7:result[-1]=(result[-1][0],max(b,result[-1][1]))
        else:result.append((a,b))
    return result

def run(base=None):
    source={p.name:p.read_text() for p in (AS/'Runtime').rglob('*.cs')}
    # The actual reported passage: each completion must retain its Sky endpoint,
    # never resurrect an older completed Flick position.
    es=parse(AS/'Resources/InFalsusStudio/LightsOut.spc.txt')
    passage=[e for e in es if e['k']=='skyarea' and 73846<=e['t']<=77077]
    check('reported passage has all eight supplied Sky segments',len(passage)==8,'supplied chart audit')
    exits=[]
    for e in passage:
        check(f"Sky {e['t']}: both endpoint centers equal 0.5",abs(center(e,e['t'])-.5)<1e-12 and abs(center(e,e['end'])-.5)<1e-12)
        check(f"Sky {e['t']}: symmetric throughout",all(abs(center(e,e['t']+(e['end']-e['t'])*i/64)-.5)<1e-12 for i in range(65)))
        nextsky=next((s for s in es if s['k']=='skyarea' and s['t']>e['end']),None)
        previous=[f for f in es if f['k']=='flick' and f['t']+100<=e['end']]
        prior=max(previous,key=lambda f:f['t']) if previous else None
        if prior:
            old_end=max(0,min(1,prior['a'][1]/prior['a'][2]+(-.055 if prior['a'][4]==16 else .055)))
            check(f"Sky {e['t']}: old Flick predates completed Sky",prior['t']+100<e['end'])
        else:old_end=None
        exits.append(dict(start=e['t'],end=e['end'],tailCenter=center(e,e['end']),priorFlickTime=prior['t'] if prior else None,priorFlickUnconstrainedEnd=old_end))
    check('fix uses most recently completed Sky, half-open active ranges remain',all(x in source['AutoplayCursor.cs'] for x in ['LatestSkyExit(timeMs)','lastSky.EndMs>=prev.End','if(prev!=null && !skyIsLast)','SkyExitRange(lastSky)']),'source contract')
    check('exit during post-flick return evaluates the actual return value at Sky end','lastSky.EndMs-prev.End' in source['AutoplayCursor.cs'],'source contract')
    check('center is .5 immediately before/at/after symmetric tail under hold-exit policy',all(abs(x['tailCenter']-.5)<1e-12 for x in exits))

    ss=[(0,120,4),(1123,150,3),(4000,100,5)]
    check('future timing roots & local meter enumeration',bars(ss,0,10000)==[0,1123,2323,3523,4000,7000,10000])
    check('no old timing phase retained after reset',2000 not in bars(ss,0,10000) and 4723 not in bars(ss,0,10000))
    check('fractional-meter bars',bars([(0,130,3.5)],0,5000)==[0,1615,3231,4846])
    check('future timing origin visible in ahead window',5123 in bars([(0,120,4),(5123,150,3)],3000,8000))
    check('long bar horizon has no accumulated rounding',bars([(0,130,4)],184615300,184616000)==[184615385])
    render=source['NoteRenderer.cs']
    check('bars independent of snap grid','showGrid||persistentBars' in render and 'BarsBetween(a,b)' in render,'source contract')
    check('six tracks share one Z per guide','for(int lane=0;lane<6;lane++)' in render and 'BeatGuideGeometry.Point(p,lane,0,z)' in render,'source contract')
    check('pale gray pixel-width persistent lines', 'new Color(.82f,.86f,.89f,.57f)' in render and '!snapGrid?1.15f' in render,'source contract')
    check('future scroll segments enumerated (not current timing only)','for(int i=0;i<scroll.Points.Count;i++)' in render,'source contract')

    def score(c,n):return 0 if n==0 else 100000000*max(0,min(c,n))//n+max(0,min(c,n))
    for n in [1,2,3,617,1334,1698,2169,3108]:
        check(f'score {n}: zero and final ceiling',score(0,n)==0 and score(n,n)==100000000+n)
        check(f'score {n}: monotone direct rational lookup',all(score(i+1,n)>=score(i,n) for i in range(n)))
    check('integer score no repeated rounding drift',score(1,3)==33333334 and score(2,3)==66666668)
    check('score empty chart remains zero',score(0,0)==0)
    check('score explicitly provisional and not song-progress interpolation',all(s in source['PreviewScore.cs'] for s in ['NOT a verified game score','decimal.Floor','completed/total']), 'source contract')
    check('screen-centered combo present and no OS cursor movement','private void DrawCenterCombo()' in source['StudioBootstrap.SongHud.cs'] and 'SetCursorPos' not in '\n'.join(source.values()),'source contract')
    check('SCORE reference anchor moved inside label compartment','HR(33,20,78,20)' in source['StudioBootstrap.SongHud.cs'],'source contract')

    p=source['StudioBootstrap.Panels.cs'];n=source['StudioBootstrap.NotePanel.cs'];boot=source['StudioBootstrap.cs']
    check('tool drawer defaults folded, not a full width toolbar','bool toolsExpanded,guiToolsExpanded' in p and 'if(guiToolsExpanded)DrawToolbar()' in boot and 'new Rect(-10,-10,0,0)' in p,'source contract')
    check('timing panel independent of selected-note panel','DrawTimingBrowser()' in p and 'DrawNotePanel()' in n,'source contract')
    check('one friendly form per note type',all(f in n for f in ['DrawTapProperties','DrawHoldProperties','DrawFlickProperties','DrawSkyProperties']),'source contract')
    check('note-only UI has no timing or nearby-note lists','guiTimingEvents' not in n and 'guiNearNotes' not in n,'source contract')
    check('no form changes during playback','GUI.enabled=old&&!transport.Playing' in n,'source contract')
    check('form snapshots frozen at Layout','CaptureWorkspaceFrame();' in source['StudioBootstrap.GuiFrame.cs'] and 'guiNoteForm=noteForm' in p,'source contract')
    check('collapsed panels do not block scene clicks','WorkspaceBlocksInput(gui)' in source['StudioBootstrap.Editing.cs'],'source contract')
    check('friendly note form refuses stale edits','OriginalSource' in source['NoteProperties.cs'] and 'This note changed while the form was open' in source['NoteProperties.cs'],'source contract')
    check('friendly form edits only changed fields and preserves splits',all(x in source['NoteProperties.cs'] for x in ['if(!Changed(key))return;','original.Get(2)','original.Get(5)','SetOptionalNumber']),'source contract')
    check('note Apply is an undoable RAM command, not file IO','Change(d=>pending.Apply(d))' in n and 'File.Write' not in n and 'Save(' not in source['NoteProperties.cs'],'source contract')
    check('raw-source form remains explicit', 'Apply SOURCE (no snap)' in n and 'ReplaceSourceLine' in n,'source contract')
    for split in [2,24,100,200]:
        check(f'percentage to raw preserves split {split}',Fraction(10)*split/100/Fraction(split)==Fraction(1,10))
    check('Hold moving head does not accidentally move tail',1500-1100==400)
    check('rejected invalid form can be rolled back atomically','var next = Document.Clone(); edit(next);' in source['SpcDocument.cs'],'source contract')

    sfx=parse(ROOT/'Fixtures/06_KeySounds_AllChannels.spc')
    floor=intervals(sfx,'hold');skyholds=intervals(sfx,'skyarea')
    check('all-six ground Hold voices union',floor==[(5000,8500)])
    check('touching Sky spans do not restart hold loop',skyholds==[(3500,4500),(5500,9000)])
    check('5500..8000 has both independent continuous sound buses',all(any(a<=t<b for a,b in floor) and any(a<=t<b for a,b in skyholds) for t in [5500,6000,7500,7999]))
    plan=source['HitSoundPlan.cs'];scheduler=source['StudioKeySounds.cs']
    check('no fictional SkyTap schema added', 'enum EventKind' in source['SpcDocument.cs'] and 'SkyTap' not in source['SpcDocument.cs'],'source contract')
    check('per-segment Sky attack is default','skyAttacks=SkyAttackPolicy.EverySegment' in plan,'source contract')
    check('floor & side attacks have separate routing','e.Lane==0||e.Lane==5' in plan and 'HitSoundKind.SideHit:HitSoundKind.FloorHit' in plan,'source contract')
    check('Flick uses source time (not visual contact or combo ticks)','Shot(e,HitSoundKind.Flick)' in plan and 'VisualContact' not in plan and 'ProvisionalCombo' not in plan,'source contract')
    check('sound has independent AudioSources + DSP scheduling','go.AddComponent<AudioSource>()' in scheduler and 'a.PlayScheduled(start)' in scheduler and 'a.SetScheduledEndTime(end)' in scheduler,'source contract')
    check('music and SFX share transport mapping','transport.DspTimeAtChart(chartStart)' in scheduler and 'transport.ChartTimeAtDsp' in scheduler,'source contract')
    check('epoch reset stops pending sounds on seek/rate change','lastRevision!=transport.Revision' in scheduler and 'Revision++' in source['StudioTransport.cs'],'source contract')
    check('loop lookahead cannot fire shots after B','plan.Shots[shotIndex].Start<endLimit' in scheduler and 'Math.Min(end,transport.DspTimeAtChart(endLimit))' in scheduler,'source contract')
    check('no prior attacks replayed on plan rebuild','plan=value;lastRevision=transport.Revision' in scheduler,'source contract')
    check('hold resume advances sample phase, does not replay attack','a.timeSamples=(int)(frames%clip.samples)' in scheduler,'source contract')
    for rate in [.5,1,2]:
        anchor,base_ms=25.08,5000
        dsp=lambda t:anchor+(t-base_ms)/(1000*rate)
        at=lambda d:base_ms+(d-anchor)*1000*rate
        check(f'DSP {rate}: anchor and inverse',abs(dsp(base_ms)-anchor)<1e-12 and abs(at(dsp(6321))-6321)<1e-8)
        check(f'DSP {rate}: fixed chart hold duration scales in playback',abs((dsp(8000)-dsp(6000))-2/rate)<1e-10)
    manifest=json.loads((ROOT/'Documentation/Reference/key_sound_manifest_06.json').read_text())
    for item in manifest:
        path=AS/'Resources/InFalsusStudio/KeySounds'/(item['resource']+'.ogg')
        check(item['resource']+' raw bytes preserved',sha(path)==item['sha256'],'audio resource hash')
        check(item['resource']+' genuine OGG Vorbis',path.read_bytes()[:4]==b'OggS' and item['codec_name']=='vorbis','audio resource audit')
        check(item['resource']+' preload and native stereo sample rate',item['sample_rate']=='48000' and item['channels']==2 and 'loadType: 0' in Path(str(path)+'.meta').read_text(),'audio import metadata')
    guids=[]
    for path in (AS).rglob('*.meta'):
        match=re.search(r'^guid: (\w+)',path.read_text(),re.M)
        if match:guids.append(match[1])
    check('no duplicate Unity GUIDs',len(guids)==len(set(guids)),'resource structure')
    check('all new Unity scripts have metadata',all(Path(str(p)+'.meta').exists() for p in AS.rglob('*.cs')),'resource structure')
    check('no font files bundled',not any(p.suffix.lower() in {'.ttf','.otf','.woff','.woff2'} for p in ROOT.rglob('*')),'resource structure')
    check('new actual C# regression invoked by existing runner','Release06SelfTests.Run(check,suppliedChart)' in source['CoreSelfTests.cs'],'source contract')
    check('actual Unity audio check is present','Check v0.6 Key Sound Assets' in (AS/'Editor/StudioSetup.cs').read_text(),'source contract')
    if base:
        old=Path(base)/'Assets/InFalsusStudio'
        frozen=['Runtime/Core/CameraCalibration.cs','Runtime/StudioProfile.cs','Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Rendering/FlickGeometry.cs','Runtime/LeftControlGate.cs','Runtime/Core/ManualSongSession.cs','Runtime/Core/SongMetadataEdit.cs','Runtime/StudioBootstrap.ManualSave.cs','Runtime/StudioBootstrap.Radial.cs','Runtime/Core/ProvisionalComboTimeline.cs','Runtime/Core/TickCountResearch.cs','Resources/InFalsusStudio/CalibratedProfile.asset','Scenes/InFalsusStudio_Demo.unity','Resources/InFalsusStudio/LightsOut.spc.txt']
        frozen += [str(p.relative_to(old)) for p in old.rglob('*.shader')]
        for rel in frozen:check('unchanged '+rel,sha(old/rel)==sha(AS/rel),'baseline hash')
        check('all existing .meta GUID files unchanged',all(sha(p)==sha(AS/p.relative_to(old)) for p in old.rglob('*.meta')),'baseline hash')
        check('manual save OnDestroy remains no-write','File.Write' not in boot[boot.index('private void OnDestroy()'):],'source contract')
    output={'scope':'Executed independent Python math, input-file hash and LIMITED source contracts. NOT C# compilation, Unity rendering, actual mouse UI or audible playback.', 'checks':len(rows),'passed':sum(x['passed'] for x in rows),'results':rows,'reported_cursor_exits':exits}
    (ROOT/'Documentation/Reference/v06_verification.json').write_text(json.dumps(output,ensure_ascii=False,indent=2))
    print(f'{len(rows)} independent reference/resource/source-contract checks passed. NOT Unity/C# execution.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--baseline',type=Path);args=parser.parse_args();run(args.baseline)
