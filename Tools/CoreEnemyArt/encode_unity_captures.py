"""Encode actual Unity capture sequences; no synthetic/interpolated animation."""
from pathlib import Path
from PIL import Image
import csv

root=Path(__file__).resolve().parents[2]/'Docs/Enemies/CoreEnemies/Validation'
for folder in sorted(root.glob('*_Live')):
    paths=sorted(folder.glob('*.png'))
    if not paths: continue
    frames=[Image.open(p).convert('RGB') for p in paths]
    durations=[83]*len(frames)
    if (folder/'timing.csv').exists():
        rows=list(csv.DictReader((folder/'timing.csv').open()))
        times=[float(row['scaled_time']) for row in rows]
        durations=[max(20,round((b-a)*1000)) for a,b in zip(times,times[1:])]+[100]
    frames[0].save(root/(folder.name+'.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,disposal=2)
    print(folder.name,len(frames),'captured frames')
