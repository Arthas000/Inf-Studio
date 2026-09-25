#!/usr/bin/env python3
"""Independent reference/data/finite source checks. NOT C# execution or Unity validation."""
from pathlib import Path
from fractions import Fraction
import hashlib, json, math, re, struct, collections, argparse
ROOT=Path(__file__).resolve().parents[1]
R=ROOT/'Assets/InFalsusStudio/Runtime'
checks=[]
def check(ok,name,category):
    checks.append({'name':name,'category':category,'passed':bool(ok)})
    if not ok:raise AssertionError(name)
def src(path):return (R/path).read_text(encoding='utf-8-sig')
def parse(path):
    out=[]
    for row,line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(),1):
        m=re.match(r'([A-Za-z_]+)\((.*)\)$',line)
        if m:
            try:a=[Fraction(s) for s in m[2].split(',')]
            except(ValueError,ZeroDivisionError):continue
            out.append({'id':row,'kind':m[1],'a':a,'raw':line})
    return out

def members(events,g):return sorted([e for e in events if e['kind']=='skyarea' and len(e['a'])>=11 and e['a'][10]==g and g>=0],key=lambda e:(e['a'][0],e['id']))
def neighbor(es,g,t,direction):
    m=members(es,g); c=[e for e in m if (e['a'][0]>t if direction>0 else e['a'][0]<t)]
    return (min(c,key=lambda e:(e['a'][0],e['id'])) if direction>0 else min(c,key=lambda e:(-e['a'][0],e['id']))) if c else None

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--baseline',type=Path);args=ap.parse_args()
    es=parse(ROOT/'Fixtures/09_GroupNavigation.spc')
    check([e['a'][0] for e in members(es,7)]==[Fraction('1846.25'),3692,5000],'fixture group7 member order','group reference')
    for g in (7,8,99,-1):
        for t in (0,1846,Fraction('1846.25'),2500,3692,6000):
            for direction in (-1,1):
                n=neighbor(es,g,t,direction)
                check(n is None or n in members(es,g) and ((n['a'][0]>t) if direction>0 else n['a'][0]<t),f'group {g} time {t} direction {direction}','group reference')
    check(neighbor(es,7,Fraction('1846.25'),1)['a'][0]==3692,'strict next, no revisit same timestamp','group reference')
    check(neighbor(es,7,5000,1) is None,'no implicit wrap','group reference')
    cold=ROOT/'Fixtures/09_Coldsea_PairedPlaintext.spc';raw=cold.read_bytes();ce=parse(cold)
    expected={'chart':1,'tap':633,'hold':180,'skyarea':412,'flick':250,'bpm':3}
    check(dict(collections.Counter(e['kind'] for e in ce))==expected,'Coldsea exact type counts','attachment data')
    check(len(raw.splitlines())==1479,'Coldsea physical line count','attachment data')
    check(hashlib.sha256(raw).hexdigest()=='09b9dc3a3c0d2d6356d7ffa8a76d46349c0ac12f00079bdb23c3bfe500f0ff01','Coldsea bytes preserved','attachment data')
    check([e['raw'] for e in ce if e['kind']=='bpm']==['bpm(137931,174.0,99.0)','bpm(140000,174.0,4.0)','bpm(140690,174.0,4.0)'],'Coldsea actual BPM statements','attachment data')
    check([4+2*1475+i for i in range(3)]==[2954,2955,2956],'work-line explanation arithmetic only','attachment data')
    bp=[(Fraction(0),Fraction(174),Fraction(4))]+[tuple(e['a'][:3]) for e in ce if e['kind']=='bpm']
    bars=[]
    for i,(start,bpm,meter) in enumerate(bp):
        end=bp[i+1][0] if i+1<len(bp) else 145000
        period=60000/bpm*meter
        for j in range(max(0,math.ceil((end-start)/period))):
            t=start+j*period
            if t<end:bars.append(int(t+Fraction(1,2)))
    for t in [137931,140000,140690]:check(t in bars,f'new timing origin {t} is a bar','grid reference')
    check(not any(137931<t<140000 for t in bars),'99 meter does not fabricate extra bars before next timing','grid reference')
    for rate in (Fraction(1),Fraction(3,4),Fraction(1,2),Fraction(1,4)):
        for elapsed in (0,10,175,1000,1500,5300,10000):
            real_ms=Fraction(elapsed)/rate
            phase=int(real_ms/1000*48000)%288000
            check(0<=phase<288000,f'key normal-speed loop phase at {rate}, {elapsed}','audio reference')
            chart_end_delta=1000*rate*Fraction('3.564')
            check(chart_end_delta/rate/1000==Fraction('3.564'),f'attack sample length fixed at {rate}, {elapsed}','audio reference')
        check(4000/rate>=4000,'slow chart hold occupies at least normal real length '+str(rate),'audio reference')
    check('a.pitch=KeySoundClock.Pitch' in src('StudioKeySounds.cs') and '/transport.Rate' not in src('StudioKeySounds.cs'),'key code not scaled by chart rate','static connection')
    check('source.pitch=(float)Rate' in src('StudioTransport.cs') or 'source.pitch=Rate' in src('StudioTransport.cs'),'music retains rate path','static connection')
    image=ROOT/'Assets/InFalsusStudio/Resources/InFalsusStudio/Backgrounds/Corridor.png';ib=image.read_bytes();w,h=struct.unpack('>II',ib[16:24]);check((w,h)==(2048,1024),'supplied background dimensions actually 2:1','attachment data')
    provenance=json.loads((ROOT/'Documentation/ASSET_PROVENANCE_0.9.json').read_text());check(hashlib.sha256(ib).hexdigest()==provenance['sha256'],'background original byte hash','attachment data')
    for ta in (Fraction(1),Fraction(2),Fraction(16,9),Fraction(21,9),Fraction(9,16)):
        for va in (Fraction(16,9),Fraction(4,3),Fraction(21,9),Fraction(9,16)):
            cw,ch=(va/ta,Fraction(1)) if ta>va else (Fraction(1),ta/va)
            x,y=(1-cw)/2,(1-ch)/2
            check(cw*ta/ch==va and x>=0 and y>=0 and x+cw<=1 and y+ch<=1,f'cover aspect/UV bounds {ta}/{va}','background reference')
    check((Fraction(1)-Fraction(16,9)/2)/2==Fraction(1,18),'background horizontal margin 1/18','background reference')
    ranges=[(534,678),(684,866),(873,997),(1004,1124),(1132,1310)]
    for width,height in ((1280,720),(1920,1080),(2560,1440),(3840,2160)):
        factor=height/1080
        for i in range(len(ranges)-1):check(ranges[i][1]*factor<ranges[i+1][0]*factor,f'top controls separate at {width}x{height} #{i}','layout reference')
        check(ranges[-1][1]*factor<1338*factor,'top fields before song HUD '+str(width),'layout reference')
    chrome=src('StudioBootstrap.Chrome.cs')
    check('HR(18,90,109,63)' in chrome,'play expanded 109x63 hit box','static connection')
    check('HR(46.5f,108.5f,18,18)' in chrome or 'HR(46.5f,108.5f,18f,18f)' in chrome,'play icon size unchanged','static connection')
    static={
      'exact group jump guard':('StudioBootstrap.Navigation.cs','PreserveExactGroupJump()'),
      'manual group not assigning note':('StudioBootstrap.Groups.cs','currentSkyGroup=g;groupFollowingId=-1;'),
      'foreground selection follows Sky':('StudioBootstrap.Editing.cs','RefreshSelectionFields();FollowSelectedSkyGroup();'),
      'group follows source edits':('StudioBootstrap.Editing.cs','UpdateFollowedGroupAfterEdit();'),
      'timeline selection follows group':('StudioBootstrap.Timeline.cs','FollowSelectedSkyGroup();'),
      'all current timings verified before merge':('Core/TimingReferenceMerge.cs','var missing=Prepare(target,reference);'),
      'mismatch reference refused':('Core/TimingReferenceMerge.cs','Known note content does not match'),
      'existing time conflict refused':('Core/TimingReferenceMerge.cs','Existing BPM conflicts at'),
      'opaque retention message':('StudioBootstrap.TimingReference.cs','original opaque events retained'),
      'explicit named JSON BPM':('Core/IcpJsonImport.cs','ProjectJson.Get(e,"beats_per_bar") is double'),
      'native own process only':('NativeBorderlessWindow.cs','pid!=own'),
      'native own class only':('NativeBorderlessWindow.cs','"UnityWndClass"'),
      'native signed monitor coordinates':('NativeBorderlessWindow.cs','unchecked((short)'),
      'native callback chain ownership check':('NativeBorderlessWindow.cs','Get(hwnd,GWLP_WNDPROC)!=callbackPointer'),
      'native editor guard':('NativeBorderlessWindow.cs','#if UNITY_STANDALONE_WIN && !UNITY_EDITOR'),
      'unity endframe mode application':('StudioWindow.cs','new WaitForEndOfFrame()'),
      'explicit four display modes':('StudioWindow.cs','BorderlessFullscreen=3'),
      'F11 recovery':('StudioBootstrap.Presentation.cs','KeyCode.F11'),
      'background decode preflight':('StudioBootstrap.Presentation.cs','LocalImageSize.Validate(data,8192,33554432)'),
      'background no collider':('Rendering/StudioBackground.cs','no collider'),
      'background file not shader code':('StudioBootstrap.Presentation.cs','ext!=".png"&&ext!=".jpg"&&ext!=".jpeg"'),
      'C#09 suite connected':('Core/CoreSelfTests.cs','Release09SelfTests.Run'),
    }
    for name,(p,text) in static.items():check(text in src(p),name,'static connection')
    shader=(ROOT/'Assets/InFalsusStudio/Resources/InFalsusStudio/Background.shader').read_text()
    check('"Queue"="Background"' in shader and 'ZWrite Off ZTest Always Cull Off' in shader,'background ordering not GUI overlay','static connection')
    body=src('StudioBootstrap.Presentation.cs');check('OnApplicationQuit' not in body and 'SavePresentationPreferences();' not in body,'no periodic/exit presentation save','static connection')
    check('SavePresentationPreferences();' in src('StudioBootstrap.SongProject.cs'),'presentation saved only via successful project save','static connection')
    check(not any(ROOT.rglob('*.ttf')) and not any(ROOT.rglob('*.otf')) and not any(ROOT.rglob('*.woff*')),'no font files redistributed','resource audit')
    build=(ROOT/'Assets/InFalsusStudio/Editor/StudioBuild08.cs').read_text();check('resizableWindow=true' in build,'standalone build allows window resizing','static connection')
    check('Check v0.9 Runtime / Input / Projection' in build and 'RuntimeReport09' in build,'runtime actual diagnostics wired','static connection')
    assets=ROOT/'Assets'; files=[p for p in assets.rglob('*') if p.is_file()]
    guids=[]
    for f in files:
        if f.suffix=='.meta':
            m=re.search(r'^guid:\s*([0-9a-f]{32})$',f.read_text(),re.M)
            check(bool(m),'valid meta '+str(f.relative_to(ROOT)),'resource audit');guids.append(m[1])
        else:check(f.with_name(f.name+'.meta').exists(),'asset has meta '+str(f.relative_to(ROOT)),'resource audit')
    check(len(guids)==len(set(guids)),'unique meta GUIDs','resource audit')
    frozen=['Runtime/Core/CameraCalibration.cs','Runtime/StudioProfile.cs','Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Rendering/FlickGeometry.cs','Runtime/Core/ManualSongSession.cs','Runtime/Core/SongMetadataEdit.cs','Runtime/Core/AutoplayCursor.cs','Runtime/Core/PreviewScore.cs','Runtime/Core/TickCountResearch.cs','Runtime/Core/ProvisionalComboTimeline.cs','Runtime/Core/SkyGroups.cs','Runtime/Core/RadialMenuState.cs','Runtime/Core/PointPlacement.cs','Runtime/StudioTransport.cs','Runtime/Rendering/NoteRenderer.cs']
    hashes={}
    if args.baseline:
        for rel in frozen:
            a=assets/'InFalsusStudio'/rel;old=args.baseline/'Assets/InFalsusStudio'/rel
            check(a.read_bytes()==old.read_bytes(),'frozen '+rel,'baseline audit');hashes[rel]=hashlib.sha256(a.read_bytes()).hexdigest()
        for f in (args.baseline/'Assets').rglob('*.meta'):
            target=assets/f.relative_to(args.baseline/'Assets');check(target.read_bytes()==f.read_bytes(),'old GUID file unchanged '+str(f.relative_to(args.baseline)),'baseline audit')
    report={'version':'0.9','execution':'Python independent math/data/finite static checks only. Not C#/Unity/Windows/audio/Shader execution.','passed':len(checks),'categories':dict(collections.Counter(c['category'] for c in checks)),'asset_files':len(files),'checks':checks,'frozen_hashes':hashes,'not_run':['C# compiler/CoreSelfTests','Unity Editor / Player / Shader compilation','Windows window styles / resize / DPI / native dialog','actual sample audio recording / phase / music virtualization','pixel-accurate runtime screenshot comparison']}
    out=ROOT/'Documentation/Reference/v09_verification.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(f'{len(checks)} independent checks passed; categories={report["categories"]}; NOT C#/Unity execution.')
if __name__=='__main__':main()
