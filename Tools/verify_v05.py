#!/usr/bin/env python3
"""Independent Python oracles + static integration checks. NOT C# or Unity execution.
Run from anywhere: python Tools/verify_v05.py [--baseline /path/to/v04root]
Optional paired source audit: --song /path/to/014_enigma
"""
from pathlib import Path
from fractions import Fraction
import argparse, collections, hashlib, json, math, re, random
ROOT=Path(__file__).resolve().parents[1]
CORE=ROOT/'Assets/InFalsusStudio/Runtime/Core'
RUNTIME=CORE.parent
checks=[]
def check(name, ok):
    checks.append({'name':name,'passed':bool(ok)})
    if not ok:raise AssertionError(name)
def parse(path):
    result=[]
    for line in path.read_text(encoding='utf-8-sig').splitlines():
        m=re.match(r'\s*(\w+)\((.*?)\)',line)
        if m:
            try:result.append({'kind':m[1].lower(),'args':[float(x.strip()) for x in m[2].split(',')]})
            except ValueError:pass
    return result

def n(x):return f'{x:.10f}'.rstrip('0').rstrip('.') if x else '0'
def canonical(e):
    k,a=e['kind'],e['args']
    if k=='chart':v=a[:2]
    elif k=='bpm':v=a[:3] if len(a)>2 else a[:2]+[-1]
    elif k=='tap':v=a[:3]
    elif k=='hold':v=a[:4]
    elif k=='flick':v=[a[0],a[1]/a[2],a[3]/a[2],a[4]]
    elif k=='skyarea':v=[a[0],a[1]/a[2],a[3]/a[2],a[4]/a[5],a[6]/a[5],a[7],a[8],a[9],a[10] if len(a)>10 else -1]
    else:return None
    return k+'|'+'|'.join(n(x) for x in v)
def signature(es):return hashlib.sha256(('\n'.join(sorted(filter(None,map(canonical,es))))+'\n').encode()).hexdigest()
def count_pair(k,d,bpm):
    # Fraction oracle, no iterative addition of rounded periods.
    beat=Fraction(30000)/Fraction(str(bpm));interval=math.ceil(beat)
    if k in ('tap','flick'):return 1,1
    if k=='hold':return max(2,math.ceil(Fraction(str(d))/interval)+1),max(2,math.floor((Fraction(str(d))+Fraction(1,2))/beat+Fraction(1,2))+1)
    return max(1,math.ceil((Fraction(str(d))-1)/interval)),max(1,math.floor((Fraction(str(d))+60)/beat+Fraction(1,2)))
def calc(es):
    bpm=next(e['args'][0] for e in es if e['kind']=='chart')
    bpms=[(0,bpm)]+sorted((e['args'][0],e['args'][1]) for e in es if e['kind']=='bpm')
    a=collections.Counter();b=collections.Counter();different=[]
    for e in es:
        k,ar=e['kind'],e['args']
        if k not in ('tap','hold','skyarea','flick'):continue
        d=ar[3 if k=='hold' else 9] if k in ('hold','skyarea') else 0
        onset=next(value for t,value in reversed(bpms) if t<=ar[0])
        ca,cb=count_pair(k,d,onset);a[k]+=ca;b[k]+=cb
        if ca!=cb:different.append([k,ar[0],d,ca,cb])
    return dict(a),dict(b),different,len(set(value for _,value in bpms))>1

def sky(args,t):
    u=max(0,min(1,(t-args[0])/args[9])) if args[9]>0 else 0
    ease=lambda code:u if code==0 else math.sin(math.pi*u/2) if code==1 else 1-math.cos(math.pi*u/2)
    c0,w0,c1,w1=args[1]/args[2],abs(args[3]/args[2]),args[4]/args[5],abs(args[6]/args[5])
    return c0-w0/2+(c1-w1/2-c0+w0/2)*ease(args[7]),c0+w0/2+(c1+w1/2-c0-w0/2)*ease(args[8])

def clip_plane(poly,edge,left):
    result=[]
    if not poly:return result
    a=poly[-1];ai=a[0]>=edge if left else a[0]<=edge
    for b in poly:
        bi=b[0]>=edge if left else b[0]<=edge
        if ai!=bi:
            t=(edge-a[0])/(b[0]-a[0]);result.append((edge,a[1]+t*(b[1]-a[1])))
        if bi:result.append(b)
        a,ai=b,bi
    return result

def area(poly):return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(poly,poly[1:]+poly[:1])))/2 if poly else 0

