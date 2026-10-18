"""Mechanical sprite-sheet slicing, canvas registration and review exports.

Requires Pillow 11.3. Source illustrations/poses are imagegen outputs, not generated
or deformed here. No rotations, mirroring, tweening or anatomy edits are performed.
Run from the project root: python Tools/EnemyArt/export_frames.py
"""
from pathlib import Path
from PIL import Image, ImageDraw
import json, statistics, hashlib
from collections import deque

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).parent / 'Source'
ART = ROOT / 'Assets/_Project/Art/Enemies/Animated'
REVIEW = ROOT / 'Docs/Enemies/SupportExploder/ArtReview'
DIRS = ['D', 'DR', 'R', 'UR', 'U', 'UL', 'L', 'DL']
REVIEW.mkdir(parents=True, exist_ok=True)

def cuts(alpha, count, axis):
    size = alpha.size[axis]
    other = alpha.size[1-axis]
    result = [0]
    for n in range(1, count):
        expected = size*n/count
        reach = size/count*.13
        candidates = range(round(expected-reach), round(expected+reach)+1)
        def score(p):
            box = (p, 0, p+1, other) if axis == 0 else (0, p, other, p+1)
            nonempty = other - alpha.crop(box).histogram()[0]
            return nonempty*10000 + abs(p-expected)
        result.append(min(candidates, key=score))
    return result + [size]

def slice_sheet(path, rows):
    image = Image.open(path).convert('RGBA')
    # Locate connected silhouettes, not an assumed perfect grid. Generated poses can
    # cross a nominal cell line; cutting on that line would amputate a planted foot.
    width,height=image.size
    mask=bytearray(image.getchannel('A').point(lambda x: 1 if x >= 24 else 0).tobytes())
    groups=[[] for _ in range(rows*4)]
    for start in range(width*height):
        if not mask[start]: continue
        mask[start]=0; queue=deque([start]); area=0
        x0=x1=start%width; y0=y1=start//width
        while queue:
            p=queue.popleft(); x=p%width; y=p//width; area+=1
            x0=min(x0,x); x1=max(x1,x); y0=min(y0,y); y1=max(y1,y)
            for q in (p-width if y else -1, p+width if y+1<height else -1, p-1 if x else -1, p+1 if x+1<width else -1):
                if q>=0 and mask[q]: mask[q]=0; queue.append(q)
        if area<24: continue
        col=min(3,int((x0+x1+1)*.5/width*4)); row=min(rows-1,int((y0+y1+1)*.5/height*rows))
        groups[row*4+col].append((x0,y0,x1+1,y1+1))
    frames, bounds, warnings = [], [], []
    for n,group in enumerate(groups):
        if not group: raise ValueError(f'Empty component group {path.name} {n}')
        bbox=(min(b[0] for b in group),min(b[1] for b in group),max(b[2] for b in group),max(b[3] for b in group))
        if bbox[2]-bbox[0]>width/4*1.3 or bbox[3]-bbox[1]>height/rows*1.3:
            raise ValueError(f'Touching source sprites {path.name} {n}: {bbox}')
        rect=(max(0,bbox[0]-2),max(0,bbox[1]-2),min(width,bbox[2]+2),min(height,bbox[3]+2))
        if bbox[0]<=1 or bbox[1]<=1 or bbox[2]>=width-1 or bbox[3]>=height-1:
            warnings.append(f'{path.name} frame {n+1}: inspect source canvas edge {bbox}')
        frames.append(image.crop(rect)); bounds.append(list(rect))
    return frames, bounds, warnings

def registered(frame, scale):
    w, h = round(frame.width*scale), round(frame.height*scale)
    if w > 466 or h > 440: raise ValueError(f'Canvas overflow {w}x{h}')
    canvas = Image.new('RGBA', (512,512))
    scaled = frame.resize((w,h), Image.Resampling.LANCZOS)
    canvas.alpha_composite(scaled, (round(256-w/2), 402-h))
    return canvas

