"""Read existing clip references and build labeled contact sheets for art inspection."""
from pathlib import Path
from PIL import Image, ImageDraw
import re, json, statistics

ROOT=Path(__file__).resolve().parents[2]
DEST=ROOT/'Tools/CoreEnemyArt/References'
DEST.mkdir(parents=True,exist_ok=True)
directions=['D','DR','R','UR','U','UL','L','DL']
lookup={}
for p in (ROOT/'Assets/_Project/Art/Enemies/Animated').rglob('*.png.meta'):
    match=re.search(r'^guid: (\w+)',p.read_text(),re.M)
    if match: lookup[match[1]]=Path(str(p)[:-5])
report=[]
for enemy in ('Swarmer','Rusher','Tank','Spitter'):
    clips=list((ROOT/'Assets/_Project/Animations'/enemy).rglob('*.anim'))
    sheet=Image.new('RGB',(1024,560),(30,37,43)); draw=ImageDraw.Draw(sheet)
    rows=[]
    for i,d in enumerate(directions):
        clip=next(p for p in clips if p.stem.lower()==f'{enemy}_Move_{d}'.lower())
        guid=re.search(r'value: \{fileID: 21300000, guid: (\w+)',clip.read_text()).group(1)
        source=lookup[guid]; im=Image.open(source).convert('RGBA')
        rect=im.getchannel('A').getbbox(); rows.append({'direction':d,'sprite':source.relative_to(ROOT).as_posix(),'alphaBounds':rect})
        thumb=im.resize((256,256),Image.Resampling.LANCZOS)
        x=i%4*256; y=i//4*280
        sheet.paste(thumb,(x,y+24),thumb); draw.text((x+8,y+6),f'{enemy} clip / {d}',fill=(220,225,230))
    sheet.save(DEST/f'{enemy}_ExistingViews.png')
    report.append({'enemy':enemy,'views':rows,'footBaseline':round(statistics.median(r['alphaBounds'][3] for r in rows))})
(DEST/'ExistingArt.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
