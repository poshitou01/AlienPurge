"""Export unchanged real Game View pixels and decoded real-video frames; no image generation."""
import sys,json,subprocess,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'TapTap_ReleaseAssets'
sys.path.insert(0,str(ROOT/'Logs/PlayerPreviewDependencies'))
from PIL import Image,ImageDraw,ImageChops
FF=ROOT/'Logs/TapTapTools/ffmpeg-9.0.2-essentials_build/bin/ffmpeg.exe'
items=[
 ('01_Colony_Combat','Gameplay_20261019_174824.mp4',6.0,'入口区正常射击与敌人'),
 ('02_Explosion_Chain','Gameplay_20261019_174824.mp4',145.0,'爆炸模块的实际效果与 Support 的支援连线'),
 ('03_Mixed_Enemies','Gameplay_20261019_174824.mp4',164.0,'后续区域的多类型敌人战斗'),
 ('04_Research_Terminal','Gameplay_20261019_180204.mp4',211.0,'研究区终端、任务进度与通路'),
 ('05_Loot_Search','Loot_175651.png',None,'入口区实际搜索容器'),
 ('06_Backpack','Backpack_175658.png',None,'本局拾取的战利品与背包'),
 ('07_HiveTyrant','Gameplay_20261019_181949.mp4',382.0,'从正式区域路线抵达的 Hive Tyrant 第二阶段实战'),
 ('08_Extraction_Defense','Extraction_Defense.png',None,'击败 Boss 后的真实撤离防守，保留原生爆炸和连锁闪电'),
]
records=[];sheet=Image.new('RGB',(1920,620),(15,25,32));d=ImageDraw.Draw(sheet)
for i,(name,source,t,desc) in enumerate(items):
    if t is not None:
        original=OUT/'06_Screenshots/Originals'/(name+'_'+Path(source).stem+f'_{t:.2f}s.png')
        subprocess.run([str(FF),'-v','error','-y','-ss',str(t),'-i',str(OUT/'07_Video/Raw'/source),'-frames:v','1',str(original)],check=True)
    else:original=OUT/'06_Screenshots/Originals'/source
    image=Image.open(original).convert('RGB')
    if image.size!=(1920,1080):raise RuntimeError('Unexpected real capture size')
    final=OUT/'06_Screenshots/Final'/(name+'.png');image.save(final,optimize=True)
    assert ImageChops.difference(image,Image.open(final).convert('RGB')).getbbox() is None
    records.append({'file':str(final.relative_to(OUT)).replace('\\','/'),'original':str(original.relative_to(OUT)).replace('\\','/'),
        'origin':source,'source_video_seconds':t,'description':desc,'scene':'Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity',
        'capture':'Unity Recorder real Game View decoded frame' if t is not None else 'Unity ScreenCapture in real Play Mode',
        'input':'Automated ordinary input/interaction APIs; no gameplay values or encounter progression modified',
        'postprocess':'Lossless PNG compression only; no crop, layout changes, color changes or synthetic contents',
        'pixels_equal_to_original_rgb':True,'sha256':hashlib.sha256(final.read_bytes()).hexdigest()})
    thumb=image.resize((480,270),Image.Resampling.LANCZOS);xx=(i%4)*480;yy=(i//4)*310
    sheet.paste(thumb,(xx,yy));d.text((xx+12,yy+285),name,fill='white')
    # Bigger 2x4 sheet below is intentionally not used as a deliverable screenshot.
(OUT/'06_Screenshots/SCREENSHOT_SOURCES.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),'utf-8')
sheet.save(OUT/'10_QA/Screenshot_Contact_Sheet.jpg',quality=94)
print('Exported eight real, pixel-preserving 1920x1080 screenshots.')