audit=[]
for enemy in ['Support','Exploder']:
    for direction in DIRS:
        path=SOURCE/f'{enemy}_{direction}.png'
        if not path.exists(): continue
        frames, bounds, warnings=slice_sheet(path,5 if enemy=='Support' else 6)
        target_height=345 if enemy=='Support' else 330
        scale=target_height/statistics.median(f.height for f in frames[8:12])
        # One uniform scale per sheet, including charge/death/burst; never per-frame scaling.
        maxw,maxh=max(f.width for f in frames),max(f.height for f in frames)
        scale=min(scale,466/maxw,395/maxh)
        states=[('Move',range(8)),('Idle',range(8,12)),('SupportStart' if enemy=='Support' else 'Warning',range(12,16)),('Death',range(16,20))]
        if enemy=='Exploder': states.append(('Burst',range(20,24)))
        record={'enemy':enemy,'direction':direction,'source':path.name,'sourceSize':Image.open(path).size,'scale':scale,'sourceCuts':bounds,'warnings':warnings,'frames':[]}
        for state,indices in states:
            dest=ART/enemy/state/direction; dest.mkdir(parents=True,exist_ok=True)
            for n,index in enumerate(indices,1):
                out=registered(frames[index],scale)
                filename=f'{enemy}_{state}_{direction}_{n:02}.png'
                out.save(dest/filename,optimize=True)
                record['frames'].append({'path':str((dest/filename).relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256((dest/filename).read_bytes()).hexdigest(),'alphaBounds':out.getchannel('A').getbbox()})
        audit.append(record)

active_manifest=Path(__file__).parent/'active_sheets.json'
if active_manifest.exists():
    for entry in json.loads(active_manifest.read_text(encoding='utf-8')):
        frames,bounds,warnings=slice_sheet(SOURCE/entry['file'],entry['rows'])
        for k,direction in enumerate(entry['dirs']):
            selected=frames[k*8:k*8+8]
            normal=[Image.open(ART/'Support'/'Move'/direction/f'Support_Move_{direction}_{n:02}.png').getchannel('A').getbbox() for n in range(1,9)]
            target_width=statistics.median(b[2]-b[0] for b in normal)
            scale=target_width/statistics.median(f.width for f in selected)
            scale=min(scale,466/max(f.width for f in selected),395/max(f.height for f in selected))
            dest=ART/'Support'/'SupportMove'/direction; dest.mkdir(parents=True,exist_ok=True)
            record={'enemy':'Support','direction':direction,'source':entry['file'],'state':'SupportMove','scale':scale,'warnings':warnings,'frames':[]}
            for n,frame in enumerate(selected,1):
                out=registered(frame,scale); path=dest/f'Support_SupportMove_{direction}_{n:02}.png'; out.save(path,optimize=True)
                record['frames'].append({'path':str(path.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'alphaBounds':out.getchannel('A').getbbox()})
            audit.append(record)

blast=SOURCE/'Exploder_Blast.png'
if blast.exists():
    frames,bounds,warnings=slice_sheet(blast,2)
    scale=488/max(max(f.size) for f in frames)
    dest=ART/'Exploder'/'Explosion'; dest.mkdir(parents=True,exist_ok=True)
    gif=[]
    for n,frame in enumerate(frames,1):
        scaled=frame.resize((round(frame.width*scale),round(frame.height*scale)),Image.Resampling.LANCZOS)
        out=Image.new('RGBA',(512,512)); out.alpha_composite(scaled,((512-scaled.width)//2,(512-scaled.height)//2))
        out.save(dest/f'Exploder_Explosion_{n:02}.png',optimize=True)
        display=Image.new('RGB',(512,512),(30,37,43)); display.paste(out,(0,0),out); gif.append(display)
    gif[0].save(REVIEW/'Exploder_Explosion.gif',save_all=True,append_images=gif[1:],duration=80,loop=0,disposal=2)

def preview_cell(enemy,state,direction,frame, size=192):
    path=ART/enemy/state/direction/f'{enemy}_{state}_{direction}_{frame:02}.png'
    canvas=Image.new('RGB',(size,size),(30,37,43))
    if path.exists():
        sprite=Image.open(path).resize((size,size),Image.Resampling.LANCZOS)
        canvas.paste(sprite,(0,0),sprite)
    return canvas

for enemy in ['Support','Exploder']:
    contact=Image.new('RGB',(4*256,2*280),(30,37,43)); draw=ImageDraw.Draw(contact)
    for i,direction in enumerate(DIRS):
        contact.paste(preview_cell(enemy,'Idle',direction,1,256),((i%4)*256,(i//4)*280+22))
        draw.text(((i%4)*256+12,(i//4)*280+6),f'{enemy} / {direction}',fill=(210,219,224))
    contact.save(REVIEW/f'{enemy}_EightDirections.png')
    for state in (['Move','SupportMove'] if enemy=='Support' else ['Move']):
        animation=[]
        for frame in range(1,9):
            canvas=Image.new('RGB',(4*192,2*218),(30,37,43)); draw=ImageDraw.Draw(canvas)
            for i,direction in enumerate(DIRS):
                canvas.paste(preview_cell(enemy,state,direction,frame),((i%4)*192,(i//4)*218+20))
                draw.text(((i%4)*192+10,(i//4)*218+5),direction,fill=(215,220,225))
            animation.append(canvas)
        animation[0].save(REVIEW/f'{enemy}_{state}_AllDirections.gif',save_all=True,append_images=animation[1:],duration=[80,80,90,80,80,90,80,90],loop=0,disposal=2)
    animation=[]
    states=['Idle','SupportStart','Death'] if enemy=='Support' else ['Idle','Warning','Death','Burst']
    for frame in range(1,5):
        canvas=Image.new('RGB',(256*len(states),280),(30,37,43)); draw=ImageDraw.Draw(canvas)
        for i,state in enumerate(states):
            canvas.paste(preview_cell(enemy,state,'DR',frame,256),(i*256,24)); draw.text((i*256+10,8),state,fill=(215,220,225))
        animation.append(canvas)
    animation[0].save(REVIEW/f'{enemy}_States_DR.gif',save_all=True,append_images=animation[1:],duration=200,loop=0,disposal=2)

(REVIEW/'SpriteAudit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
print(f'Exported {sum(len(x["frames"]) for x in audit)} independent frames from {len(audit)} sheets')
for record in audit:
    for warning in record['warnings']: print('REVIEW:',warning)
