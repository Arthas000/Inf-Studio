#!/usr/bin/env python3
"""Independent Fraction arithmetic and source/resource checks, NOT C#/Unity execution.
python Tools/verify_v051.py --baseline /path/to/extracted/v05/root [--notes /path/to/Notes.zip]
Generates Documentation/Reference/v051_verification.json and timing_audit_051.json.
"""
from pathlib import Path
from fractions import Fraction as F
import argparse, collections, hashlib, importlib.util, json, math, random, re, zipfile
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'Assets/InFalsusStudio'; RUN=ASSETS/'Runtime'; CORE=RUN/'Core'
checks=[]
def check(name, result, category='independent math'):
    checks.append({'name':name,'category':category,'passed':bool(result)})
    if not result: raise AssertionError(name)
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def parse(path):
    events=[]
    for line_no,line in enumerate(path.read_text(encoding='utf-8-sig').splitlines(),1):
        m=re.match(r'^\s*([\w]+)\(([^)]*)\)',line)
        if m:
            try: args=[F(v.strip()) for v in m[2].split(',')]
            except (ValueError,ZeroDivisionError): continue
            events.append({'kind':m[1].lower(),'args':args,'line':line_no,'raw':line})
    return events

def schedule(es):
    initial=next(e['args'][0] for e in es if e['kind']=='chart')
    bpms=sorted((e['args'][0],e['line'],e['args'][1]) for e in es if e['kind']=='bpm')
    notes=[]
    for e in es:
        k,a=e['kind'],e['args']
        if k not in ('tap','hold','skyarea','flick'): continue
        bpm=initial
        for t,_,b in bpms:
            if t>a[0]: break
            bpm=b
        d=a[3] if k=='hold' else a[9] if k=='skyarea' else F(0)
        step=math.ceil(F(30000)/bpm)
        n=1 if k in ('tap','flick') else max(2,math.ceil(d/step)+1) if k=='hold' else max(1,math.ceil((d-1)/step))
        times=[a[0]] if k in ('tap','flick') else [a[0]+i*step for i in range(n-1)]+[a[0]+d] if k=='hold' else [a[0]+i*step for i in range(n)]
        notes.append({'kind':k,'start':a[0],'end':a[0]+d,'bpm':bpm,'interval':step,'n':n,'times':times,'line':e['line']})
    return notes

def counts(notes,t):
    c=collections.Counter()
    for n in notes:c[n['kind']]+=sum(x<=t for x in n['times'])
    return dict(c)

def actual_counts_formula(notes,t):
    # Translate the random-access counting design separately from the explicit list.
    c=collections.Counter()
    for n in notes:
        if t<n['start']: done=0
        elif n['kind'] in ('tap','flick'): done=1
        elif t>=n['end']: done=n['n']
        else: done=min(n['n']-1 if n['kind']=='hold' else n['n'],1+math.floor((t-n['start'])/n['interval']))
        c[n['kind']]+=done
    return dict(c)

def method(text,name):
    # finite lexical helper for static call-site auditing, not a language parser
    m=re.search(r'\b(?:private|public)\s+(?:static\s+)?[\w<>\[\]]+\s+'+re.escape(name)+r'\s*\([^)]*\)\s*\{',text)
    if not m: raise AssertionError('Cannot locate method '+name)
    start=m.end()-1;depth=0
    # These specific methods have no curly braces embedded in string literals.
    for i in range(start,len(text)):
        if text[i]=='{':depth+=1
        elif text[i]=='}':
            depth-=1
            if depth==0:return text[start:i+1]
    raise AssertionError('Unclosed method '+name)

