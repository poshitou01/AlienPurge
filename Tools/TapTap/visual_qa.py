"""QA-only crop, alpha, small-size and video timeline previews; never source gameplay edits."""
import sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'TapTap_ReleaseAssets'
sys.path.insert(0,str(ROOT/'Logs/PlayerPreviewDependencies'))
from PIL import Image,ImageDraw
R=Image.Resampling.LANCZOS
bg=Image.open(OUT/'04_Background/AlienPurge_Library_Background_3840x1240.png').convert('RGB')
sheet=Image.new('RGB',(1600,1000),(17,26,33));draw=ImageDraw.Draw(sheet)
preview=bg.resize((1600,517),R);sheet.paste(preview,(0,20))
draw.rectangle((0,365,400,537),fill=(30,42,50));draw.text((20,390),'CLIENT ANNOUNCEMENT SAFE AREA (QA ONLY)',fill='white')
draw.rectangle((1290,20,1600,150),fill=(30,42,50));draw.text((1310,45),'CLIENT LOGO SAFE AREA',fill='white')
for i,(name,ratio) in enumerate([('Centered 16:9 crop',16/9),('Centered 21:9 crop',21/9)]):
    width=round(bg.height*ratio);crop=bg.crop(((bg.width-width)//2,0,(bg.width+width)//2,bg.height));crop.thumbnail((780,400),R)
    x=i*800;sheet.paste(crop,(x,580));draw.text((x+10,550),name,fill='white')
sheet.save(OUT/'10_QA/Background_Crop_Check.jpg',quality=94)
logo=Image.open(OUT/'03_Logo/AlienPurge_Logo_1280x720.png').convert('RGBA');logo.thumbnail((640,360),R)
sheet=Image.new('RGB',(1280,560),(15,25,32));d=ImageDraw.Draw(sheet)
for i,color in enumerate([(13,29,41),(210,218,222)]):
    panel=Image.new('RGBA',(640,360),color+(255,));panel.alpha_composite(logo,(0,0));sheet.paste(panel.convert('RGB'),(i*640,0))
    d.text((i*640+16,365),'Transparent logo / '+('dark' if i==0 else 'light')+' preview',fill='white')
icon=Image.open(OUT/'01_Icon/AlienPurge_Icon_512.png').convert('RGB')
for i,size in enumerate((128,64,32)):
    sheet.paste(icon.resize((size,size),R),(24+i*180,410));d.text((24+i*180,545),str(size)+'px',fill='white')
sheet.save(OUT/'10_QA/Logo_Alpha_Icon_Check.jpg',quality=94)
print('Created crop, logo contrast and icon small-size QA previews')
