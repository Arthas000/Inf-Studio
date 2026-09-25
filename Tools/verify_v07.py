#!/usr/bin/env python3
"""Independent reference math + finite source/resource audits. NOT C#/Unity execution."""
from pathlib import Path
from fractions import Fraction as F
from collections import Counter
import hashlib,json,re,math,struct
R=Path(__file__).resolve().parents[1]; A=R/'Assets/InFalsusStudio'; C=A/'Runtime/Core'; report=[]
def check(v,category,name):
    if not v:raise AssertionError(category+': '+name)
    report.append({'category':category,'check':name,'result':'pass'})
def rounded(q):
    q=F(q);return (q.numerator*2+q.denominator)//(q.denominator*2) if q>=0 else -rounded(-q)
def grid(origin,bpm,n,index):return rounded(F(origin)+F(index*60000,bpm*n))
# Independent rational grid, no rounded period accumulation.
for n in [1,4,6,7,8,11,12,96,1024]:
    vals=[grid(1123,150,n,k) for k in range(n+1)]
    check(vals[0]==1123 and vals[-1]==1523,'math','timing-local endpoints N='+str(n))
    check(vals==sorted(vals),'math','monotone rational grid N='+str(n))
check([grid(0,130,4,k) for k in range(5)]==[0,115,231,346,462],'math','130 BPM 1/4 no accumulated rounding')
check(grid(0,130,4,1000000)==115384615,'math','millionth grid from absolute index')
check([F(k,12) for k in range(13) if F(k,12)*4==int(F(k,12)*4)]==[F(0),F(1,4),F(1,2),F(3,4),F(1)],'math','N12 retains quarter and half categories')
for p in [(0,120,4),(1123,150,3),(9123,172,4)]:
    origin,bpm,meter=p
    check(grid(origin,bpm,1,meter)==rounded(F(origin)+F(60000*meter,bpm)),'math','future bar reset '+str(p))
# Range semantics differ deliberately from overlap.
notes=[('tap',999,999),('tap',1000,1000),('flick',2000,2000),('hold',500,2000),('hold',1500,2500),('sky',500,1500)]
sel=[i+1 for i,(k,h,t) in enumerate(notes) if 1000<=(t if k in ('hold','sky') else h)<=2000]
check(sel==[2,3,4,6],'math','inclusive range uses long-note ends')
# Exact-offset copy + explicit regrid, not silently snap all copied notes.
src=[115,231];target=231;ghost=[x+target-min(src) for x in src]
check(ghost==[231,347] and src==[115,231],'math','floating relative copy and unchanged source')
def near(t,n=4,bpm=130):
    center=math.floor(F(t*bpm*n,60000));candidates=[grid(0,bpm,n,k) for k in range(max(0,center-2),center+4)]
    return min(candidates,key=lambda q:(abs(q-t),-q))
check([near(x) for x in ghost]==[231,346],'math','explicit align fixes one-ms offset')
check(near(1001,4,120)==1000 and near(1002,4,120)==1000,'math','alignment collapsing a long note must reject')
for width in [1,2,3,4]:
    starts=[min(lane,5-width) for lane in [1,2,3,4]]
    check(all(1<=s and s+width<=5 for s in starts),'math','wide note edit fit '+str(width))
# Voice budgets reserve music even for tiny configurations.
for real in [1,2,4,7,8,16,32,64,128]:
    shots=max(0,min(20,real-7));beds=4 if real>=7 else max(0,real-2)
    check(shots+beds+1<=real and shots<=20 and beds<=4,'audio reference','voice reservation '+str(real))
check(0<48<160,'audio reference','music > hold > shot priority')
# Geometry head connectivity with explicit independent heads and exact duplicates.
skies=[(1,1000,2000,(.15,.35),(.4,.6)),(2,2000,3000,(.4,.6),(.5,.7)),(3,3001,3501,(.5,.7),(.5,.7)),(4,2000,3000,(.85,.95),(.85,.95))]
starts=[]
for i,t0,t1,head,tail in skies:
    predecessor=any(p0<t0 and abs(p1-t0)<=.001 and min(pt[1],head[1])+1e-7>=max(pt[0],head[0]) for j,p0,p1,ph,pt in skies if j!=i)
    if not predecessor:starts.append(i)
