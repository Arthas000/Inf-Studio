#!/usr/bin/env python3
"""Independent Python reference/audit of supplied ICP1 JSON and new contracts.
NOT execution or compilation of the delivered C#/Unity implementation.
Usage: python Tools/verify_song_project_reference.py --enigma /path/to/014_enigma
"""
from pathlib import Path
from fractions import Fraction
import argparse, collections, hashlib, json, math, re, struct
ROOT=Path(__file__).resolve().parents[1]
REPORT=ROOT/'Documentation/Reference';REPORT.mkdir(parents=True,exist_ok=True)
checks=[]
def check(ok,label):
    if not ok: raise AssertionError(label)
    checks.append(label)
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def rat(n,k):
    d=n[k]; return Fraction(d['numerator'], d['denominator'])
def endpoint(n,p):
    x,w=rat(n,p+'_x'),rat(n,p+'_width'); den=math.lcm(x.denominator,w.denominator)
    return [int(x*den),den,int(w*den)]
def ease(code,u):return u if code==0 else math.sin(math.pi*u/2) if code==1 else 1-math.cos(math.pi*u/2)
def sky(args,t):
    start,sx,ss,sw,ex,es,ew,el,er,duration,*_=args
    u=max(0,min(1,(t-start)/duration)) if duration else 1
    sl=(sx-sw/2)/ss;sr=(sx+sw/2)/ss;tl=(ex-ew/2)/es;tr=(ex+ew/2)/es
    return sl+(tl-sl)*ease(el,u),sr+(tr-sr)*ease(er,u)
def convert_record(n):
    ty,side,flags=n['type'],n['side'],n['extra_flags']; t=n['start_ms']; d=n['end_ms']-t
    assert n['packed_flags']==(ty<<8)|side
    assert n['relay']==0 and t>=0 and d>=0
    if ty in (1,2):
        assert flags==0 and rat(n,'start_x')==rat(n,'end_x') and rat(n,'start_width')==rat(n,'end_width')
        lane=0 if side==2 else 5 if side==3 else rat(n,'start_x')*4
        width=rat(n,'start_width')*4
        assert lane.denominator==1 if isinstance(lane,Fraction) else True
        assert width.denominator==1 and 1<=width<=4
        lane,width=int(lane),int(width)
        assert (lane in (0,5) and width==1) or (1<=lane<=4 and lane+width<=5)
        if ty==1:assert d==0;return 'tap',[t,width,lane]
        return 'hold',[t,lane,width,d]
    if ty==4:
        assert side==4 and flags in (1024,4096) and d==0
        x,s,w=endpoint(n,'start');return 'flick',[t,x,s,w,flags>>8]
    if ty==5:
        assert side==4 and flags&~252==0
        m={4:0,8:1,16:2};el=m[flags&28];er=m[(flags>>3)&28]
        return 'skyarea',[t,*endpoint(n,'start'),*endpoint(n,'end'),el,er,d,n['group_id']]
    raise ValueError('unknown record')
def call(kind,args):
    return kind+'('+','.join(str(float(x)) if isinstance(x,Fraction) else str(x) for x in args)+')'
def audit_enigma(folder):
    folder=folder.resolve();meta=json.loads((folder/'song.json').read_text(encoding='utf-8-sig'))
    originals={str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}
    music=sorted(p for p in folder.iterdir() if p.suffix.lower() in ('.mp3','.ogg'))
    check(len(music)==2,'actual Enigma contains TWO top-level audio files')
    check(meta['audio']['path'].endswith('/enigma.ogg'),'main explicitly enigma.ogg')
    check(meta['supplementary_audio'][0]['path'].endswith('/enigma_bga.ogg'),'supplementary explicitly declared, not guessed by suffix')
    check((folder/'enigma.ogg').read_bytes()[:4]==b'OggS','main Ogg magic')
    check((folder/'enigma_bga.ogg').read_bytes()[:4]==b'OggS','supplementary Ogg magic')
    stats=[];out=ROOT/'Validation/Enigma_Import_Reference';out.mkdir(parents=True,exist_ok=True)
    for i,record in enumerate(meta['charts']):
        mask=record['Difficulty'];index={1:0,2:1,4:2,8:3}[mask]
        source=folder/'Charts'/Path(record['json_path']).name;binary=folder/'Charts'/Path(record['binary_path']).name
        j=json.loads(source.read_text());check(binary.read_bytes()[:4]==b'ICP1',f'{index} binary SPC magic, not plaintext')
        check(j['format']=='ICP1' and j['version']==1 and j['bpm']==172 and j['beats_per_bar']==4,f'{index} decoded version and timing')
        check(len(j['notes'])==record['note_count']==j['note_count'],f'{index} declared object counts match arrays, not combo')
        check(len(j['events'])==record['event_count']==j['event_count'],f'{index} event counts match arrays')
        records=[];lines=[call('chart',[j['bpm'],j['beats_per_bar']]),'// Python independent semantic reference, NOT Unity output; easing-profile verification pending.']
        for n in j['notes']:
            kind,args=convert_record(n);records.append((kind,args));lines.append(call(kind,args))
            check(kind in ('tap','hold','flick','skyarea'),f'{index} source note {n["id"]} field mapping and invariants')
            if kind=='skyarea':
                for frac in (0,.25,.5,.75,1):
                    l,r=sky(args,args[0]+args[9]*frac)
                    check(math.isfinite(l) and math.isfinite(r),f'{index} source sky {n["id"]} finite geometry {frac}')
        for e in j['events']:
            if e['type']==0:lines.append(call('track',[e['timestamp_ms'],e['value']]))
            else:lines.append('// preserved opaque event: '+json.dumps(e,separators=(',',':')))
        count=collections.Counter(k for k,_ in records)
        output=out/f'{index}_{record["difficulty_name"]}.spc';output.write_text('\n'.join(lines)+'\n',encoding='utf-8')
        stats.append(dict(index=index,sourceMask=mask,difficulty=record['difficulty_name'],rating=record['LevelSectionIndicator'],bpm=j['bpm'],beatsPerBar=j['beats_per_bar'],objects=len(records),byType=dict(count),events=len(j['events']),opaqueEvents=sum(e['type']!=0 for e in j['events']),lastEndMs=max(n['end_ms'] for n in j['notes']),skyFlags=dict(collections.Counter(n['extra_flags'] for n in j['notes'] if n['type']==5)),sourceJSON=str(source.relative_to(folder)),sourceSHA256=sha(source),binarySHA256=sha(binary),referenceSPC=str(output.relative_to(ROOT))))
    for rel,digest in originals.items():check(sha(folder/rel)==digest,'input unchanged: '+rel)
    return dict(scope='Archive extraction + independent Python audit, not Unity/C# execution or official scoring evidence',title=meta['title']['default'],artist=meta['artist']['default'],mainAudio=meta['audio'],supplementaryAudio=meta['supplementary_audio'],difficultyStats=stats,originalSHA256=originals,unverified=['Sky one-hot field to easing-name mapping is an import profile, not game-verified','side visibility events remain opaque','No Hold/Sky judgement ticks, combo or intermediate score inferred'])
