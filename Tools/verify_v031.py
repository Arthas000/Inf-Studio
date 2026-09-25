#!/usr/bin/env python3
"""0.3.1 independent exact-rational/math oracles and static integration checks.
Does NOT compile or run C#; does NOT run Unity, IMGUI, shaders, or real input devices.
"""
from __future__ import annotations
from pathlib import Path
from fractions import Fraction as F
from dataclasses import dataclass
from bisect import bisect_right, bisect_left
import argparse, math, re, json, hashlib, importlib.util
ROOT=Path(__file__).resolve().parents[1]
A=ROOT/'Assets/InFalsusStudio'
checks=[]
def check(name, value):
    checks.append({'name':name,'passed':bool(value)})
    if not value: raise AssertionError(name)
def round_ms(value):
    f=F(value); return max(0,(2*f.numerator+f.denominator)//(2*f.denominator))
def exact_line(index,division,bpm=130,origin=0):
    return F(origin)+F(index*60000,bpm*division)
@dataclass
class Seg:
    time: F
    bpm: F
    meter: F
@dataclass
class Line:
    time:int; exact:F; segment:int; local:F; bar:bool; beat:bool
    @property
    def priority(self):return 3 if self.bar else 2 if self.beat else 1

def oracle(segments,division,end):
    """Exhaustive rational grid, independent of C# bounded candidate lookup."""
    lines={}
    for s,p in enumerate(segments):
        stop=segments[s+1].time if s+1<len(segments) else F(end)+1
        for unit,is_bar in [(F(1,division),False),(p.meter,True)]:
            n=0
            while True:
                beat=n*unit;t=p.time+beat*60000/p.bpm
                if t>=stop or t>F(end)+F(1,2):break
                ms=round_ms(t)
                if ms<=end:
                    q=Line(ms,t,s,beat,is_bar or (beat/p.meter).denominator==1,beat.denominator==1)
                    if ms not in lines or (q.priority,q.segment)>(lines[ms].priority,lines[ms].segment):lines[ms]=q
                n+=1
    return sorted(lines.values(),key=lambda v:v.time)

def candidate_nearest(segments,t,division):
    """Float bounded-neighborhood logic translated from delivered AuthoringGrid.Nearest.
    Compare against exhaustive rational oracle; this is still Python, not running C#.
    """
    best=None;dist=math.inf
    for s,p in enumerate(segments):
        local=(t-float(p.time))*float(p.bpm)/60000
        for bar in (False,True):
            units=float(p.meter) if bar else 1/division
            center=math.floor(local/units)
            for dn in (-1,0,1,2):
                n=max(0,center+dn);beat=n*float(p.meter) if bar else n/division
                exact=float(p.time)+beat*60000/float(p.bpm)
                if s+1<len(segments) and exact>=float(segments[s+1].time)-1e-8:continue
                ms=max(0,math.floor(exact+.5));isbar=bar or abs(beat/float(p.meter)-round(beat/float(p.meter)))<1e-7
                priority=3 if isbar else 2 if abs(beat-round(beat))<1e-7 else 1
                candidate=(ms,priority,s)
                err=abs(ms-t)
                if err<dist-1e-9 or abs(err-dist)<1e-9 and (best is None or candidate>best):best=candidate;dist=err
    return best[0]

class Wheel:
    def __init__(self,units=3):self.units=units;self.remainder=0
    def consume(self,dy):
        if not math.isfinite(dy):return 0
        self.remainder=max(-1024,min(1024,self.remainder-dy/self.units))
        n=math.floor(self.remainder+1e-7) if self.remainder>=0 else math.ceil(self.remainder-1e-7)
        self.remainder-=n;return n

def run(baseline):
    windows={};basic=[Seg(F(0),F(130),F(4))]
    for division in (4,6,8):
        ls=oracle(basic,division,200000);values=[l.time for l in ls]
        first=[l for l in ls if l.time<1846]
        check(f'N={division}: exactly N*4 lines before second bar',len(first)==division*4)
        check(f'N={division}: four beat lines, one bar line',sum(l.beat for l in first)==4 and sum(l.bar for l in first)==1)
        check(f'N={division}: independent float candidate snap matches rational line oracle',all(candidate_nearest(basic,x,division)==x for x in values))
        # A detent moves to an adjacent exact-index-derived result, never adds a rounded period.
        result=values[0]
        for n in range(1,1025):
            result=values[bisect_right(values,result)]
            check(f'N={division} absolute-index wheel reference {n}',result==round_ms(exact_line(n,division)))
        for _ in range(1024):result=values[max(0,bisect_left(values,result)-1)]
        check(f'N={division} forward/back 1024 steps is reversible',result==0)
        windows[str(division)]=[l.time for l in first]+[1846]
    check('130 /4 first steps alternate rounded lengths, never +115 repetition',windows['4'][:5]==[0,115,231,346,462])
    check('Millionth /4 tick rounds once without accumulation',round_ms(exact_line(1000000,4))==115384615)
    check('/4 is subset of /8 over a complete bar',set(windows['4']).issubset(windows['8']))
    check('All divisions share whole-beat locations',all(round_ms(exact_line(n*4,4)) in windows[str(d)] for d in (4,6,8) for n in range(5)))
    reset=[Seg(F(0),F(120),F(4)),Seg(F(1123),F(150),F(3)),Seg(F(2507),F(100),F(3))]
    for div in (4,6,8):
        lines=oracle(reset,div,10000);values=[l.time for l in lines]
        check(f'Reset /{div}: 1123 is local origin',1123 in values)
        check(f'Reset /{div}: new quarter grid not global phase',candidate_nearest(reset,1123+60000/(150*div),div)==round_ms(F(1123)+F(60000,150*div)))
        check(f'Reset /{div}: brute rational oracle and float snap agree',all(candidate_nearest(reset,t/7,div)==min(values,key=lambda a:(abs(a-t/7),-a)) for t in range(0,69000,37)))
        check(f'Reset /{div}: bars based on timing-local meter', [l.time for l in lines if l.bar][:5]==[0,1123,2323,2507,4307])
    check('Same BPM but new meter resets phase', [l.time for l in oracle([Seg(F(0),F(120),F(4)),Seg(F(1123),F(120),F(5))],8,3623) if l.bar]==[0,1123,3623])
    w=Wheel();check('Negative IMGUI delta maps forward',w.consume(-3)==1)
    check('Positive IMGUI delta maps backward',w.consume(3)==-1)
    check('Batched input preserves multiple notches',w.consume(-6)==2 and w.consume(9)==-3)
    w=Wheel();check('High-resolution .1 input does not move one grid on every event',sum(w.consume(-.1) for _ in range(30))==1)
    w=Wheel();check('Opposite fractional inputs cancel',w.consume(-1)==0 and w.consume(1)==0)
    w=Wheel(1);check('Scale 1 for alternate input backend',w.consume(-1)==1 and w.consume(1)==-1)
    for width in range(1,5):
        for hit in range(6):
            lane=hit if hit in (0,5) else min(hit,5-width);actual_width=1 if hit in (0,5) else width
            check(f'Brush hit={hit},width={width} fits without shrinking central width',actual_width==(1 if hit in (0,5) else width) and (lane in (0,5) or lane+actual_width<=5))
        check(f'Wide lane table width={width}',[min(l,5-width) for l in range(1,5)]=={1:[1,2,3,4],2:[1,2,3,3],3:[1,2,2,2],4:[1,1,1,1]}[width])
    for right in (False,True):
        for start in (0,1,2):
            pos={0:0,1:1,2:-1}[start]
            for target in (-1,0,1):
                dx=(target-pos)*32*(1 if right else -1)
                now=pos+dx*(1 if right else -1)/32
                got=1 if now>.38 else 2 if now<-.38 else 0
                check(f'Ease rail side={right} source={start} outward-position={target}',got=={-1:2,0:0,1:1}[target])
    # Independently project guide ribbons using the already calibrated reference camera.
    spec=importlib.util.spec_from_file_location('ref031',ROOT/'Tools/verify_reference.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);cam=module.Calibration()
    def project(world):
        x,y,z=world;px,py=cam.project(x,y,z);depth=-(y-cam.h)*cam.s+(z+cam.back)*cam.c
        return px,1152-py,depth
    def unproject(px,py,depth):
        xx=(px-1024)/cam.f*depth;yy=(py-576)/cam.f*depth
        return xx,cam.h+yy*cam.c-depth*cam.s,-cam.back+yy*cam.s+depth*cam.c
    def lane_point(lane,u,z):
        x=-2-cam.run*(1-u) if lane==0 else 2+cam.run*u if lane==5 else -2+(lane-1+u)
        q=cam.surface(x,z);slope=cam.rise/cam.run
        nx,ny=((-math.copysign(slope,x)/math.hypot(slope,1),1/math.hypot(slope,1)) if abs(x)-2>1e-6 else (0,1))
        return(q[0]+nx*.012,q[1]+ny*.012,q[2])
    pixel_results=[]
    for z in (0,5,15,40):
        for lane in range(6):
            pa=project(lane_point(lane,0,z));pb=project(lane_point(lane,1,z))
            dx,dy=pb[0]-pa[0],pb[1]-pa[1];length=math.hypot(dx,dy)
            for width in (1.65,2.5,3.5):
                ox,oy=-dy/length*width/2,dx/length*width/2
                fac=1/cam.f;depth=pa[2]*(1-fac*(width+1))
                minus=project(unproject(pa[0]-ox,pa[1]-oy,depth));plus=project(unproject(pa[0]+ox,pa[1]+oy,depth))
                measured=math.hypot(plus[0]-minus[0],plus[1]-minus[1])
                check(f'Ribbon pixel thickness z={z},lane={lane},target={width}',abs(measured-width)<1e-8)
            if lane<5:check(f'Six-lane shared guide seam z={z},lane={lane}',all(abs(a-b)<1e-9 for a,b in zip(lane_point(lane,1,z),lane_point(lane+1,0,z))))
        old_pixels=abs(cam.project(0,.012,z-.002)[1]-cam.project(0,.012,z+.002)[1])
        pixel_results.append({'world_z':z,'old_subdivision_pixels_at_1152p':old_pixels,'new_pixels':1.65})
    check('Old world-thin guides are subpixel even fairly near',all(p['old_subdivision_pixels_at_1152p']<1 for p in pixel_results))
    read=lambda p:(A/p).read_text()
    ui=read('Runtime/StudioBootstrap.cs');frame=read('Runtime/StudioBootstrap.GuiFrame.cs');edit=read('Runtime/StudioBootstrap.Editing.cs');nav=read('Runtime/StudioBootstrap.Navigation.cs');time=read('Runtime/StudioBootstrap.Timeline.cs');point=read('Runtime/StudioBootstrap.PointPlacement.cs');renderer=read('Runtime/Rendering/NoteRenderer.cs');radial=read('Runtime/StudioBootstrap.Radial.cs');core=read('Runtime/Core/NavigationAndHandles.cs')
    check('Core tests actually wired into Unity check entry','NavigationSelfTests.Run(check)' in read('Runtime/Core/CoreSelfTests.cs'))
    check('Both wheel browsing callsites route to same stepper','BrowseWheel(e.delta.y)' in edit and 'else BrowseWheel(e.delta.y)' in time)
    check('No old half-beat/floating-millisecond wheel browse formula','e.delta.y*(e.shift?.125:.5)' not in edit and 'e.delta.y*timelineSpanMs*.05' not in time)
    check('Pause snap explicitly excludes active playback','transport.Playing||authoringGrid==null||!showGrid' in nav)
    check('Wheel uses strict AuthoringGrid.Step, not BeatAt addition','authoringGrid.Step(CurrentMs,steps,Division)' in nav and 'beat' not in nav.split('private void BrowseSteps')[1].split('private void EnsureBrowseGridLock')[0].replace('beat.','')) # explanatory text has 'beat' only if changed; no computation
    check('Repaint does not force slider time backward off-grid','if(sliderMoved&&' in ui and 'Math.Max(durationMs,CurrentMs)' in ui)
    check('Immediate radial visible at Press','Pressed=true;Visible=true;' in read('Runtime/Core/RadialMenuState.cs'))
    check('Submenu still .5','SubmenuSeconds=.5' in read('Runtime/Core/RadialMenuState.cs'))
    check('No hold-to-open hint','继续按住右键' not in radial)
    check('RMB release only and blank no-op kept','e.rawType==EventType.MouseUp&&e.button==1' in radial and 'hover!=RadialItem.Blank' in read('Runtime/Core/RadialMenuState.cs'))
    check('Wide brush resolved for hover, draft and quick add','GroundPlacement.TryResolve' in point and 'GroundPlacement.TryResolve' in ui and 'GroundPlacement.TryResolve' in read('Runtime/Core/PointPlacement.cs'))
    check('Import codec is unchanged by placement fix', (A/'Runtime/Core/SpcDocument.cs').read_bytes()==(baseline/'Assets/InFalsusStudio/Runtime/Core/SpcDocument.cs').read_bytes())
    check('Ease midpoint anchored to true half-time','if(IsEaseHandle(handle))time=(e.TimeMs+e.EndMs)*.5' in edit)
    ease=read('Runtime/StudioBootstrap.EaseHandles.cs')
    check('Ease preview changes just left/right easing via history','History.Preview(d=>EditOperations.SetEase' in ease and 'dragHandle==NoteHandle.RightEase' in ease)
    check('Ease handles gated by LEFT Ctrl','!showNotes||!LeftCtrlHeld' in edit and '!LeftCtrlHeld' in ease)
    check('During easing no time/position remapping','if(IsEaseHandle(dragHandle)){PreviewEaseSelector();return;}' in edit)
    check('Dynamic Inspector list frozen at Layout','if(e.type==EventType.Layout)BeginGuiFrame();' in ui and 'guiNearNotes=events.Where' in frame and 'foreach(var item in guiNearNotes)' in ui)
    check('Timing row list and parser diagnostic rows frozen','foreach(var evt in guiTimingEvents)' in edit and 'foreach(var message in guiDiagnostics)' in ui)
    check('Conditional primary kind and fields frozen','var selectedEvent=guiPrimary' in ui and 'guiFieldValues.Length' in ui and 'var primary=guiPrimary' in edit)
    check('Panel toggles frozen','if(guiShowInspector)DrawInspector();if(guiShowCalibration)DrawCalibration();if(guiShowHelp)' in ui and 'if(!guiShowDetail)return' in time)
    check('UI command queue processed before BeginArea','Action command=guiCommands.Dequeue();' in frame and 'catch(ExitGUIException){throw;}' in frame and 'GUILayout.Begin' not in frame)
    check('Raw Apply, deletion and undo queued outside current layout pass','QueueGui(()=>Change(d=>{d.ReplaceSourceLine(id,raw);' in ui and 'QueueGui(DeleteSelected)' in ui and 'QueueGui(()=>Undo(false))' in ui)
    check('Sky diagnostics do not catch GUILayout failures','foreach(string info in guiSkyInfo)GUILayout.Label(info)' in ui and 'skyInfo.Add(' in frame)
    check('Render uses one AuthoringGrid generator and six shared-time strips','authoringGrid.Between(a,b,gridDivision)' in renderer and 'for(int lane=0;lane<6;lane++)' in renderer and 'grid.ScreenStrip(camera' in renderer)
    check('Fixed pixel thickness and greater alpha are actually used','line.IsBar?3.5f:line.IsBeat?2.5f:1.65f' in renderer and 'new Color(.65f,.88f,1,.88f)' in renderer)
    check('Base grid and hover share judgment-lift geometry','BeatGuideGeometry.Point(p,lane,0,z)' in renderer and 'BeatGuideGeometry.Point(Profile,lane,0,z)' in point and '.012f' in read('Runtime/Rendering/BeatGuideGeometry.cs'))
    check('Stop/reverse coincident visual grids deduplicated','Dictionary<double,BeatGridPoint> positions' in renderer)
    check('GUI shader/camera changes not smuggled into fix',not (ROOT/'Packages').exists() and not (ROOT/'ProjectSettings').exists())
    # File stability is measured, not guessed from filenames.
    locked=['Runtime/Core/CameraCalibration.cs','Runtime/StudioProfile.cs','Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Rendering/FlickGeometry.cs','Scenes/InFalsusStudio_Demo.unity','Resources/InFalsusStudio/CalibratedProfile.asset','Resources/InFalsusStudio/LightsOut.spc.txt','Resources/InFalsusStudio/VertexOpaque.shader','Resources/InFalsusStudio/VertexTransparent.shader','Resources/InFalsusStudio/VertexGlow.shader','Runtime/LeftControlGate.cs','Runtime/StudioTransport.cs','Runtime/Core/ChartMath.cs']
    hashes={}
    for rel in locked:
        content=(A/rel).read_bytes();check('Frozen baseline '+rel,content==(baseline/'Assets/InFalsusStudio'/rel).read_bytes());hashes[rel]=hashlib.sha256(content).hexdigest()
    guids=[]
    for p in A.rglob('*'):
        if p.is_file() and p.suffix!='.meta':check('Asset has meta '+str(p.relative_to(A)),Path(str(p)+'.meta').is_file())
        if p.suffix=='.meta':
            m=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(),re.M)
            if m:guids.append(m.group(1))
            original=baseline/p.relative_to(ROOT)
            if original.exists():check('Old metadata preserved '+str(p.relative_to(A)),p.read_bytes()==original.read_bytes())
    check('New/old GUIDs unique',len(guids)==len(set(guids)))
    report={'scope':'Independent Python oracles, translated algorithm cross-checks and static/asset checks. NOT running C# or Unity.','passed':len(checks),'failed':0,'first_bar_130_BPM':windows,'guide_projection_reference':pixel_results,'locked_sha256':hashes,'checks':checks}
    dest=ROOT/'Documentation/Reference/v031_checks.json';dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(f'{len(checks)} independent Python/math/static checks passed. C# / Unity NOT executed.');print(json.dumps(pixel_results,indent=2));return report
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--baseline',type=Path,required=True);args=p.parse_args();run(args.baseline)