check(starts==[1,3,4],'geometry reference','connected first-only heads; disjoint region still heads')
# Needle endpoint: independent camera ray-plane projection to 75% up viewport.
pitch=math.radians(23.65212);cy=3.086448;back=3.219452;sky=1.156494+.034
up=(0,math.cos(pitch),math.sin(pitch));forward=(0,-math.sin(pitch),math.cos(pitch));tan=math.tan(math.radians(25))
rayy=forward[1]+.5*tan*up[1];rayz=forward[2]+.5*tan*up[2]
s=(sky-cy)/rayy;z=-back+s*rayz
camy=(sky-cy)*up[1]+(z+back)*up[2];camz=(sky-cy)*forward[1]+(z+back)*forward[2]
check(s>0 and z>0 and abs((camy/camz/tan+1)/2-.75)<1e-9,'geometry reference','needle same-Y plane ends upper quarter')
# Explicit memory/source contracts, finite static checks only (not executed C#).
files={p.name:p.read_text() for p in (A/'Runtime').glob('StudioBootstrap*.cs')}
def has(file,needle,label):check(needle in files[file],'source connection',label)
has('StudioBootstrap.cs','keySounds.AutoPlayEnabled=autoCursor','autoplay gates FX sound')
has('StudioBootstrap.GuiFrame.cs','guiShowUi=showUi&&!transport.Playing','play mode hides editor control tree')
has('StudioBootstrap.NotePanel.cs','QueueBatchField','choice buttons use queued RAM edits')
has('StudioBootstrap.NotePanel.cs','SelectionAuthoring.Field','panel uses common atomic batch core')
has('StudioBootstrap.NotePanel.cs','Time.realtimeSinceStartupAsDouble-pendingNoteAt<.28','numeric input debounces incomplete typing')
has('StudioBootstrap.NotePanel.cs','sources.Any','stale source validation')
has('StudioBootstrap.NotePanel.cs','foreach(var field in edits)','fast option click retains pending numeric')
check('ApplyNoteForm' not in files['StudioBootstrap.NotePanel.cs'],'source connection','no ordinary per-note Apply button handler')
has('StudioBootstrap.Editing.cs','e.control||e.command||e.shift','Ctrl refines selection')
has('StudioBootstrap.Editing.cs','if(!EditHeld||transport.Playing||WorkflowActive','drag modifier/play/preview gating')
has('StudioBootstrap.Radial.cs','e.button==1&&(WorkflowActive||pointPlacement.Pending)','RMB first cancels draft')
has('StudioBootstrap.Radial.cs','if(showGrid){items.Add(RadialItem.Align)','align only visible with beat grid')
has('StudioBootstrap.SelectionWorkflow.cs','workflowSelection=selection.ToArray()','preview retains original selection')
has('StudioBootstrap.SelectionWorkflow.cs','floatingHidden=cut?','cut is hide-only before drop')
has('StudioBootstrap.SelectionWorkflow.cs','pending.Place','preview commits at one placement action')
has('StudioBootstrap.SelectionWorkflow.cs','PointerAuthoringTime','range/paste use shared projected grid target')
has('StudioBootstrap.Chrome.cs','Deliberately NOT CancelGesture','progress scrubbing preserves drafts')
has('StudioBootstrap.Chrome.cs','exitDeadline=now+1','one-second second-click exit')
has('StudioBootstrap.Chrome.cs','reference/InitialBpm','reference BPM only view normalization')
has('StudioBootstrap.Chrome.cs','transport.SetAudioOffset(-x)','positive UI delay starts music later')
has('StudioBootstrap.Chrome.cs','if(i!=active)DrawDifficultyTab','inactive tabs drawn first')
has('StudioBootstrap.Chrome.cs','DrawDifficultyTab(active,true)','selected tab drawn front')
has('StudioBootstrap.Panels.cs','BeginRangeSelection','visible range button connected')
has('StudioBootstrap.Panels.cs','SkyAttackPolicy.ContinuousRegionStart','runtime default sky attacks first connected head')
has('StudioBootstrap.Timeline.cs','CommitPendingNoteNow','global Save includes valid typed note draft')
core=(C/'SelectionAuthoring.cs').read_text();check('File.' not in core,'source connection','selection core has no file writes')
check('Release07SelfTests.Run(check)' in (C/'CoreSelfTests.cs').read_text(),'source connection','C# regressions linked into actual runner')
audio=(A/'Runtime/StudioKeySounds.cs').read_text();transport=(A/'Runtime/StudioTransport.cs').read_text()
check('AudioVoiceBudget.MusicPriority' in transport and 'AudioVoiceBudget.Shots' in audio and 'AudioVoiceBudget.Holds' in audio,'source connection','production audio priorities and budgets wired')
check('AudioSettings.Reset(' not in audio and 'isPlaying' not in audio,'source connection','FX does not reset device or reuse future scheduled slots via isPlaying')
render=(A/'Runtime/Rendering/NoteRenderer.cs').read_text();effects=(A/'Runtime/Rendering/HitFeedbackRenderer.cs').read_text()
check('DrawSkyBrackets' in render and 'connections.Starts.Contains' in render,'source connection','same connected start model drives brackets')
check('pixelRect.height*.75f' in render,'source connection','needle limit wired to viewport')
check('if(EffectCount>=128)break' in effects and 'if(enabled)' in effects,'source connection','bounded play-only procedural feedback')
check('showFeedback&&autoCursor&&showNotes&&transport.Playing' in files['StudioBootstrap.cs'],'source connection','VFX disabled while editing/seeking paused')
# Frozen contracts, supplied assets, complete unique Unity GUID inventory.
reference=R/'Documentation/Reference'
for path,sha in json.loads((reference/'v07_frozen_baseline.json').read_text()).items():
    check(hashlib.sha256((R/path).read_bytes()).hexdigest()==sha,'frozen file',path)
