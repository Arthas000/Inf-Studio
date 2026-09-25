#!/usr/bin/env python3
"""v0.2 math/structure audits. These DO NOT execute the C# implementation.
Optional --baseline-zip verifies camera, stage and shaders byte-for-byte vs 0.1.1.
"""
import argparse, hashlib, json, math, random, re, zipfile
from pathlib import Path
from verify_reference import ROOT, ASSETS, sky, ease, close, Scroll
results=[]
def check(name,passed):
    results.append({'name':name,'passed':bool(passed)})
    if not passed: raise AssertionError(name)
def minimum(a):
    l0,r0=sky(a,a[0]);l1,r1=sky(a,a[0]+a[9]);A=B=C=0.;k=math.pi/2
    for code,delta in ((int(a[8]),r1-r0),(int(a[7]),l0-l1)):
        if code==0:A+=delta
        elif code==1:C+=k*delta
        else:B+=k*delta
    candidates=[0.,1.];rad=math.hypot(B,C)
    if rad>=1e-14 and abs(A)<=rad+1e-12:
        phi=math.atan2(C,B);alpha=math.asin(max(-1,min(1,-A/rad)))
        for n in range(-2,3):
            for theta in (alpha-phi+2*math.pi*n,math.pi-alpha-phi+2*math.pi*n):
                if 0<theta<k:candidates.append(theta/k)
    pairs=[]
    for u in candidates:
        l,r=sky(a,a[0]+a[9]*u);pairs.append((r-l,u))
    return min(pairs)
def run(baseline=None):
    rng=random.Random(20260921)
    for le in range(3):
      for re_ in range(3):
        mirror_ok=minimum_ok=True
        for _ in range(12):
            a=[1000,rng.uniform(0,24),24,rng.uniform(.2,12),rng.uniform(0,32),32,rng.uniform(.2,16),le,re_,2000]
            mirrored=a.copy();mirrored[1]=a[2]-a[1];mirrored[4]=a[5]-a[4];mirrored[7],mirrored[8]=a[8],a[7]
            dense=[]
            for i in range(2001):
                t=1000+i;l,r=sky(a,t);ml,mr=sky(mirrored,t)
                mirror_ok &= close(ml,1-r) and close(mr,1-l)
                dense.append(r-l)
            exact,u=minimum(a)
            minimum_ok &= exact<=min(dense)+1e-9 and min(dense)-exact<1e-5
        check(f'Mirror both boundaries + swap eases {le}/{re_}',mirror_ok)
        check(f'Continuous width minimum vs dense samples {le}/{re_}',minimum_ok)
    a=[1000,6,24,2.4,18,24,2.4,1,2,2000]
    check('Interior negative width despite positive endpoints',minimum(a)[0]<0 and sky(a,1000)[1]-sky(a,1000)[0]>0)
    # A translated chain retains exact end/start agreement with different divisors.
    first=[1846,100,200,2,1,2,2,1,1,923,0]
    second=[2769,1,2,2,100,200,2,2,2,923,0]
    for a in (first,second):a[0]+=125;a[1]+=.125*a[2];a[4]+=.125*a[5]
    check('Mixed-divisor translation retains shared chain boundary',all(close(a,b) for a,b in zip(sky(first,2894),sky(second,2894))))
    for preferred,expected in ((480,500),(1510,1500),(2510,2500)):
        candidates=Scroll([(1000,-1),(2000,1)]).inverse(500,0,4000)
        chosen=min(candidates,key=lambda p:abs(p[0]-preferred))[0]
        check('Inverse preserves selected temporal branch '+str(preferred),chosen==expected)
    for p in ASSETS.rglob('*'):
        if p.is_file() and p.suffix!='.meta':check('Every asset has metadata '+p.relative_to(ASSETS).as_posix(),Path(str(p)+'.meta').is_file())
    source='\n'.join(p.read_text() for p in ASSETS.rglob('*.cs'))
    check('No unintended external networking import',all('#if INFALSUS_USE_UNITY_WEBREQUEST_AUDIO\nusing UnityEngine.Networking;' in p.read_text() for p in ASSETS.rglob('*.cs') if 'using UnityEngine.Networking;' in p.read_text()))
    check('Editor tests hooked into main self-test', 'EditingSelfTests.Run(check)' in (ASSETS/'Runtime/Core/CoreSelfTests.cs').read_text())
    check('No value-based height dependency in Flick source','ScreenHeight(p,camera,y,z)' in (ASSETS/'Runtime/Rendering/FlickGeometry.cs').read_text() and 'flickScreenHeightRatio' not in (ASSETS/'Runtime/Rendering/FlickGeometry.cs').read_text())
    check('Preview clones pre-gesture snapshot','var next = transactionStart.Clone();' in (ASSETS/'Runtime/Core/SpcDocument.cs').read_text())
    check('Source IDs are stored on lines','SourceId = lines[i].Id' in source)
    check('No full project settings added',not (ROOT/'Packages').exists() and not (ROOT/'ProjectSettings').exists())
    check('At least six original synthetic editing fixtures',len(list((ROOT/'Fixtures').glob('*.spc')))>=6)
    baseline_hashes={}
    if baseline:
        unchanged=['Runtime/Rendering/StageSpace.cs','Runtime/Rendering/StageRenderer.cs','Runtime/Core/CameraCalibration.cs','Scenes/InFalsusStudio_Demo.unity','Resources/InFalsusStudio/LightsOut.spc.txt']
        unchanged += ['Resources/InFalsusStudio/'+name for name in ('VertexOpaque.shader','VertexTransparent.shader','VertexGlow.shader')]
        with zipfile.ZipFile(baseline) as archive:
            for rel in unchanged:
                suffix='Assets/InFalsusStudio/'+rel
                name=next(n for n in archive.namelist() if n.endswith('/'+suffix))
                old=archive.read(name);new=(ASSETS/rel).read_bytes()
                check('Baseline unchanged '+rel,old==new);baseline_hashes[rel]=hashlib.sha256(new).hexdigest()
            for p in ASSETS.rglob('*.meta'):
                suffix=p.relative_to(ROOT).as_posix();matches=[n for n in archive.namelist() if n.endswith('/'+suffix)]
                if matches:check('Existing metadata unchanged '+suffix,archive.read(matches[0])==p.read_bytes())
    report={'scope':'Independent mathematical and static-structure audits, not C# or Unity execution','passed':len(results),'failed':0,'baseline_compared':bool(baseline),'baseline_sha256':baseline_hashes,'results':results}
    (ROOT/'Documentation/Reference/editing_contracts_check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(f'{len(results)} v0.2 mathematical/structure audits passed. C# and Unity NOT executed.')
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--baseline-zip');run(parser.parse_args().baseline_zip)
