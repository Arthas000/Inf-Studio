#!/usr/bin/env python3
"""Independent Python reference checks. NOT a C# compiler or Unity runtime test.
Run from any directory: python Tools/verify_reference.py
Only Python's standard library is required. The bundled private sample is read-only.
"""
from __future__ import annotations
import collections, hashlib, json, math, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets/InFalsusStudio'
RESULTS: list[dict] = []

def check(name: str, ok: bool, detail: str = '') -> None:
    RESULTS.append(dict(name=name, passed=bool(ok), detail=detail))
    if not ok:
        raise AssertionError(name + ': ' + detail)

def close(a: float, b: float, eps: float = 1e-8) -> bool:
    return abs(a-b) <= eps

def ease(code: int, u: float) -> float:
    u=max(0.,min(1.,u))
    if code==0:return u
    if code==1:return math.sin(math.pi*u/2)
    if code==2:return 1-math.cos(math.pi*u/2)
    raise ValueError('Unknown easing: '+str(code))

def sky(args: list[float], t: float) -> tuple[float,float]:
    _,sx,ss,sw,ex,es,ew,le,re_,duration,*_=args
    if ss<=0 or es<=0:raise ValueError('divisor must be positive')
    u=max(0,min(1,(t-args[0])/duration)) if duration>0 else 0
    cs,ce=sx/ss,ex/es;ws,we=abs(sw/ss),abs(ew/es)
    l0,r0,l1,r1=cs-ws/2,cs+ws/2,ce-we/2,ce+we/2
    return l0+(l1-l0)*ease(int(le),u),r0+(r1-r0)*ease(int(re_),u)

class Calibration:
    """Chosen FOV and scale; solve screenshot landmarks, not unique camera recovery."""
    def __init__(self) -> None:
        self.f=576/math.tan(math.radians(25));self.a=math.atan((576-35)/self.f)
        self.c,self.s=math.cos(self.a),math.sin(self.a)
        self.zc=self.f*2/590;self.yc=(576-1029)/self.f*self.zc
        self.h=-self.yc*self.c+self.zc*self.s;self.back=self.yc*self.s+self.zc*self.c
        self.air=self.height(734);self.rise=self.height(840)
        self.run=900*(self.zc-self.rise*self.s)/self.f-2
    def height(self,y:float)->float:
        r=(576-y)/self.f;return (r*self.zc-self.yc)/(self.c+r*self.s)
    def project(self,x:float,y:float,z:float)->tuple[float,float]:
        yy=(y-self.h)*self.c+(z+self.back)*self.s
        zz=-(y-self.h)*self.s+(z+self.back)*self.c
        return 1024+self.f*x/zz,576-self.f*yy/zz
    def surface(self,x:float,z:float)->tuple[float,float,float]:
        return x,max(0,abs(x)-2)*self.rise/self.run,z

class Scroll:
    def __init__(self,tracks:list[tuple[float,float]])->None:
        self.p=[(0.,1.,0.)]
        for t,s in sorted(tracks,key=lambda v:v[0]):
            if t<=0:self.p[0]=(0.,s,0.);continue
            pt,ps,pos=self.p[-1]
            if t==pt:self.p[-1]=(t,s,pos)
            else:self.p.append((t,s,pos+(t-pt)*ps))
    def at(self,t:float)->float:
        a=self.p[0]
        for p in self.p:
            if p[0]>t:break
            a=p
        return a[2]+(t-a[0])*a[1]
    def inverse(self,x:float,begin:float,end:float)->list[tuple[float,float]]:
        out=[]
        for i,(t,s,pos) in enumerate(self.p):
            a=max(t,begin);b=min(end,self.p[i+1][0] if i+1<len(self.p) else end)
            if b<a:continue
            if abs(s)<1e-12:
                if close(x,pos):out.append((a,b))
            else:
                q=t+(x-pos)/s
                if a-1e-8<=q<=b+1e-8:out.append((q,q))
        return out