for item in json.loads((reference/'v07_user_assets.json').read_text()):
    raw=(R/item['destination']).read_bytes();w,h=struct.unpack('>II',raw[16:24])
    check(hashlib.sha256(raw).hexdigest()==item['sha256'] and (w,h)==(1024,1024),'user asset',item['source'])
assets=[f for f in (R/'Assets').rglob('*') if f.suffix!='.meta']
check(all(Path(str(f)+'.meta').is_file() for f in assets),'asset inventory','every asset and folder has meta')
guids=[]
for f in (R/'Assets').rglob('*.meta'):
    m=re.search(r'^guid:\s*([a-f0-9]{32})$',f.read_text(),re.M)
    if not m:raise AssertionError('invalid meta '+str(f))
    guids.append(m.group(1))
check(len(guids)==len(set(guids)),'asset inventory','no GUID collision')
check(not any(f.suffix.lower() in ['.ttf','.otf','.woff','.woff2'] for f in R.rglob('*')),'asset inventory','no font binaries shipped')
check(not (R/'ProjectSettings').exists() and not (R/'Packages').exists(),'asset inventory','does not overwrite project settings or packages')
fixture_counts={}
for f in sorted((R/'Fixtures').glob('07_*.spc')):
    text=f.read_text();check(text.startswith('chart('),'fixtures','synthetic fixture '+f.name)
    fixture_counts[f.name]=sum(bool(re.match(r'^(tap|hold|skyarea|flick)\(',line)) for line in text.splitlines())
# Bare method audit for partial class, lexical not overload/type checking.
combined='\n'.join(files.values());clean=re.sub(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|//[^\n]*|/\*[\s\S]*?\*/',' ',combined)
declarations=re.findall(r'\b(?:private|public|internal)\s+(?:static\s+)?\w+(?:<[^>]+>)?(?:\[\])?\s+(\w+)\s*\(',clean)
check(len(declarations)==len(set(declarations)),'source connection','no duplicate Bootstrap method names')
result={'scope':'Independent Python reference math and finite source/asset checks. No C# / Unity compilation or playback.','checks':len(report),'categories':dict(Counter(q['category'] for q in report)),'fixture_note_records':fixture_counts,'results':report,'unity_execution':'not_run','csharp_compilation':'not_run'}
(reference/'v07_verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2))
print(json.dumps({k:v for k,v in result.items() if k!='results'},ensure_ascii=False,indent=2))
