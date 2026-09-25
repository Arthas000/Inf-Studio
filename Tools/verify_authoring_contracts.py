#!/usr/bin/env python3
"""0.3 contracts retained in 0.3.1: INDEPENDENT reference mathematics + static integration checks.
Does not execute C#, Unity, GUI events, native key state, shaders or audio.
"""
from pathlib import Path
from fractions import Fraction
from decimal import Decimal, ROUND_HALF_UP
import json, re, zipfile, hashlib, argparse
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'Assets/InFalsusStudio'
RESULTS=[]
def check(name, value):
    RESULTS.append({'name':name,'passed':bool(value)})
    if not value:raise AssertionError(name)
def rounded(x):
    x=Fraction(x)
    return max(0,(2*x.numerator+x.denominator)//(2*x.denominator))
def line(n,bpm=130,division=4,origin=0):
    return rounded(Fraction(origin)+Fraction(n*60000,bpm*division))
def snap(t,bpm=130,division=4,origin=0):
    n=Fraction(t-origin)*bpm*division/60000
    nearest=n.numerator//n.denominator
    return min((line(max(0,i),bpm,division,origin) for i in range(nearest-1,nearest+3)),key=lambda v:(abs(v-t),-v))
def run(baseline):
    check('130 BPM exact fourth beat is 1846 rounded ms',line(16)==1846)
    check('Millionth subdivision has no period-rounding drift',line(1_000_000)==115384615)
    data=[line(i) for i in range(4097)]
    check('4096 divisions differ by at most one ms from exact rational',all(abs(Fraction(x)-Fraction(i*60000,520))<=Fraction(1,2) for i,x in enumerate(data)))
    check('Distinct increasing authoring lines at 130 BPM',all(b>a for a,b in zip(data,data[1:])))
    check('Every enumerated integer line is an exact snapping target',all(snap(x)==x for x in data))
    check('Group drag re-snaps each translated head independently',snap(115)==115 and snap(115+115)==231)
    check('Group duration follows absolute snapped endpoints',snap(115+115)-snap(115)==116)
    check('Different interval lengths are retained rather than accumulated 115ms',set(b-a for a,b in zip(data,data[1:]))=={115,116})
    check('Two seconds contains 18 lines at 130 BPM subdivision 4',len([x for x in data if x<=2000])==18)
    for bpm in (73,127,130,175,241):
        for div in (1,3,4,7,16,32):
            vals=[line(n,bpm,div) for n in (0,1,2,37,999,100001)]
            check(f'Rational oracle long timeline BPM={bpm}, subdivision={div}',all(snap(v,bpm,div)==v for v in vals))
    check('4-beat bars at 120 BPM', [line(n*16,120) for n in range(3)]==[0,2000,4000])
    check('3-beat bars at 120 BPM', [line(n*12,120) for n in range(3)]==[0,1500,3000])
    check('Timing origin 1123ms at 150 BPM uses local grid',snap(1220,150,4,1123)==1223)
    def x_snap(x,step=Decimal(5)):
        v=max(Decimal(0),min(Decimal(100),Decimal(str(x))*100))
        q=(v/step).quantize(Decimal(1),rounding=ROUND_HALF_UP)*step
        q=max(Decimal(0),min(Decimal(100),q))
        if 100-v<abs(v-q):q=Decimal(100)
        return q/100
    check('X ruler defaults to 5-percent steps',x_snap(.526)==Decimal('.55'))
    check('X midpoint has deterministic tie handling',x_snap(.525)==Decimal('.55'))
    check('Non-dividing steps include right boundary',x_snap(.999,Decimal(3))==1)
    for split in (2,24,32,100,200):
        center=Decimal(split)/2;width=Decimal(split)/100
        left=center-width/2;right=Decimal('.55')*split
        new_center=(left+right)/2;new_width=right-left
        check(f'Decimal edge edit keeps opposing boundary and split={split}',new_center-new_width/2==left and new_center+new_width/2==right)
    read=lambda p:(ASSETS/p).read_text()
    edit=read('Runtime/StudioBootstrap.Editing.cs');ui=read('Runtime/StudioBootstrap.cs');point=read('Runtime/StudioBootstrap.PointPlacement.cs')
    radial=read('Runtime/StudioBootstrap.Radial.cs');menu=read('Runtime/Core/RadialMenuState.cs');doc=read('Runtime/Core/SpcDocument.cs')
    grid=read('Runtime/Core/AuthoringGrid.cs');render=read('Runtime/Rendering/NoteRenderer.cs');time=read('Runtime/StudioBootstrap.Timeline.cs')
    check('No independent Snap toggle remains','snapEnabled' not in edit+ui+time)
    check('Mandatory time snap ignores legacy bypass argument','showGrid&&authoringGrid!=null?authoringGrid.Nearest(t,Division).TimeMs:AuthoringGrid.RoundMs(t)' in edit)
    check('Renderer uses SAME AuthoringGrid and same subdivision','authoringGrid.Between(a,b,gridDivision)' in render and 'selection,authoringGrid,Division' in ui)
    check('No hard-coded four-beat major-line condition','bool major=b%4==0' not in render)
    check('X ruler derives ticks from configured AirAuthoringGrid','foreach(double norm in airGrid.Ticks())' in point)
    check('Typed source Apply really updates the document','guiSourceText=GUILayout.TextArea' in ui and 'sourceEditText=guiSourceText' in ui and 'ReplaceSourceLine(id,raw)' in ui and 'lines[i].Text=text;' in doc)
    check('Raw typed text does not use generated formatter','d.SetToken(id,i,values[i])' in ui)
    check('Gesture writer uses clean decimal edge arithmetic','AuthoredNumber.AirEdge(doc,e,tail,right,boundaryNorm)' in read('Runtime/Core/EditOperations.cs'))
    check('Explicit LEFT Ctrl key, not merged modifier','GetAsyncKeyState(0xA2)' in read('Runtime/LeftControlGate.cs') and 'KeyCode.LeftControl' in read('Runtime/LeftControlGate.cs'))
    check('Bodies gated by left Ctrl','if(LeftCtrlHeld)BeginNoteDrag(e,id,NoteHandle.Body)' in edit)
    check('Handles built only under left Ctrl','!showNotes||!LeftCtrlHeld' in edit)
    check('Scene drag releases only on LEFT mouse-up','e.rawType==EventType.MouseUp&&e.button==0&&gesture!=Gesture.None' in edit)
    check('Timeline bodies also gated','if(LeftCtrlHeld)' in time and 'Capture(e,Gesture.TimelineMove)' in time)
    check('Release left Ctrl stops edit immediately','if(!LeftCtrlHeld&&PointerEditGesture)' in radial and 'ReleaseGesture();' in radial)
    check('No left-activatable GUI buttons in radial view','GUI.Button(' not in radial and 'GUILayout.Button(' not in radial)
    check('Menu commits only on RIGHT mouse-up','e.rawType==EventType.MouseUp&&e.button==1' in radial and 'radial.Release(hit)' in radial)
    check('Submenu opens at parent button center','childCenter=b.Rect.center' in radial and 'radialCenter=childCenter' in radial)
    check('Submenu dwell is exactly 0.5 seconds','SubmenuSeconds=.5' in menu)
    check('Blank radial release invalidates choice','hover!=RadialItem.Blank' in menu)
    check('Tap/Hold independent defaults','private int tapWidth=1,holdWidth=1' in radial)
    check('Side widths override both defaults','lane==0||lane==5?1' in point)
    check('Sky defaults 10 percent and split100','private double skyWidthPercent=10' in point and 'tool==Tool.FlickRight?4:16,100)' in point)
    check('Click flow replaces drag-to-place','BeginPlacement' not in edit and 'ClickPointPlacement(e)' in edit)
    check('Four Sky stages explicitly retained','StartTime, StartX, EndTime, EndX, Ready' in read('Runtime/Core/PointPlacement.cs'))
    check('Pending clicks never open a history transaction','History.Begin' not in point)
    check('Existing pre-gesture document remains preview source','var next = transactionStart.Clone();' in doc)
    check('All new C# tests wired into Unity self-test','AuthoringSelfTests.Run(check)' in read('Runtime/Core/CoreSelfTests.cs'))
    check('No Packages or ProjectSettings replacements',not (ROOT/'Packages').exists() and not (ROOT/'ProjectSettings').exists())
    guids=[]
    for f in ASSETS.rglob('*'):
        if f.is_file() and f.suffix!='.meta':check('Asset metadata: '+str(f.relative_to(ASSETS)),Path(str(f)+'.meta').is_file())
        if f.suffix=='.meta':
            m=re.search(r'^guid: ([0-9a-f]{32})$',f.read_text(),re.M)
            if m:guids.append(m.group(1))
    check('No duplicate asset GUIDs',len(set(guids))==len(guids))
    unchanged=['Runtime/Core/CameraCalibration.cs','Runtime/StudioProfile.cs','Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Rendering/FlickGeometry.cs','Scenes/InFalsusStudio_Demo.unity','Resources/InFalsusStudio/CalibratedProfile.asset','Resources/InFalsusStudio/LightsOut.spc.txt']
    unchanged+=['Resources/InFalsusStudio/'+n for n in ['VertexOpaque.shader','VertexTransparent.shader','VertexGlow.shader']]
    hashes={}
    if baseline:
        with zipfile.ZipFile(baseline) as z:
            for rel in unchanged:
                file=next(n for n in z.namelist() if n.endswith('/Assets/InFalsusStudio/'+rel))
                content=(ASSETS/rel).read_bytes();check('Locked 0.2 baseline: '+rel,z.read(file)==content);hashes[rel]=hashlib.sha256(content).hexdigest()
            for f in ASSETS.rglob('*.meta'):
                matches=[n for n in z.namelist() if n.endswith('/'+f.relative_to(ROOT).as_posix())]
                if matches:check('Existing GUID metadata unchanged: '+str(f.relative_to(ASSETS)),z.read(matches[0])==f.read_bytes())
    report={'scope':'Independent rational/decimal oracles and static integration; NOT execution of C# or Unity','passed':len(RESULTS),'failed':0,'unchanged_baseline_sha256':hashes,'checks':RESULTS}
    output=ROOT/'Documentation/Reference/authoring_03_checks.json';output.write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(f'{len(RESULTS)} independent mathematical/static assertions passed. C# / Unity NOT executed.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--baseline-zip');args=parser.parse_args();run(args.baseline_zip)
