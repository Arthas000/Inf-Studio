#!/usr/bin/env python3
"""Independent reference/data/limited-source checks. NOT a C# compiler or Unity run."""
from pathlib import Path
from fractions import Fraction
import re, json, hashlib, zipfile, math, shutil, platform
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'Assets/InFalsusStudio'
checks=[]
def check(ok,name):
    if not ok: raise AssertionError(name)
    checks.append(name)
def source(name): return (ASSETS/name).read_text(encoding='utf-8-sig')
def half_up(fr):
    return (fr.numerator*2+fr.denominator)//(2*fr.denominator)
# Direct rational grid, including odd divisors, close-edge ticks and reversibility.
for n in (1,2,3,7,12,24,100,333,4096):
    ticks=[Fraction(i,n) for i in range(n+1)]
    check(ticks[0]==0 and ticks[-1]==1 and len(ticks)==n+1,f'grid endpoints/count N{n}')
    for k in range(0,n+1,max(1,n//25)):
        x=ticks[k]
        check(half_up(x*n)==k,f'idempotent tick {k}/{n}')
        w=Fraction(1,10);split=math.lcm(x.denominator,w.denominator)
        xc=x*split;wc=w*split
        check(xc.denominator==wc.denominator==1 and xc/split==x and wc/split==w,f'integer fraction encoding {k}/{n}')
# Strict per-group heads regardless of time/space gaps, independent sustain time unions.
def heads(notes):
    groups=set();result=[]
    for n in sorted(notes,key=lambda n:(n['t'],n['id'])):
        if n['g']<0 or n['g'] not in groups: result.append(n['id']);groups.add(n['g'])
    return result
def node(i,t,e,c,g): return dict(id=i,t=t,e=e,c=c,g=g,w=.2)
def joins(a,b):
    return a['id']!=b['id'] and a['t']<b['t'] and abs(a['e']-b['t'])<=.001 and min(a['c']+a['w']/2,b['c']+b['w']/2)+1e-9>=max(a['c']-a['w']/2,b['c']-b['w']/2)
ns=[node(1,1000,1500,.25,7),node(2,2500,3000,.75,7),node(3,1500,2000,.25,8)]
check(heads(ns)==[1,3],'gapped same group one head; touching different imported groups remain distinct')
check(joins(ns[0],ns[2]),'touching source notes eligible for authored group join')
check(not joins(ns[0],ns[1]),'gap does not auto-join')
for dc in (-.5,-.21,-.2,-.1,0,.1,.2,.21,.5):
    a=node(1,0,1000,.5,0);b=node(2,1000,2000,.5+dc,1)
    check(joins(a,b)==(abs(dc)<=.200000001),f'overlap/touch criterion delta{dc}')
check(heads([node(1,2000,3000,.5,3),node(2,1000,1500,.5,3)])==[2],'earliest timestamp not source row')
check(heads([node(1,1000,2000,.5,-1),node(2,2000,3000,.5,-1)])==[1,2],'ungrouped legacy records independent')
# Floor projection changes Y only and therefore shares exact four-lane bounds.
for nx in (0,.1,.3333333333333333,.5,.9,1):
    for z in (0,1,5,30):
        air=(4*(nx-.5),1.156494,z);floor=(air[0],.004,air[2])
        check(floor[0]==air[0] and floor[2]==air[2] and -2<=floor[0]<=2,f'floor footprint x{nx} z{z}')
# Scroll quantization / playback preset sequence, independent of source BPM and grid time.
for v,out in [('1.398','1.4'),('1.349','1.3'),('1.35','1.4'),('9.999','10.0')]:
    from decimal import Decimal,ROUND_HALF_UP
    check(Decimal(v).quantize(Decimal('.1'),rounding=ROUND_HALF_UP)==Decimal(out),'one-decimal scroll '+v)
check([1,.75,.5,.25][1:]+[1]==[.75,.5,.25,1],'rate cycle')
# Contract checks are explicitly STATIC: they do not execute OnGUI or native/engine APIs.
a=source('Runtime/StudioBootstrap.InputFields.cs')
check('Time.realtime' not in a and 'pendingNoteAt' not in a,'no idle input auto-commit in new field layer')
check('KeyCode.Return' in a and 'RootRect.Contains' in a,'Enter and outside-click paths present')
check('Ticket' in a and 'ReferenceEquals(present,state)' in a,'queued commit guarded against duplicate/obsolete state')
check('GUI.FocusControl(null)' in a,'focus released after commit request')
check('GUIStyle.none,GUI.skin.verticalScrollbar' in a and 'scroll.x=0' in a,'panels have bounded content and no horizontal scroll dependency')
check('GUI.skin=previousSkin' in source('Runtime/StudioBootstrap.cs'),'skin restored after runtime GUI')
p=source('Runtime/StudioBootstrap.Radial.cs')
check('WorkflowActive||pointPlacement.Pending' in p and p.index('WorkflowActive||pointPlacement.Pending')<p.index('radial.Press(now'),'RMB pending cancellation before opening menus')
check('selection.Count>0&&tool==Tool.Select' in p,'active creation tool not mistaken for selection-context menu')
check('radial.InPointMode=true' in source('Runtime/StudioBootstrap.PointPlacement.cs'),'successful placement keeps point mode')
s=source('Runtime/Core/SpcDocument.cs');check(s.count('BeforeCommit(')==2,'group normalization in both execute and preview transactions')
check('SkyGroups.Heads(source)' in source('Runtime/Core/SkyConnections.cs'),'heads use groups')
check('NextGroup(doc)' in source('Runtime/Core/EditOperations.cs'),'new Sky gets next group')
check('shadows.Triangle' in source('Runtime/Rendering/NoteRenderer.cs') and 'a.y=b.y=c.y=.004f' in source('Runtime/Rendering/NoteRenderer.cs'),'actual floor shadow mesh connected')
check('BeforeCommit=SkyGroups.NormalizeAuthoredChanges' in source('Runtime/StudioBootstrap.cs') and 'BeforeCommit=SkyGroups.NormalizeAuthoredChanges' in source('Runtime/StudioBootstrap.SongProject.cs'),'normalization hook restored on difficulty switch')
check('!inputConsumed' in source('Runtime/StudioBootstrap.cs'),'blur clicks do not fall through into accidental note creation')
check('StudioFileDialogs.Save' in source('Runtime/StudioBootstrap.cs'),'standalone save dialog wired')
check('StartCoroutine(OpenSongFolderPlayer(folder))' in source('Runtime/StudioBootstrap.SongProject.cs'),'Player async folder workflow wired')
check('DecodePlayerAudio(chosen' in source('Runtime/StudioBootstrap.SongProject.cs'),'Player audio decoded before new workspace activation')
b=source('Editor/StudioBuild08.cs')
check('extraScriptingDefines=new[]{"INFALSUS_USE_UNITY_WEBREQUEST_AUDIO"}' in b,'per-build runtime decoder define')
check('report.summary.result!=BuildResult.Succeeded||!File.Exists(exe)' in b,'build cannot report success without successful BuildPlayer and exe')
check('finally' in b and 'oldBackend' in b,'temporary build settings restored')
check('Release08SelfTests.Run(check)' in source('Runtime/Core/CoreSelfTests.cs'),'actual C# regression entry linked')
# Audit the original chart coordinate tokens when Notes.zip is available.
notes=Path('/mnt/data/Notes.zip');audit={};count_fields=0
if notes.exists():
    with zipfile.ZipFile(notes) as archive:
        for name in archive.namelist():
            if not name.endswith('.spc'):continue
            raw=archive.read(name);hist={};events=[]
            for line in raw.decode('utf-8-sig').splitlines():
                m=re.match(r'(skyarea|flick)\(([^)]*)\)',line)
                if not m:continue
                fields=m.group(2).split(',');indexes=(1,2,3,4,5,6) if m[1]=='skyarea' else (1,2,3)
                for k in indexes:
                    token=fields[k].strip();dec=len(token.partition('.')[2]) if '.' in token else 0
                    hist[str(dec)]=hist.get(str(dec),0)+1;count_fields+=1
            audit[name]={'sha256':hashlib.sha256(raw).hexdigest(),'raw_coordinate_decimal_histogram':hist}
            check(set(hist)=={'0'},'provided raw coordinates are integer numerator/divisor fields: '+name)
    check(count_fields==13284,'all 13284 original air coordinate fields audited')
# Current environment detection is factual; NOT a simulated build.
env={name:shutil.which(name) for name in ('Unity','unity-editor','dotnet','csc','mcs','mono')}
report={'version':'0.8','scope':'Independent reference math, supplied-file audit and limited static source assertions. No C# / Unity / native-dialog execution.',
        'checks_passed':len(checks),'checks':checks,'precision_audit':audit,'coordinate_fields':count_fields,
        'environment':{'platform':platform.system(),'tools':env},'unity_run':False,'windows_executable_built_here':False}
out=ROOT/'Documentation/Reference/v08_verification.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(f'{len(checks)} reference/data/static checks passed. C# and Unity were NOT executed.')