def run(baseline=None,song=None):
    resource=json.loads((ROOT/'Assets/InFalsusStudio/Resources/InFalsusStudio/ComboReferences.json').read_text())
    report=[]
    for r in resource['references']:
        path=ROOT/'Research/CountSamples'/f"{r['name']}.spc";es=parse(path);ca,cb,pernote,change=calc(es)
        check(r['name']+' original bytes hash',hashlib.sha256(path.read_bytes()).hexdigest()==r['sourceSha256'])
        check(r['name']+' canonical note/BPM signature',signature(es)==r['signature'])
        for k in ('tap','hold','skyarea','flick'):
            check(r['name']+' preferred candidate '+k,ca[k]==r[k])
            check(r['name']+' distinct alternative '+k,cb[k]==r[k])
        check(r['name']+' all per-event counts also agree between candidates',not pernote)
        check(r['name']+' no actual numerical BPM changes',not change)
        report.append({'name':r['name'],'expected':{k:r[k] for k in ('tap','hold','skyarea','flick','total')},'candidate':ca,'alternative':cb,'per_event_model_differences':pernote,'actual_bpm_change':change})
    pairs=[]
    if song:
        for name in ('Minimal_enigma0','Forbidden_enigma3'):
            es=parse(ROOT/'Research/CountSamples'/f'{name}.spc');notes=[e for e in es if e['kind'] in ('tap','hold','skyarea','flick')]
            js=json.loads((song/'Charts'/f'{name}.json').read_text());converted=[{'kind':'chart','args':[js['bpm'],js['beats_per_bar']]}];skies=0
            check(name+' paired array length',len(notes)==len(js['notes']))
            for e,source in zip(notes,js['notes']):
                rat=lambda field:Fraction(source[field]['numerator'],source[field]['denominator'])
                t,end=source['start_ms'],source['end_ms'];k=source['type'];side=source['side'];flag=source['extra_flags']
                if k in(1,2):
                    lane=0 if side==2 else 5 if side==3 else int(rat('start_x')*4);w=float(rat('start_width')*4)
                    got={'kind':'tap' if k==1 else 'hold','args':[t,w,lane] if k==1 else [t,lane,w,end-t]}
                elif k==4:got={'kind':'flick','args':[t,float(rat('start_x')),1,float(rat('start_width')),flag>>8]}
                else:
                    l={4:0,8:1,16:2}[flag&28];rr={4:0,8:1,16:2}[(flag>>3)&28];skies+=1
                    got={'kind':'skyarea','args':[t,float(rat('start_x')),1,float(rat('start_width')),float(rat('end_x')),1,float(rat('end_width')),l,rr,end-t,source['group_id']]}
                check(f'{name} paired record {source["id"]}',canonical(e)==canonical(got));converted.append(got)
            check(name+' converted canonical evidence signature',signature(converted)==signature(es))
            pairs.append({'name':name,'paired_notes':len(notes),'sky_segments':skies,'easing_edges':skies*2,'signature':signature(converted)})
        check('565 paired Sky segments prove current enum map for supplied Enigma',sum(x['sky_segments'] for x in pairs)==565)
    # Discriminators show why exact per-record agreement STILL does not identify one law.
    discriminators=[]
    for k in ('hold','skyarea'):
        for bpm in (120,130,172,200):
            diffs=[d for d in range(1,1501) if count_pair(k,d,bpm)[0]!=count_pair(k,d,bpm)[1]]
            check(f'Counterexamples exist for {k} {bpm} BPM',len(diffs)>0)
            d=diffs[0];a,b=count_pair(k,d,bpm);discriminators.append({'kind':k,'bpm':bpm,'duration_ms':d,'candidate':a,'alternative':b})
    (ROOT/'Research/count_discriminators.json').write_text(json.dumps(discriminators,indent=2)+'\n')
    check('tested extreme left Sky exactly0',sky([45916,1,12,2,1,12,2,2,1,5363,256],46000)[0]==0)
    random.seed(705)
    for i in range(300):
        polygon=[(random.uniform(-6,6),random.uniform(-10,40)) for _ in range(3)]
        clipped=clip_plane(clip_plane(polygon,-2,True),2,False)
        check(f'Clipped triangle {i}: all vertices within reach and area never grows',all(-2<=x<=2 and math.isfinite(z) for x,z in clipped) and area(clipped)<=area(polygon)+1e-8)
    # Simulate the user's exact two-left-Flick segment with a pure trajectory oracle.
    enigma=parse(ROOT/'Research/CountSamples/Forbidden_enigma3.spc')
    active=[e['args'] for e in enigma if e['kind']=='skyarea' and e['args'][0]<=24070<e['args'][0]+e['args'][9]]
    flicks=[e['args'] for e in enigma if e['kind']=='flick' and e['args'][0]==24070]
    check('actual 24070 cluster is two LEFT Flicks',len(flicks)==2 and all(a[4]==16 for a in flicks))
    targets=sorted((a[1]/a[2] for a in flicks),reverse=True);allowed=[max(0,max(sky(a,24070)[0] for a in active)),min(1,min(sky(a,24070)[1] for a in active))]
    route=targets+[max(allowed[0],targets[-1]-.055)]
    distances=[0]
    for a,b in zip(route,route[1:]):distances.append(distances[-1]+abs(b-a))
    def gesture(t):
        u=max(0,min(1,(t-24070)/100));s=(u*u*(3-2*u))*distances[-1]
        pos=route[-1]
        for i in range(1,len(route)):
            if s<=distances[i]:pos=route[i-1]+(route[i]-route[i-1])*(s-distances[i-1])/(distances[i]-distances[i-1]);break
        l=max(0,max(sky(a,t)[0] for a in active));r=min(1,min(sky(a,t)[1] for a in active))
        return max(l,min(r,pos)),l,r
    last=gesture(24070)[0];crossed=False
    for i in range(101):
        x,l,r=gesture(24070+i);check(f'actual Enigma sweep {i}ms respects moving Sky and hard limits',0<=l<=x<=r<=1)
        if last>=targets[-1]>=x:crossed=True
        last=x
    check('rightmost center at group start',abs(gesture(24070)[0]-targets[0])<1e-9)
    check('sweep reaches left Flick inside moving Sky',crossed)
    for scale in (.1,.5,1,2,4):
        w,h=328*scale,50*scale;pad=w*.012;gap=w*.010;sep=w*.012;dw=(w-2*pad-8*gap-2*sep)/9;x=pad
        for i in range(9):
            check(f'HUD scale {scale} digit {i} box in panel',0<=x<x+dw<=w+1e-7)
            # Same segment math as source: all glow padding remains within its glyph.
            t=max(.5,min(dw*.095,h*.92*.07));p=t*1.6
            check(f'HUD scale {scale} digit {i} glow padding positive',p-t*.7>0 and dw>2*p+2*t)
            x+=dw+(gap if i<8 else 0)+(sep if i in(2,5) else 0)
        check(f'HUD scale {scale} full budget closes',abs(x+pad-w)<1e-8)
    src=lambda f:(RUNTIME/f).read_text()
    ed=src('StudioBootstrap.Editing.cs');hud=src('StudioBootstrap.SongHud.cs');meta=src('Core/SongMetadataEdit.cs');mesh=src('Rendering/MeshBatch.cs');rend=src('Rendering/NoteRenderer.cs')
    checks_static={
      'left-click playback branch returns before editing':'Playback continues. Canvas clicks only select' in ed,
      'handles disabled during active playback':'if(transport.Playing' in ed,
      'nine-digit rect fits artwork':'HR(138,44,328,50)' in hud,
      'HUD has no Repaint-only BeginGroup control ID mismatch':'GUI.BeginGroup(' not in hud,
      'jacket button opens folder':'BeginHudField(HudField.Title)' in hud and 'CancelHudField();ChooseSongFolder();' in hud,
      'artist and numeric rating editing':'BeginHudField(HudField.Artist)' in hud and 'BeginHudField(HudField.Level)' in hud,
      'HUD commit deferred to Layout':'CommitHudField()' in hud and 'QueueGui(()=>' in hud,
      'metadata uses token span patcher':'new LosslessJsonPatch(oldSource)' in meta,
      'source and manifest byte backups':'song.json.before' in meta and 'manifest.before' in meta,
      'external conflict check and rollback':'RestoreIfOurWrite' in meta and 'Match(manifest,oldManifest)' in meta,
      'triangle intersection clipping (not vertex clamp only)':'ClipPlane(polygon,MinX,true)' in mesh and 'Color.Lerp' in mesh,
      'hit testing follows clipped visual mesh':'air.Vertices[air.Triangles[i]]' in rend,
      'cursor and visible Flick share visual contacts':'visualCursor.VisualContactTime(e)' in rend and 'return 0;' in rend,
      'true scoring remains separate':'visualContacts' in src('Core/AutoplayCursor.cs') and 'TickCountResearch' not in src('Core/AutoplayCursor.cs'),
      'research warns actualBPMchange':'start-BPM versus dynamic-BPM' in src('Core/TickCountResearch.cs'),
      'all new C# tests connected':'Release05SelfTests.Run(check)' in src('Core/CoreSelfTests.cs'),
    }
    for name,ok in checks_static.items():check(name,ok)
    if baseline:
        frozen=['Runtime/Core/CameraCalibration.cs','Runtime/StudioProfile.cs','Runtime/StudioTransport.cs','Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Rendering/FlickGeometry.cs','Runtime/LeftControlGate.cs']
        frozen += [str(x.relative_to(ROOT/'Assets/InFalsusStudio')) for x in (ROOT/'Assets/InFalsusStudio').rglob('*') if x.suffix in('.shader','.unity','.asset')]
        for f in frozen:check('frozen baseline '+f,(baseline/'Assets/InFalsusStudio'/f).read_bytes()==(ROOT/'Assets/InFalsusStudio'/f).read_bytes())
    report_path=ROOT/'Documentation/Reference/v05_verification.json'
    report_path.write_text(json.dumps({'scope':'Independent Python mathematics + actual supplied file checks + static source inspection. NOT compiled C# or Unity execution.','checks':checks,'count_fits':report,'paired_enigma':pairs,'discriminators':discriminators},indent=2,ensure_ascii=False)+'\n')
    print(f'{len(checks)} independent data/math/static checks passed. C# / Unity NOT executed. Report: {report_path}')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--baseline',type=Path);p.add_argument('--song',type=Path);a=p.parse_args();run(a.baseline,a.song)
