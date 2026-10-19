"""Create time-labeled review contact sheets from real recordings; labels never enter deliverable video."""
import argparse,subprocess,sys,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'TapTap_ReleaseAssets'
sys.path.insert(0,str(ROOT/'Logs/PlayerPreviewDependencies'))
from PIL import Image,ImageDraw
FF=ROOT/'Logs/TapTapTools/ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe'
parser=argparse.ArgumentParser();parser.add_argument('take');parser.add_argument('--times',required=True);parser.add_argument('--name',required=True)
args=parser.parse_args();times=[float(x) for x in args.times.split(',')]
source=OUT/'07_Video/Raw'/args.take
folder=OUT/'10_QA'/args.name;folder.mkdir(exist_ok=True)
sheet=Image.new('RGB',(1920,310*((len(times)+3)//4)),(12,21,28));d=ImageDraw.Draw(sheet)
for i,t in enumerate(times):
    path=folder/f'{t:08.2f}.png'
    subprocess.run([str(FF),'-hide_banner','-v','error','-y','-ss',str(t),'-i',str(source),'-frames:v','1','-vf','scale=480:270',str(path)],check=True)
    im=Image.open(path);x=(i%4)*480;y=(i//4)*310;sheet.paste(im,(x,y));d.text((x+12,y+280),f'{source.name}  {t:.2f}s',fill=(240,240,240))
sheet.save(OUT/'10_QA'/(args.name+'.jpg'),quality=94)
print(OUT/'10_QA'/(args.name+'.jpg'))
