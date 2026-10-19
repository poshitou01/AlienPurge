"""Check the actual upload assets, video streams, and protected tracked project files."""
import sys,json,hashlib,subprocess,argparse
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'TapTap_ReleaseAssets'
sys.path.insert(0,str(ROOT/'Logs/PlayerPreviewDependencies'))
from PIL import Image
BIN=ROOT/'Logs/TapTapTools/ffmpeg-9.0.2-essentials_build/bin'
specs={
 '01_Icon/AlienPurge_Icon_512.png':(512,512,4),
 '02_Covers/AlienPurge_PC_Cover_460x215.png':(460,215,3),
 '02_Covers/AlienPurge_Vertical_Cover_600x900.png':(600,900,3),
 '03_Logo/AlienPurge_Logo_1280x720.png':(1280,720,4),
 '04_Background/AlienPurge_Library_Background_3840x1240.png':(3840,1240,10),
 '05_Promotional/AlienPurge_Promo_1920x1080.png':(1920,1080,4),
 '05_Promotional/AlienPurge_Windows_Promo_NoLogo_1920x1080.png':(1920,1080,6),
 '05_Promotional/AlienPurge_Windows_Promo_NoLogo_720x1080.png':(720,1080,6),
}
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()
def main():
    records=[];failures=[]
    for p in sorted((OUT/'06_Screenshots/Final').glob('*.png')):specs[str(p.relative_to(OUT)).replace('\\','/')]=(1920,1080,4)
    if len(list((OUT/'06_Screenshots/Final').glob('*.png')))!=8:failures.append('Exactly eight final gameplay screenshots required')
    for rel,(w,h,mb) in specs.items():
        p=OUT/rel
        if not p.exists():failures.append('Missing '+rel);continue
        with Image.open(p) as im:
            im.load();mode=im.mode;size=im.size;alpha=im.getextrema()[-1] if mode=='RGBA' else [255,255]
            entropy=im.convert('RGB').entropy();fmt=im.format
        checks={'dimensions':size==(w,h),'format':fmt=='PNG','size_bytes':p.stat().st_size<mb*1_000_000,'not_blank':entropy>2}
        if rel.startswith('03_'):checks['real_transparency']=mode=='RGBA' and alpha[0]==0 and alpha[1]==255
        if rel.startswith('01_'):checks['opaque']=alpha==[255,255] or alpha==(255,255)
        records.append({'path':rel,'kind':'real_gameplay_screenshot' if rel.startswith('06_') else 'non_gameplay_store_art',
            'dimensions':size,'bytes':p.stat().st_size,'sha256':sha(p),'mode':mode,'format':fmt,'entropy':entropy,'checks':checks,
            'visual_review':'See contact sheets and QA report; final owner review required'})
        failures.extend(rel+': '+k for k,v in checks.items() if not v)
    v=OUT/'07_Video/Final/AlienPurge_TapTap_Gameplay_1080p.mp4'
    if v.exists():
        probe=json.loads(subprocess.check_output([str(BIN/'ffprobe.exe'),'-v','error','-show_streams','-show_format','-of','json',str(v)],text=True))
        (OUT/'10_QA/VIDEO_FFPROBE.json').write_text(json.dumps(probe,indent=2),'utf-8')
        video=next(s for s in probe['streams'] if s['codec_type']=='video');audio=next((s for s in probe['streams'] if s['codec_type']=='audio'),{})
        checks={'dimensions':(video['width'],video['height'])==(1920,1080),'codec':video['codec_name']=='h264',
            'duration':90<=float(probe['format']['duration'])<=180,'size':v.stat().st_size<500_000_000,
            'frame_rate':video['avg_frame_rate']=='30/1','audio':audio.get('codec_name')=='aac'}
        records.append({'path':str(v.relative_to(OUT)).replace('\\','/'),'kind':'real_gameplay_video','bytes':v.stat().st_size,
            'sha256':sha(v),'duration':float(probe['format']['duration']),'checks':checks})
        failures.extend('Video '+k for k,val in checks.items() if not val)
    else:failures.append('Final video not present')
    result={'source_commit':'eecc6e3d14dca76a6bd6a6ec29d427cd5f32e19c','branch':'feature/taptap-release-assets-v1',
        'files':records,'automated_failures':failures,'source_register':'09_Source/SOURCE_REGISTER.md',
        'screenshots_source':'06_Screenshots/SCREENSHOT_SOURCES.json','video_source':'07_Video/VIDEO_EDIT_LOG.md',
        'copyright_review':'Project font redistribution credentials and complete visual provenance require owner confirmation; see QA report'}
    (OUT/'10_QA/ASSET_MANIFEST.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
    print(json.dumps({'files':len(records),'failures':failures},ensure_ascii=False))
if __name__=='__main__':main()