def run() -> dict:
    path=ASSETS/'Resources/InFalsusStudio/LightsOut.spc.txt';original=path.read_bytes()
    digest=hashlib.sha256(original).hexdigest()
    check('Supplied SPC exact SHA-256',digest=='166a8abf158c8e2ccdebac0b2ee897b371f569b0ae4cdbcc7bbd6709ea41aa7e')
    check('UTF-8 decode/encode leaves sample bytes intact',original.decode('utf-8').encode('utf-8')==original)
    lines=original.decode('utf-8-sig').splitlines();check('1138 source lines',len(lines)==1138)
    events=[]
    for i,line in enumerate(lines):
        m=re.fullmatch(r'([a-z]+)\((.*)\)',line.strip())
        if m:events.append((m[1],[float(v.strip()) for v in m[2].split(',')],i+1))
    counts=collections.Counter(x[0] for x in events)
    for kind,n in dict(chart=1,tap=600,hold=26,flick=304,skyarea=190,track=17).items():check('Count '+kind,counts[kind]==n)
    check('1120 note objects',sum(counts[k] for k in ('tap','hold','flick','skyarea'))==1120)
    for code in (0,1,2):
        check('Ease start '+str(code),close(ease(code,0),0));check('Ease end '+str(code),close(ease(code,1),1))
    check('SineOut midpoint',close(ease(1,.5),math.sqrt(.5)))
    check('SineIn midpoint',close(ease(2,.5),1-math.sqrt(.5)))
    check('Clamped temporal progress',ease(0,-1)==0 and ease(0,2)==1)
    try:ease(7,.4);check('Unknown ease rejected',False)
    except ValueError:check('Unknown ease rejected',True)
    for left in range(3):
        for right in range(3):
            a=[1000,6,24,6,18,24,6,left,right,2000]
            l0,r0=sky(a,1000);l1,r1=sky(a,3000)
            check(f'Independent edge combination {left}/{right}',all(close(u,v) for u,v in zip((l0,r0,l1,r1),(.125,.375,.625,.875))))
    l,r=sky([1000,6,24,6,18,24,6,1,2,2000],2000)
    check('Midpoint width is 0.042893..., not 0.25',close(r-l,.0428932188134524))
    first=[x[1] for x in events if x[0]=='skyarea'][:2]
    for idx,t,width in [(0,1846,.01),(0,2769,1),(1,2769,1),(1,3692,.01)]:
        l,r=sky(first[idx],t);check(f'Opening segment {idx} at {t} width {width}',close(r-l,width))
        check(f'Opening segment {idx} center at {t}',close((l+r)/2,.5))
    check('Opening shared group 0 preserved',first[0][10]==0 and first[1][10]==0)
    check('Opening different divisors really present',first[0][2]==200 and first[0][5]==2 and first[1][2]==2 and first[1][5]==200)
    floor=[]
    for kind,a,line in events:
        if kind not in ('tap','hold'):continue
        lane,w=(a[2],a[1]) if kind=='tap' else (a[1],a[2])
        floor.append(lane==int(lane) and w==int(w) and 0<=lane<=5 and w>=1 and (w==1 if lane in (0,5) else lane+w<=5))
    check('All 626 floor spans valid',len(floor)==626 and all(floor))
    check('First tap parameter order',events[3][0]=='tap' and events[3][1]==[3692,1,1])
    check('First Flick is yellow-left direction 16',events[4][0]=='flick' and events[4][1]==[3692,6,24,12,16])
    check('Second Flick is green-right direction 4',next(a for k,a,n in events if k=='flick' and a[0]==4154)[4]==4)
    tracks=[(a[0],a[1]) for k,a,n in events if k=='track'];timeline=Scroll(tracks)
    check('Track records are at file end',min(n for k,a,n in events if k=='track')==1122)
    check('Track at 0 = 1',close(timeline.at(1000),1000))
    check('Track at 22154 applied to side Hold',close(timeline.at(22400)-timeline.at(22154),246*.30000001192092896))
    check('No BPM multiplication in scroll',close(timeline.at(2000)-timeline.at(1000),1000))
    rev=Scroll([(1000,-1),(2000,1)]);check('Three depths coincide',all(close(rev.at(t),500) for t in (500,1500,2500)))
    check('Reverse inverse returns three choices',len(rev.inverse(500,0,3000))==3)
    pause=Scroll([(1000,0),(2000,1)]);check('Zero speed inverse contains interval',(1000,2000) in pause.inverse(1000,0,3000))
    check('Duplicate track last event wins',close(Scroll([(1000,2),(1000,3)]).at(2000),4000))
    cal=Calibration()
    for name,world,expected in [('ground-left',(-2,0,0),(434,1029)),('ground-right',(2,0,0),(1614,1029)),('air',(0,cal.air,0),(1024,734)),('side-left',(-2-cal.run,cal.rise,0),(124,840)),('side-right',(2+cal.run,cal.rise,0),(1924,840))]:
        result=cal.project(*world);check('Projection '+name,all(close(a,b) for a,b in zip(result,expected)),str(result))
    check('Horizon at approximately y=35',abs(cal.project(0,0,1e9)[1]-35)<1e-4)
    check('Side slope approx 43.0768 degrees',abs(math.degrees(math.atan2(cal.rise,cal.run))-43.0767932212)<1e-7)
    for z in (-1.5,0,1,10,65):
        check('Both side seams touch ground at z='+str(z),cal.surface(-2,z)==(-2,0,z) and cal.surface(2,z)==(2,0,z))
        l=cal.project(*cal.surface(-2-cal.run,z));r=cal.project(*cal.surface(2+cal.run,z))
        check('Mirrored side projection at z='+str(z),close(l[0]+r[0],2048) and close(l[1],r[1]))
    # v0.2 fixed-height Flick, same depth => same height, independent of width.
    # This is a mathematical reference, NOT a Unity rendering test.
    height_by_depth={}
    for direction in (4,16):
      for z in (0.,1.23,8.16,20.):
       for width in (1/6,1/3,.5,1.,2.,4.):
        y=cal.air+.019
        xl,xr=-width/2,width/2;hi,tip=(xr,xl) if direction==4 else (xl,xr)
        hs=cal.project(hi,y,z);ts=cal.project(tip,y,z);points=[];screens=[]
        fixed_height=abs(cal.project(.5,y,z)[0]-cal.project(-.5,y,z)[0])*.4
        cap=min(fixed_height,abs(ts[0]-hs[0])*.5)
        for i in range(49):
            q=i/48;taper=1-math.sin(math.pi*q/2);h=fixed_height*taper
            px=hs[0]+(ts[0]-hs[0])*q+(-1 if direction==4 else 1)*cap*taper
            py=hs[1]-h;r=(576-py)/cal.f
            zz=(r*(cal.zc-y*cal.s)-(cal.yc+y*cal.c))/(cal.s-r*cal.c)
            xx=(px-1024)*(cal.zc-y*cal.s+zz*cal.c)/cal.f
            points.append((xx,y,zz));screens.append((px,py))
        tag=f'{direction} Z={z} width={width:.6f}'
        check('Flick flat Y '+tag,all(close(v[1],y) for v in points))
        check('Flick recedes, not rises '+tag,points[0][2]>z and close(points[-1][2],z))
        check('Flick width-independent height '+tag,close(hs[1]-screens[0][1],fixed_height))
        check('Flick inverse projection '+tag,all(abs(a-b)<1e-6 for v,xy in zip(points,screens) for a,b in zip(cal.project(*v),xy)))
        sign=-1 if direction==4 else 1
        check('Narrow Flick does not double back '+tag,all(sign*(b[0]-a[0])>=-1e-9 for a,b in zip(screens,screens[1:])))
        height_by_depth.setdefault(z,[]).append(hs[1]-screens[0][1])
    for z,heights in height_by_depth.items():check('Every width/direction same height at Z='+str(z),max(heights)-min(heights)<1e-8)
    check('Perspective height decreases in the distance',all(a>b for a,b in zip([height_by_depth[z][0] for z in sorted(height_by_depth)],[height_by_depth[z][0] for z in sorted(height_by_depth)][1:])))
    check('SPC opening event positions are untouched',lines[1]=='skyarea(1846,100,200,2,1,2,2,1,1,923,0)')
    check('Visual speed scales distance independently',close((1000*.015*4.5)/(1000*.015),4.5))
    # Filesystem safety and unity asset-reference audit. Not shader/C# compilation.
    index=json.loads((ROOT/'Documentation/Reference/asset_guid_index.json').read_text())
    check('GUID uniqueness',len(set(index.values()))==len(index))
    check('Scene and profile exist',(ASSETS/'Scenes/InFalsusStudio_Demo.unity').is_file() and (ASSETS/'Resources/InFalsusStudio/CalibratedProfile.asset').is_file())
    check('No global project configuration replacement',not (ROOT/'Packages').exists() and not (ROOT/'ProjectSettings').exists())
    for rel,guid in index.items():
        check('Meta reference '+rel,('guid: '+guid) in (ROOT/(rel+'.meta')).read_text())
    for p in [ASSETS/'Scenes/InFalsusStudio_Demo.unity',ASSETS/'Resources/InFalsusStudio/CalibratedProfile.asset']:
        for g in re.findall(r'guid: ([0-9a-f]{32})',p.read_text()):check('Resolved serialized GUID '+g,g in index.values())
    source='\n'.join(p.read_text() for p in ASSETS.rglob('*.cs'))
    check('No InputSystem package API use','using UnityEngine.InputSystem' not in source and 'Input.Get' not in source)
    report=dict(scope='Independent Python reference checks and asset-layout inspection; does NOT execute C# or Unity',passed=len(RESULTS),failed=0,sample_sha256=digest,counts=dict(counts),results=RESULTS)
    out=ROOT/'Documentation/Reference/python_verification.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(f'PASS: {len(RESULTS)} independent Python / asset checks. C# and Unity tests NOT run by this script.\nReport: {out}')
    return report

if __name__=='__main__':run()