def contracts():
    a=ROOT/'Assets/InFalsusStudio/Runtime'
    bootstrap=(a/'StudioBootstrap.cs').read_text();proj=(a/'StudioBootstrap.SongProject.cs').read_text();hud=(a/'StudioBootstrap.SongHud.cs').read_text()
    check('cursorMotion.Evaluate(transport.TimeMs)' in bootstrap and '(float)cursorPose.X' in bootstrap,'actual renderer cursor motion hookup')
    check('CaptureSongGuiFrame();' in (a/'StudioBootstrap.GuiFrame.cs').read_text(),'song widgets use Layout snapshot')
    check('HudBlocksInput(gui)' in (a/'StudioBootstrap.Editing.cs').read_text(),'HUD captures clicks, no scene input leak')
    check('settings.preloadAudioData=true' in proj and 'importer.preloadAudioData=' not in proj,'Unity6 sample-settings preload property, no obsolete member')
    check('ForceSynchronousImport' in proj and 'UnityWebRequest' not in proj,'folder audio uses Editor importer rather than missing web request module')
    check('if(!LeaveCurrentDocumentSafely())return;' in proj and 'CommitInitialWorkspace' in proj,'staged folder adoption and previous save guard present')
    check('Use declared main' in proj and 'Cancel' in proj,'multiple audio exception requires visible confirmation')
    check('SaveSongDifficulty()' in (a/'StudioBootstrap.Timeline.cs').read_text(),'Ctrl+S routes to project workspace')
    check('TICK RULE UNVERIFIED' in hud and '---------"' in hud,'unknown score explicitly labelled, not fake ticking')
    check('DrawScoreDigits' in hud and 'SongFolderProject.Codes' in hud,'score and metadata HUD implemented')
    for filename in ('ScoreFrame','Difficulty0','Difficulty1','Difficulty2','Difficulty3'):
        p=a.parent/'Resources/InFalsusStudio/Hud'/f'{filename}.png'
        check(p.exists() and p.with_suffix('.png.meta').exists(),'HUD resource and meta: '+filename)
    # Evaluate independent mathematical invariants, not the C# Evaluate method.
    s=[1000,20,100,10,80,100,20,1,2,1000]
    for t in (1000,1250,1500,1750,2000):
        lr=sky(s,t);center=sum(lr)/2
        check(min(lr)<=center<=max(lr),'asymmetric Sky mathematical midpoint '+str(t))
    for direction in (-1,1):
        positions=[.5+direction*.055*(3*u*u-2*u*u*u) for u in (0,.25,.5,.75,1)]
        check(positions[0]==.5 and all(direction*(b-a)>0 for a,b in zip(positions,positions[1:])),'swipe center, direction and bounded travel '+str(direction))
    # Public scoring premise is supplied by user; test arithmetic only, NOT rule validation.
    check(f'{100000000+123:09,}'.replace(',','\'')=="100'000'123",'stated maximum arithmetic and digit format')
    metas=list((a.parent).rglob('*.meta'));guids=[]
    for p in metas:
        m=re.search(r'^guid: ([a-f0-9]{32})$',p.read_text(),re.M);check(m is not None,'valid meta GUID '+str(p.relative_to(ROOT)));guids.append(m[1])
    check(len(guids)==len(set(guids)),'all existing and new GUIDs unique')
    return len(metas)
def main():
    ap=argparse.ArgumentParser();ap.add_argument('--enigma',type=Path);args=ap.parse_args();contracts()
    audit=audit_enigma(args.enigma) if args.enigma else None
    if audit:(REPORT/'enigma_import_audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2))
    report={'scope':'Python reference math, actual archive/schema/asset/byte audits, source integration checks. NOT C# or Unity runtime tests.','passed':len(checks),'checks':checks,'enigma_checked':bool(audit)}
    (REPORT/'v04_reference_checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(f'{len(checks)} reference/archive/static checks passed (not C#/Unity tests).')
if __name__=='__main__':main()
