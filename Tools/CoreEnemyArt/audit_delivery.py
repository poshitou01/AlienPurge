"""Read-only delivery checks against the recorded local baseline."""
from pathlib import Path
from PIL import Image
import subprocess,hashlib,json,re

root=Path(__file__).resolve().parents[2]
base='727645fb7ce2acce8910ecf1d053522b394c7afc'
def git(*args): return subprocess.check_output(['git','-C',str(root),*args]).decode('utf-8').strip()
changed=git('diff','--name-only','--diff-filter=MDR',base).splitlines()
allowed_existing={
    'Assets/_Project/Editor/SupportExploderSetup.cs',
    'Assets/_Project/Scripts/Enemy/FastChargeAttack.cs',
    'Assets/_Project/Scripts/Enemy/Attacks/HeavyShockwaveAttack.cs',
    'Assets/_Project/Scripts/Enemy/Attacks/RangedEnemyAttack.cs',
    'Assets/_Project/Scripts/Enemy/EnemyContactDamage.cs',
    *['Assets/_Project/Prefabs/Enemies/Animated/Enemy_'+name+'.prefab' for name in
      ('Normal_Swarmer','Fast_Rusher','Heavy_Tank','Ranged_Spitter')]
}
assert not set(changed)-allowed_existing,('Unexpected existing file modified',set(changed)-allowed_existing)
assert not git('diff','--name-only','--diff-filter=DR',base),'Existing file deleted or renamed'
assert not [p for p in changed if p.endswith('.meta')], 'An existing GUID/meta changed'
assert not [p for p in changed if '/Data/' in p or '/Boss/' in p or ('/Scenes/' in p and not p.endswith('/CoreEnemies_Art_Test.unity'))], 'Protected existing scene/config changed'
frames=[]; hashes=set(); counts={}
for enemy,count in [('Swarmer',128),('Rusher',224),('Tank',192),('Spitter',192)]:
    files=sorted((root/f'Assets/_Project/Art/Enemies/Animated/{enemy}/Authored').rglob('*.png'))
    assert len(files)==count,(enemy,len(files),count)
    counts[enemy]=len(files)
    for path in files:
        im=Image.open(path)
        assert im.mode=='RGBA' and im.size==(512,512),path
        a=im.getchannel('A'); box=a.getbbox()
        assert box and min(box[0],box[1],512-box[2],512-box[3])>=8,('canvas edge',path,box)
        assert a.getextrema()==(0,255),('alpha range',path)
        assert path.with_suffix('.png.meta').exists(),('missing meta',path)
        digest=hashlib.sha256(im.tobytes()).hexdigest()
        assert digest not in hashes,('duplicate authored frame',path)
        hashes.add(digest); frames.append({'path':path.relative_to(root).as_posix(),'pixelSha256':digest})
new_files=git('ls-files','--others','--exclude-standard','Assets').splitlines()
# Include staged additions as well, so checks still work immediately before commit.
new_files+=git('diff','--cached','--name-only','--diff-filter=A','--','Assets').splitlines()
for rel in set(new_files):
    p=root/rel
    if p.is_file() and not rel.endswith('.meta'): assert Path(str(p)+'.meta').exists(),('missing metadata',rel)
guids={}; collision=[]
for path in (root/'Assets').rglob('*.meta'):
    text=path.read_text(errors='replace'); match=re.search(r'^guid: ([a-f0-9]{32})$',text,re.M)
    if not match: continue
    value=match[1]
    if value in guids: collision.append([str(path),guids[value]])
    guids[value]=str(path)
assert not collision,collision
result={'baseline':base,'branch':git('branch','--show-current'),'authoredFrameCounts':counts,'uniquePixelHashes':len(hashes),'assetsGuidsChecked':len(guids),
    'existingFilesModified':changed,'existingMetaChanged':False,'protectedDataBossScenesChanged':False,'projectSettingsChanged':False,'result':'PASS'}
destination=root/'Docs/Enemies/CoreEnemies/StaticAudit.json'; destination.write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result,indent=2))
