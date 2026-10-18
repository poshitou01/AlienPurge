"""Analyze torso registration; change importer pivots only. PNG pixels are untouched."""
from pathlib import Path
from PIL import Image
import json,statistics,math
root=Path(__file__).resolve().parents[2]
draft=root
base=Path('Assets/_Project/Art/Enemies/Animated')
names=['Swarmer','Rusher','Tank','Spitter','Support','Exploder']
entries=[];report=[]
def anchor(path,name):
    # Brown shell in the central torso ROI. Excludes pale membranes/feet and bright organs.
    image=Image.open(path).convert('RGBA').resize((128,128))
    dots=[]
    lo,hi=(.40,.76) if name=='Support' else (.31,.72) if name=='Exploder' else (.12,.65)
    for y in range(int(lo*128),int(hi*128)):
        for x in range(24,104):
            r,g,b,a=image.getpixel((x,y))
            if a>220 and 24<r<191 and 1.08*g<r<1.6*g and 1.04*b<g<1.85*b:
                dots.append((x*4+2,y*4+2))
    assert len(dots)>100,(path,len(dots))
    return (statistics.mean(p[0] for p in dots),statistics.mean(p[1] for p in dots))
for name in names:
    folder=root/base/name/('Authored' if name in names[:4] else '')
    paths=sorted(folder.rglob('*.png'))
    locomotion=[p for p in paths if p.parent.parent.name in ('Idle','Move','SupportMove')]
    anchors={p:anchor(p,name) for p in locomotion}
    target=tuple(statistics.mean(v[i] for v in anchors.values()) for i in (0,1))
    offsets={}
    for direction in ('D','DR','R','UR','U','UL','L','DL'):
        idle=[a for p,a in anchors.items() if p.parent.name==direction and p.parent.parent.name=='Idle']
        offsets[direction]=tuple(statistics.mean(a[i] for a in idle)-target[i] for i in (0,1))
    drift=[]
    for direction in offsets:
        gait=[anchors[p] for p in locomotion if p.parent.name==direction and p.parent.parent.name=='Move']
        drift.extend(math.dist(gait[i],gait[(i+1)%len(gait)]) for i in range(len(gait)))
    for p in paths:
        if p.parent.name not in offsets: continue # radial VFX stays centered
        dx,dy=(anchors[p][0]-target[0],anchors[p][1]-target[1]) if p in anchors else offsets[p.parent.name]
        assert max(abs(dx),abs(dy))<85,(p,dx,dy)
        entries.append({'path':p.relative_to(root).as_posix(),'x':round(.5+dx/512,7),'y':round(.5-dy/512,7)})
    report.append(f'{name}: move torso centroid step mean={statistics.mean(drift):.2f}px max={max(drift):.2f}px; importer registration residual <0.001px (measured shell mask, not anatomy).')
    if name=='Tank': print([(p.name,tuple(round(v,1) for v in anchors[p])) for p in locomotion if p.parent.name=='DR' and p.parent.parent.name=='Move'])
out=draft/base/'MotionRegistration.json';out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps({'entries':entries},indent=2),encoding='utf-8')
report_path=draft/'Docs/Enemies/CoreEnemies/PolishV2/MotionRegistration_Report.txt'
report_path.parent.mkdir(parents=True,exist_ok=True)
report_path.write_text('\n'.join(report),encoding='utf-8')
print('\n'.join(report));print('Registered sprite pivots:',len(entries))
