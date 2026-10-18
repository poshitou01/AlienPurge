from pathlib import Path
from PIL import Image
folder=Path(__file__).resolve().parents[2]/'Docs/Enemies/CoreEnemies/PolishV2/Core'
for name in ['Tank_Motion_V2','Tank_Live','Rusher_Live','Spitter_Live','Four_Move_Live']:
    frames=[Image.open(p).convert('RGB') for p in sorted((folder/name).glob('*.png'))]
    if not frames: continue
    # Export the actual Unity camera recording; never used as runtime enemy art.
    frames[0].save(folder/(name+'.gif'),save_all=True,append_images=frames[1:],duration=67 if name=='Tank_Motion_V2' else 83,loop=0)
    print(name,len(frames))