def run(baseline=None,notes_zip=None):
    reference=json.loads((ASSETS/'Resources/InFalsusStudio/ComboReferences.json').read_text())['references']
    audit=[];summary=[];rng=random.Random(510)
    original_hashes=set()
    if notes_zip:
        with zipfile.ZipFile(notes_zip) as z:original_hashes={hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist() if n.endswith('.spc')}
    for r in reference:
        p=ROOT/'Research/CountSamples'/f"{r['name']}.spc";es=parse(p);ns=schedule(es)
        check(r['name']+' bundled original SHA256',sha(p)==r['sourceSha256'],'source data')
        if notes_zip:check(r['name']+' is byte-identical to a Notes.zip member',sha(p) in original_hashes,'source data')
        sums=collections.Counter()
        for n in ns:sums[n['kind']]+=n['n']
        for k in ('tap','hold','skyarea','flick'):check(r['name']+' candidate total '+k,sums[k]==r[k])
        check(r['name']+' final total',sum(sums.values())==r['total'])
        check(r['name']+' explicit tick list count',all(len(n['times'])==n['n'] for n in ns))
        check(r['name']+' every candidate timestamp inside original duration',all(all(n['start']<=t<=n['end'] for t in n['times']) for n in ns))
        end=max(n['end'] for n in ns)
        points=[F(0),end,end+100]+[F(rng.randrange(0,int(end)+1),1) for _ in range(30)]
        check(r['name']+' random-access matches explicit schedule at 33 seek points',all(counts(ns,t)==actual_counts_formula(ns,t) for t in points))
        sortedpoints=sorted(set(points)); cs=[sum(counts(ns,t).values()) for t in sortedpoints]
        check(r['name']+' monotone candidate accumulated combo',all(a<=b for a,b in zip(cs,cs[1:])))
        tail=counts(ns,end)
        check(r['name']+' final schedule matches per-kind reference',all(tail[k]==r[k] for k in sums))
        bpms=[e for e in es if e['kind'] in ('chart','bpm')];tracks=[e for e in es if e['kind']=='track']
        values={e['args'][0 if e['kind']=='chart' else 1] for e in bpms}
        check(r['name']+' supplied sample has constant numeric BPM',len(values)==1,'source data')
        audit.append({'name':r['name'],'sha256':sha(p),'bpmValues':sorted(float(x) for x in values),'timingStatements':[{'line':e['line'],'text':e['raw']} for e in bpms],
                      'trackStatements':[{'line':e['line'],'text':e['raw']} for e in tracks]})
        checkpoints={str(t):dict(counts(ns,F(t))) for t in (0,1395,22154,22400,22769,23600,24070,25385,36000)}
        summary.append({'name':r['name'],'counts':dict(sums),'total':sum(sums.values()),'checkpointCountsProvisional':checkpoints})
    l=parse(ROOT/'Research/CountSamples/Forbidden_lightsout3.spc');ln=[n for n in schedule(l) if n['kind']=='hold' and n['start']==22154]
    check('LightsOut original holds are 3231ms and BPM130',len(ln)==2 and all(n['end']==25385 and n['bpm']==130 for n in ln),'source data')
    check('LightsOut changes track not the count input, 231ms and 15 each',all(n['interval']==231 and n['n']==15 for n in ln))
    for index,n in enumerate(ln):check('LightsOut hold '+str(index)+' candidate head and tail present',n['times'][0]==22154 and n['times'][-1]==25385)
    fixture=schedule(parse(ROOT/'Fixtures/Count_Bpm_Onset_Hypothesis_051.spc'))
    check('BPM crossing fixture onset assumption 13 then 9',[n['n'] for n in fixture]==[13,9])
    check('BPM crossing fixture onset periods 250 then 125',[n['interval'] for n in fixture]==[250,125])
    # Curve geometry, using the actual analytic midpoint of each easing candidate.
    def mid(a,b,code):return a+(b-a)*(.5 if code==0 else math.sqrt(.5) if code==1 else 1-math.sqrt(.5))
    def pos(delta,code):return (1 if code==1 else -1 if code==2 else 0)*(1 if delta>0 else -1)
    def drag(delta,code,dx):
        u=pos(delta,code)+dx/32
        return 0 if abs(u)<=.38 else 1 if (u>0)==(delta>0) else 2
    for edge in ('left','right'):
        for a,b in ((.2,.8),(.8,.2),(.1,.1001),(.9,.8999)):
            d=b-a
            check(f'{edge}/{a}/{b} left/right drags move visible midpoint same direction',mid(a,b,drag(d,0,-32))<mid(a,b,0)<mid(a,b,drag(d,0,32)))
            check(f'{edge}/{a}/{b} all current codes stable at zero drag',all(drag(d,c,0)==c for c in (0,1,2)))
            check(f'{edge}/{a}/{b} mathematical identity Out-linear=0.20710678*delta',abs(mid(a,b,1)-mid(a,b,0)-(math.sqrt(.5)-.5)*d)<1e-12)
    def stationary(a,b):return abs(b-a)<1e-9
    def pinned(a,b):return stationary(a,b) and (abs(a)<1e-9 or abs(a-1)<1e-9)
    check('wall edge independent from a moving opposite edge',pinned(0,0) and not pinned(.4,.2))
    check('moving ONE endpoint frees a pinned side',not stationary(0,.2) and not pinned(0,.2))
    check('whole translation of equal-X edge no longer wall but still straight',stationary(.3,.3) and not pinned(.3,.3))
    # Static integration contracts, explicitly NOT execution of these methods.
    song=(RUN/'StudioBootstrap.SongProject.cs').read_text();ui=(RUN/'StudioBootstrap.SongHud.cs').read_text();edit=(RUN/'StudioBootstrap.Editing.cs').read_text()
    main=(RUN/'StudioBootstrap.cs').read_text();save=(RUN/'StudioBootstrap.ManualSave.cs').read_text();session=(CORE/'ManualSongSession.cs').read_text()
    stage=method((CORE/'SongMetadataEdit.cs').read_text(),'Stage')
    forbidden=('File.','Directory.','WriteManifest(','.Save(')
    check('metadata Stage contains no file-writing calls',not any(x in stage for x in forbidden),'static wiring')
    activation=method(song,'ActivateSongDifficulty')
    check('difficulty switch stores memory only, no Save or manifest write',not any(x in activation for x in ('SaveSongDifficulty(','.Save(','.WriteManifest(', 'File.Write','WriteRecovery(')),'static wiring')
    check('difficulty switch restores baseline independently of reset helper','savedBytes=projectSession.SavedChartBytes(index)' in activation,'static wiring')
    check('HUD uses Stage, never immediate metadata Apply','SongMetadataEdit.Stage(' in ui and 'SongMetadataEdit.Apply(' not in ui,'static wiring')
    check('explicit project Save invokes ManualSongSession.Save','projectSession.Save()' in method(song,'SaveSongDifficulty'),'static wiring')
    for filename,name in [('StudioBootstrap.Editing.cs','TickEditing'),('StudioBootstrap.cs','OnDestroy'),('StudioBootstrap.Timeline.cs','TickSession')]:
        body=method((RUN/filename).read_text(),name)
        check(name+' contains no automatic save/recovery writes',not any(x in body for x in ('File.Write','WriteRecovery(','SaveSongDifficulty(','SaveWorkingCopy(','WriteManifest(','projectSession.Save(')),'static wiring')
    check('CtrlS remains before text focus early return',edit.index('if(mod&&e.keyCode==KeyCode.S)')<edit.index('if(textFocused)return;'),'static wiring')
    check('project export does not clear project saved baseline','if(songProject==null){workingCopyPath=full;savedBytes=History.Document.ToBytes();documentDirty=false;}' in (RUN/'StudioBootstrap.Timeline.cs').read_text(),'static wiring')
    check('on-leave confirmation contains Save Cancel Discard',all(s in method(song,'LeaveCurrentDocumentSafely') for s in ('"Save", "Cancel", "Discard"','SaveWorkingCopy(false)')),'static wiring')
    check('normal no-save quit has no lifecycle recovery path','WriteRecovery' not in main,'static wiring')
    check('save snapshots capture original bytes on open','Remember(project.Resolve(SongFolderProject.ManifestFile))' in session,'static wiring')
    check('save performs preflight before backup and replacing files',session.index('foreach(var w in writes)Match(w.Path,w.Before);')<session.index('Directory.CreateDirectory(backup)')<session.index('beforeReplaceForTests(i,w.Path)'),'static wiring')
    check('failed save has conditional rollback without overwriting external edits',all(x in session for x in ('Reverse().Where','Same(current,w.After)','refusing rollback over it','Drafts remain unsaved')),'static wiring')
    check('per-edge wall logic is recomputed, no persistent flag','GeometricSkyEase.Describe(dragEvent,right)' in (RUN/'StudioBootstrap.EaseHandles.cs').read_text(),'static wiring')
    check('new count UI includes playhead and per-note report',all(x in save for x in ('At(CurrentMs)','ExportComboReport','Find(selectedId)')),'static wiring')
    check('HUD shows EST COMBO rather than inferred score','EST. COMBO ' in ui and 'comboTimeline.At(CurrentMs)' in ui,'static wiring')
    check('count uses original timestamps not visualContactTime','VisualContactTime' not in (CORE/'ProvisionalComboTimeline.cs').read_text(),'static wiring')
    check('candidate count CSharp tests are actually hooked','Release051SelfTests.Run(check);' in (CORE/'CoreSelfTests.cs').read_text(),'static wiring')
    check('toolbar displays actual new version','UNITY EDITOR 0.5.1' in main,'static wiring')
    metas={}
    for p in sorted((ROOT/'Assets').rglob('*')):
        if p.is_file() and p.suffix!='.meta':check('Unity meta '+str(p.relative_to(ROOT)),Path(str(p)+'.meta').exists(),'asset structure')
        if p.is_file() and p.suffix=='.meta':
            m=re.search(r'^guid: ([0-9a-f]{32})$',p.read_text(),re.M)
            check('valid meta GUID '+str(p.relative_to(ROOT)),bool(m),'asset structure')
            if m:check('unique GUID '+m[1],m[1] not in metas,'asset structure');metas[m[1]]=str(p)
    frozen=[]
    for prefix in ['Runtime/Core/CameraCalibration.cs','Runtime/StudioProfile.cs','Runtime/StudioTransport.cs','Runtime/LeftControlGate.cs','Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Rendering/FlickGeometry.cs','Runtime/Core/AutoplayCursor.cs','Runtime/Rendering/NoteRenderer.cs','Runtime/Rendering/MeshBatch.cs']:
        frozen.append('Assets/InFalsusStudio/'+prefix)
    frozen.extend(str(p.relative_to(ROOT)) for p in ASSETS.rglob('*') if p.is_file() and (p.suffix in ('.shader','.unity','.asset') or p.name=='LightsOut.spc.txt'))
    if baseline:
        for rel in frozen:check('frozen '+rel,(ROOT/rel).read_bytes()==(baseline/rel).read_bytes(),'baseline comparison')
        for p in (baseline/'Assets').rglob('*.meta'):
            rel=p.relative_to(baseline);check('existing meta unchanged '+str(rel),(ROOT/rel).exists() and (ROOT/rel).read_bytes()==p.read_bytes(),'baseline comparison')
    check('no font files in delivered Assets',not any(p.suffix.lower() in ('.ttf','.otf','.woff','.woff2','.ttc') for p in ASSETS.rglob('*')),'asset structure')
    out=ROOT/'Documentation/Reference';out.mkdir(parents=True,exist_ok=True)
    (out/'timing_audit_051.json').write_text(json.dumps({'scope':'Original supplied plaintext charts, line-numbered; scroll changes are not BPM changes.','charts':audit},ensure_ascii=False,indent=2))
    data={'release':'0.5.1','scope':'Independent Python Fraction arithmetic + original-data + finite static/resource checks. NO C# or Unity execution.',
          'passed':len(checks),'categories':dict(collections.Counter(c['category'] for c in checks)),
          'csharp_compiled':False,'unity_run':False,'referenceCounts':summary,'frozenFiles':frozen,'checks':checks}
    (out/'v051_verification.json').write_text(json.dumps(data,ensure_ascii=False,indent=2))
    print(json.dumps({k:data[k] for k in ('release','passed','categories','csharp_compiled','unity_run')},indent=2))
if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--baseline',type=Path);ap.add_argument('--notes',type=Path)
    a=ap.parse_args();run(a.baseline,a.notes)
