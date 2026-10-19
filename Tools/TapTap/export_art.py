"""Reproducible store layouts. Never enlarges a raster source; no gameplay screenshots edited."""
import sys, json, math, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Logs/PlayerPreviewDependencies'))
from PIL import Image, ImageDraw, ImageFilter
OUT=ROOT/'TapTap_ReleaseAssets'; SRC=OUT/'09_Source'
R=Image.Resampling.LANCZOS

def fit(im,size):
    factor=max(size[0]/im.width,size[1]/im.height)
    if factor>1.00001:raise ValueError('Raster enlargement forbidden: '+str((im.size,size)))
    resized=im.resize((round(im.width*factor),round(im.height*factor)),R)
    x=(resized.width-size[0])//2;y=(resized.height-size[1])//2
    return resized.crop((x,y,x+size[0],y+size[1]))

def save(im,path):
    im.save(OUT/path,optimize=True)
    print(path,im.size,(OUT/path).stat().st_size)

icon=Image.open(SRC/'Icon_Master.png').convert('RGB')
save(fit(icon,(512,512)),'01_Icon/AlienPurge_Icon_512.png')

logo=Image.open(SRC/'Logo_Master.png').convert('RGBA')
if logo.getextrema()[3][0]!=0:raise ValueError('Logo master lacks transparency')
logo=logo.crop(logo.getbbox())
def wordmark(width):
    if width>logo.width:raise ValueError('Logo upscale prohibited')
    return logo.resize((width,round(logo.height*width/logo.width)),R)
logo_out=Image.new('RGBA',(1280,720));mark=wordmark(1180)
logo_out.alpha_composite(mark,((1280-mark.width)//2,(720-mark.height)//2))
save(logo_out,'03_Logo/AlienPurge_Logo_1280x720.png')

painting=Image.open(SRC/'KeyArt_Master.png').convert('RGB')
portrait=Image.open(SRC/'Vertical_Master.png').convert('RGB')
# Small landscape key art: keep the full original picture, place the identity in the quiet upper-left sky.
cover=fit(painting,(460,215)).convert('RGBA')
mark=wordmark(196);cover.alpha_composite(mark,(16,13))
save(cover.convert('RGB'),'02_Covers/AlienPurge_PC_Cover_460x215.png')
vertical=fit(portrait,(600,900)).convert('RGBA');mark=wordmark(520)
vertical.alpha_composite(mark,(40,50))
save(vertical.convert('RGB'),'02_Covers/AlienPurge_Vertical_Cover_600x900.png')
save(fit(portrait,(720,1080)),'05_Promotional/AlienPurge_Windows_Promo_NoLogo_720x1080.png')

# Native-resolution landscape master is 1672x941. It is NOT stretched to 1920.
# The 1920x1080 layout deliberately adds a designed dark industrial surround and a separate wordmark.
# The full illustration stays at its native pixels; edge matting is only on non-gameplay illustration.
w,h=1920,1080
base=Image.new('RGB',(w,h));pix=base.load()
for y in range(h):
    for x in range(w):
        glow=max(0,1-math.hypot((x-600)/1450,(y-200)/1100))
        pix[x,y]=(round(10+22*glow),round(23+36*glow),round(32+46*glow))
art=painting.convert('RGBA');x=(w-art.width)//2;y=h-art.height
mask=Image.new('L',art.size);m=mask.load()
for yy in range(art.height):
    for xx in range(art.width):
        edge=min(1,xx/105,(art.width-1-xx)/105,yy/70)
        m[xx,yy]=round(255*max(0,edge))
art.putalpha(mask);native=base.convert('RGBA');native.alpha_composite(art,(x,y))
save(native.convert('RGB'),'05_Promotional/AlienPurge_Windows_Promo_NoLogo_1920x1080.png')
promo=native.copy();mark=wordmark(630);promo.alpha_composite(mark,(78,48))
save(promo.convert('RGB'),'05_Promotional/AlienPurge_Promo_1920x1080.png')
shutil.copy2(SRC/'Arena_Environment_Close_3840x1240.png',OUT/'04_Background/AlienPurge_Library_Background_3840x1240.png')

(SRC/'LAYOUT_PARAMETERS.json').write_text(json.dumps({
    'raster_enlargement':False,'screenshot_editing':False,
    'promo':{'canvas':[w,h],'illustration_native':painting.size,'illustration_position':[x,y],
             'matte_edge_pixels':[105,70],'logo_width':630,'logo_position':[78,48]},
    'pc_cover':{'logo_width':196,'logo_position':[16,13]},
    'vertical':{'logo_width':520,'logo_position':[40,50]},
    'library':'native Unity camera render, orthographic half-height 15; original scene not saved',
    'source':'Generated illustrative masters derived from project artwork; never described as gameplay.'
},ensure_ascii=False,indent=2),'utf-8')

# Contact sheet is QA only; never counted as a deliverable screenshot.
paths=['01_Icon/AlienPurge_Icon_512.png','02_Covers/AlienPurge_PC_Cover_460x215.png','02_Covers/AlienPurge_Vertical_Cover_600x900.png',
 '03_Logo/AlienPurge_Logo_1280x720.png','04_Background/AlienPurge_Library_Background_3840x1240.png','05_Promotional/AlienPurge_Promo_1920x1080.png']
sheet=Image.new('RGB',(1500,1100),(18,30,40));draw=ImageDraw.Draw(sheet)
for i,path in enumerate(paths):
    im=Image.open(OUT/path).convert('RGBA');im.thumbnail((470,480),R)
    col=i%3;row=i//3;xx=15+col*500;yy=30+row*540
    sheet.paste(im,(xx+(470-im.width)//2,yy),im)
    draw.text((xx,yy+490),Path(path).name,fill=(240,240,240))
sheet.save(OUT/'10_QA/Art_Contact_Sheet.jpg',quality=92)
