"""Mechanical alpha-component extraction and fixed-scale sprite registration.

Imagegen draws every pose. This script does not draw, deform, rotate, mirror or
interpolate anatomy. Existing enemy art / metadata is never overwritten.
Requires Pillow; run from project root. Missing directions remain explicit.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops
from collections import deque
from array import array
import hashlib, json, statistics

ROOT=Path(__file__).resolve().parents[2]
HERE=Path(__file__).parent
ART=ROOT/'Assets/_Project/Art/Enemies/Animated'
REVIEW=ROOT/'Docs/Enemies/CoreEnemies/ArtReview'
REVIEW.mkdir(parents=True,exist_ok=True)
DIRS=['D','DR','R','UR','U','UL','L','DL']
REFERENCES={r['enemy']:r for r in json.loads((HERE/'References/ExistingArt.json').read_text())}
STATES={
    'Swarmer':[('Move',8),('Idle',4),('Death',4)],
    'Rusher':[('Move',8),('Idle',4),('Windup',4),('Attack',8),('Death',4)],
    'Tank':[('Move',8),('Idle',4),('Windup',4),('Attack',4),('Death',4)],
    'Spitter':[('Move',8),('Idle',4),('Windup',4),('Attack',4),('Death',4)]}

def components(im):
    w,h=im.size
    mask=bytearray(im.getchannel('A').point(lambda a:1 if a>=40 else 0).tobytes())
    result=[]; labels=array('H',[0])*(w*h); label=0
    for start in range(w*h):
        if not mask[start]: continue
        label+=1
        mask[start]=0; q=deque([start]); area=0; x0=x1=start%w; y0=y1=start//w
        while q:
            p=q.popleft(); labels[p]=label; x=p%w; y=p//w; area+=1
            x0=min(x0,x); x1=max(x1,x); y0=min(y0,y); y1=max(y1,y)
            for a in (p-w if y else -1,p+w if y+1<h else -1,p-1 if x else -1,p+1 if x+1<w else -1):
                if a>=0 and mask[a]: mask[a]=0; q.append(a)
        if area>=20: result.append((area,(x0,y0,x1+1,y1+1),label))
    return sorted(result,reverse=True),labels

def split(im,count,source):
    regions,labels=components(im)
    assert len(regions)>=count, f'{source}: expected {count} silhouettes, found {len(regions)}'
    main=regions[:count]
    assert main[-1][0]>500, f'{source}: tiny or missing pose'
    assert len(regions)==count or regions[count][0]<main[-1][0]*.25, f'{source}: unexpected extra body'
    boxes=[list(box)+[{label}] for _,box,label in main]
    # A separated toe/plate is assigned to the nearest complete silhouette; retain it.
    for _,part,label in regions[count:]:
        cx=(part[0]+part[2])/2; cy=(part[1]+part[3])/2
        def distance(b): return max(b[0]-cx,0,cx-b[2])**2+max(b[1]-cy,0,cy-b[3])**2
        i=min(range(count),key=lambda i:distance(boxes[i])); b=boxes[i]
        if distance(b)<(min(im.size)/20)**2:
            boxes[i]=[min(b[0],part[0]),min(b[1],part[1]),max(b[2],part[2]),max(b[3],part[3]),b[4]|{label}]
    # Sort actual rows, not ideal equal-width cells: source generators may vary gutters.
    boxes.sort(key=lambda b:(b[1]+b[3])/2)
    ordered=[]
    for i in range(0,count,4): ordered.extend(sorted(boxes[i:i+4],key=lambda b:(b[0]+b[2])/2))
    frames=[]
    for box in ordered:
        b=[max(0,box[0]-2),max(0,box[1]-2),min(im.width,box[2]+2),min(im.height,box[3]+2)]
        frame=im.crop(b)
        # Extract THIS disconnected sprite, rather than a neighboring toe whose
        # rectangular bounding box overlaps. Retain original RGBA and its 2px
        # antialias perimeter; no anatomy is painted, warped or interpolated.
        ids=[labels[y*im.width+x] for y in range(b[1],b[3]) for x in range(b[0],b[2])]
        mask=Image.frombytes('L',frame.size,bytes(255 if value in box[4] else 0 for value in ids)).filter(ImageFilter.MaxFilter(5))
        separated=bytes(value if owner==0 or owner in box[4] else 0 for value,owner in zip(mask.tobytes(),ids))
        frame.putalpha(ImageChops.multiply(frame.getchannel('A'),Image.frombytes('L',frame.size,separated)))
        frames.append(frame)
    return frames,[b[:4] for b in ordered]

audit=[]; missing=[]; invalid=[]
for enemy,specs in STATES.items():
    ref=REFERENCES[enemy]
    for d in DIRS:
        source=HERE/'Source'/f'{enemy}_{d}.png'
        if not source.exists(): missing.append(source.name); continue
        im=Image.open(source).convert('RGBA'); count=sum(n for _,n in specs)
        try:
            frames,cuts=split(im,count,source.name)
        except AssertionError as error:
            invalid.append(str(error)); continue
        original=next(r['alphaBounds'] for r in ref['views'] if r['direction']==d)
        target_area=(original[2]-original[0])*(original[3]-original[1])
        scale=(target_area/statistics.median(f.width*f.height for f in frames[8:12]))**.5
        foot=ref['footBaseline']
        scale=min(scale,460/max(f.width for f in frames),(foot-20)/max(f.height for f in frames))
        record={'enemy':enemy,'direction':d,'source':source.name,'scale':scale,'footBaseline':foot,'sourceCuts':cuts,'frames':[]}
        offset=0
        for state,n in specs:
            destination=ART/enemy/'Authored'/state/d; destination.mkdir(parents=True,exist_ok=True)
            for i,frame in enumerate(frames[offset:offset+n],1):
                resized=frame.resize((round(frame.width*scale),round(frame.height*scale)),Image.Resampling.LANCZOS)
                out=Image.new('RGBA',(512,512)); out.alpha_composite(resized,(round((512-resized.width)/2),foot-resized.height))
                path=destination/f'{enemy}_{state}_{d}_{i:02}.png'; out.save(path,optimize=True)
                record['frames'].append({'path':path.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'alphaBounds':out.getchannel('A').getbbox()})
            offset+=n
        audit.append(record)

def cell(enemy,state,d,frame,size):
    canvas=Image.new('RGB',(size,size),(30,37,43)); p=ART/enemy/'Authored'/state/d/f'{enemy}_{state}_{d}_{frame:02}.png'
    if p.exists():
        sprite=Image.open(p).resize((size,size),Image.Resampling.LANCZOS); canvas.paste(sprite,(0,0),sprite)
    return canvas

for enemy,specs in STATES.items():
    sheet=Image.new('RGB',(1024,560),(30,37,43)); draw=ImageDraw.Draw(sheet)
    for i,d in enumerate(DIRS):
        x=i%4*256; y=i//4*280
        sheet.paste(cell(enemy,'Idle',d,1,256),(x,y+24)); draw.text((x+10,y+6),f'{enemy} / {d}',fill=(220,225,230))
    sheet.save(REVIEW/f'{enemy}_EightDirections.png')
    frames=[]
    for frame in range(1,9):
        sheet=Image.new('RGB',(768,436),(30,37,43)); draw=ImageDraw.Draw(sheet)
        for i,d in enumerate(DIRS):
            x=i%4*192; y=i//4*218; sheet.paste(cell(enemy,'Move',d,frame,192),(x,y+22)); draw.text((x+10,y+5),d,fill='white')
        frames.append(sheet)
    frames[0].save(REVIEW/f'{enemy}_Move.gif',save_all=True,append_images=frames[1:],duration=[80,80,90,80,80,90,80,90],loop=0,disposal=2)
    states=[s for s,n in specs if s!='Move']; frames=[]
    for frame in range(1,5):
        sheet=Image.new('RGB',(256*len(states),280),(30,37,43)); draw=ImageDraw.Draw(sheet)
        for i,s in enumerate(states):
            sheet.paste(cell(enemy,s,'DR',frame,256),(i*256,24)); draw.text((i*256+10,6),s,fill='white')
        frames.append(sheet)
    frames[0].save(REVIEW/f'{enemy}_States_DR.gif',save_all=True,append_images=frames[1:],duration=200,loop=0,disposal=2)
(REVIEW/'SpriteAudit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
print('Exported',sum(len(x['frames']) for x in audit),'distinct frames; missing directions:',missing)
if invalid:
    print('REJECTED SOURCE SHEETS:',*invalid,sep='\n')
    raise SystemExit(1)
