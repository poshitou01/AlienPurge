"""Record the exact delivery delta and file purposes, without changing assets."""
from pathlib import Path
import csv,subprocess,collections

root=Path(__file__).resolve().parents[2]
base='727645fb7ce2acce8910ecf1d053522b394c7afc'
def git(*args):
    return subprocess.check_output(['git','-C',str(root),*args]).decode('utf-8').split('\0')
paths={p for p in git('diff','--name-only','-z',base) if p}
paths.update(p for p in git('ls-files','--others','--exclude-standard','-z') if p)
baseline={p for p in git('ls-tree','-r','--name-only','-z',base) if p}
manifest='Docs/Enemies/CoreEnemies/FileManifest.csv'
paths.add(manifest)

def purpose(path):
    if path.endswith('.meta'): return 'Unity GUID/import metadata for the paired new asset or folder'
    if '/Art/Enemies/' in path: return 'Authored transparent body animation frame'
    if '/Animations/' in path: return 'Sprite-only clip or eight-direction Animator controller'
    if '/Prefabs/' in path: return 'Existing prefab art/animation references and white hit flash; identity retained'
    if '/Scenes/' in path: return 'Independent native-pool combat and animation test scene'
    if path.endswith('CoreEnemyAnimation.cs'): return 'Native-state-driven sprite presentation, death and Tank impact boundary'
    if path.endswith('EnemyAttackVisualSample.cs'): return 'Read-only shared ability presentation sample'
    if path.endswith('CoreEnemyArtLab.cs'): return 'Independent scene combat, preview and Restart controls'
    if path.endswith('EnemyContactDamage.cs'): return 'Honor disabled component state in collision callbacks'
    if '/Scripts/' in path: return 'Read-only native skill stage/direction/radius queries'
    if path.endswith('SupportExploderSetup.cs'): return 'Keep one death presenter when cloning a missing specialist prefab'
    if path.endswith('CoreEnemyTestProfileIsolation.cs'): return 'Editor-only isolated save namespace and settings restoration for enemy labs'
    if '/Editor/' in path: return 'Idempotent configuration/import checks or real Editor Play Mode verification'
    if '/Source/' in path: return 'Final imagegen source sheet, one enemy/direction'
    if '/Prompts/' in path: return 'Final generation/edit prompt and provenance'
    if '/References/' in path: return 'Read-only original art reference/audit used for matching'
    if path.endswith('.py'): return 'Reproducible slicing, capture encoding or delivery audit tool'
    if '/Validation/' in path: return 'Actual Unity Play Mode report, capture, capture timing or encoded GIF'
    if '/ArtReview/' in path: return 'Exported frame audit or asset-level preview (not a Unity capture)'
    return 'Delivery specifications, integration instructions or exact file inventory'

out=root/manifest
with out.open('w',encoding='utf-8-sig',newline='') as f:
    writer=csv.writer(f); writer.writerow(['change','path','purpose'])
    for path in sorted(paths): writer.writerow(['M' if path in baseline else 'A',path,purpose(path)])
counts=collections.Counter('M' if p in baseline else 'A' for p in paths)
print(dict(counts),len(paths),'files in',manifest)
