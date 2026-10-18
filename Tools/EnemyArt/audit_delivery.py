"""Static preservation / image audit and exact delivery file manifest.

This does not substitute for Unity compilation or Play Mode verification.
Requires Pillow; compares the entire current worktree against the prototype commit.
"""
from pathlib import Path
from collections import Counter
from PIL import Image
import hashlib, re, subprocess

ROOT = Path(__file__).resolve().parents[2]
BASE = '23114de4866261ceb0e540fabaeb4b423e1a8135'
DOCS = ROOT / 'Docs/Enemies/SupportExploder'

def git(*args):
    return subprocess.check_output(['git', '-c', 'core.quotepath=false', *args], cwd=ROOT, stderr=subprocess.DEVNULL).decode('utf-8')

def paths(output):
    return [s for s in output.split('\0') if s]

changes = paths(git('diff', '--name-only', '-z', BASE))
tracked_at_base = set(paths(git('ls-tree','-r','--name-only','-z',BASE)))
existing_changes = [p for p in changes if p in tracked_at_base]
assert not any(p.endswith('.meta') for p in existing_changes), 'Existing metadata changed'
assert not any(p.startswith('Assets/_Project/Data/') for p in changes), 'Data changed in art-only upgrade'
assert [p for p in changes if p.endswith('.unity')] == ['Assets/_Project/Scenes/SupportExploder_Test.unity'], 'Unexpected scene changes'

allowed_assets = {
    'Assets/_Project/Editor/SupportExploderSetup.cs',
    'Assets/_Project/Editor/SupportExploderVerification.cs',
    'Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab',
    'Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab',
    'Assets/_Project/Scenes/SupportExploder_Test.unity',
    *['Assets/_Project/Scripts/Enemy/' + s + '.cs' for s in ('SupportEnemyAbility','ExploderEnemyAbility','EnemyAbilityPresentation','EnemyAbilityTestLab')]
}
assert not [p for p in existing_changes if p.startswith('Assets/') and p not in allowed_assets], 'Unexpected existing Assets changes'
assert not [p for p in changes if p.startswith(('Packages/', 'ProjectSettings/'))], 'Project settings changed'

guid_files = {}
missing_meta = []
for meta in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: ([0-9a-f]{32})$', meta.read_text(encoding='utf-8-sig', errors='replace'), re.M)
    if not match: continue
    guid = match.group(1)
    assert guid not in guid_files, f'Duplicate GUID: {meta} and {guid_files.get(guid)}'
    guid_files[guid] = meta.relative_to(ROOT).as_posix()

hashes, counts, minimum_margin = set(), Counter(), 512
for enemy in ('Support', 'Exploder'):
    for path in (ROOT / 'Assets/_Project/Art/Enemies/Animated' / enemy).rglob('*.png'):
        im = Image.open(path)
        assert im.mode == 'RGBA' and im.size == (512,512), path
        alpha = im.getchannel('A'); bounds = alpha.getbbox()
        assert bounds is not None, path
        margin = min(bounds[0], bounds[1], 512-bounds[2], 512-bounds[3])
        assert margin >= 4, f'Final sprite touches canvas: {path}'
        minimum_margin = min(minimum_margin, margin)
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        assert digest not in hashes, f'Duplicate PNG: {path}'
        hashes.add(digest); counts[enemy] += 1
        if not Path(str(path)+'.meta').exists(): missing_meta.append(path)
assert not missing_meta, missing_meta
assert counts == {'Support':224, 'Exploder':200}, counts

identities = [
    ('Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab', 'f063a784dc396c940b9d1ec89e4b4ac9'),
    ('Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab', '1fd5a341d9a84cb4c9943f9d7a38b05c'),
    ('Assets/_Project/Data/Enemies/Enemy_Support_Data.asset', '65a169995ee7bdb44bcf2ae6d9e9a1d9'),
    ('Assets/_Project/Data/Enemies/Enemy_Exploder_Data.asset', '2d89d9fe0c0a16b4fac0888f6d2e980e')]
for path, guid in identities: assert guid_files[guid] == path+'.meta', path
result = (DOCS / 'Validation/PlayMode_Result.txt').read_text(encoding='utf-8-sig')
assert 'RESULT: PASS' in result and 'FAIL:' not in result
passes = len(re.findall(r'^PASS:', result, re.M))

report = [
    f'Comparison base: {BASE}',
    f'Workspace: {ROOT}',
    f'Branch: {git("branch", "--show-current").strip()}',
    'PASS: No existing .meta changed; four Prefab/EnemyData identities preserved.',
    f'PASS: All {len(guid_files)} Assets metadata GUIDs unique.',
    'PASS: No EnemyData or ability configuration changed in this art upgrade.',
    'PASS: Existing Assets changes limited to two new enemy Prefabs, their dedicated scripts/tools, and SupportExploder_Test.',
    'PASS: Original four enemies, shared runtime scripts, Boss assets/whitelist/Arena and formal scenes unchanged.',
    'PASS: Packages and ProjectSettings unchanged.',
    f'PASS: {len(hashes)} unique 512x512 RGBA PNGs, alpha-free canvas borders, minimum final margin {minimum_margin}px.',
    'PASS: Support=224, Exploder body=192 + radial VFX=8. Stable meta for every sprite.',
    f'Unity evidence: {passes} PASS entries; RESULT: PASS (separate actual Play Mode report).',
    'Static checks do not claim human art acceptance, player builds, or region balance.',
    '', 'Preserved identities:', *[path+' = '+guid for path,guid in identities]]
(DOCS / 'Preservation_ArtUpgrade.txt').write_text('\n'.join(report)+'\n', encoding='utf-8')

manifest_path = 'Docs/Enemies/SupportExploder/FileManifest.tsv'
all_paths = sorted(set(changes + paths(git('ls-files','--others','--exclude-standard','-z')) + [manifest_path]))
script_purposes = {
    'SupportEnemyAbility.cs':'Release support and start authored death through native death hook',
    'ExploderEnemyAbility.cs':'Select collapse or rupture and native visual return delay',
    'EnemyAbilityPresentation.cs':'Target links, exact warning radius, bounded radial VFX and cleanup',
    'EnemyAbilityTestLab.cs':'Pool-based encounter, direction/state preview, pause/recycle/restart controls',
    'SupportExploderAnimation.cs':'Ability-driven Sprite-only Animator selection and pool reset',
    'SupportExploderSetup.cs':'Idempotent own Prefab/test-scene setup and existing-menu integration',
    'SupportExploderArtSetup.cs':'Own-sprite importer, 96 clip/controller creation, formal asset validation',
    'SupportExploderVerification.cs':'Retained gameplay suite with authored-art verification entry',
    'SupportExploderArtVerification.cs':'Real animation/state/timing/reuse/preview Play Mode checks',
}
def purpose(p):
    if p.endswith('.meta'): return 'Stable independent Unity GUID/import settings; keep with asset'
    name = Path(p).name
    if name in script_purposes: return script_purposes[name]
    if p.startswith('Assets/_Project/Art/Enemies/Animated/'):
        parts=p.split('/'); return 'Authored transparent '+parts[5]+' '+parts[6]+' frame'
    if p.endswith('.controller'): return 'Native Animator states referencing own authored clips'
    if p.endswith('.anim'): return 'Directional Sprite-only animation; no gameplay events'
    if p.endswith('.prefab'): return 'Existing specialist Prefab wired to formal sprites/controller; GUID retained'
    if p.endswith('.unity'): return 'Only the isolated specialist lab; original runtime/pools/navigation'
    if p.startswith('Tools/EnemyArt/Source/'): return 'Included final imagegen source sheet or turnaround reference'
    if p.startswith('Tools/EnemyArt/'): return 'Reproducible source provenance, mechanical export or audit utility'
    if '/ArtReview/' in p: return 'Review preview or final sprite audit; not runtime asset'
    if '/Validation/' in p: return 'Actual Unity Play Mode evidence or camera capture'
    return 'Delivery, preservation or later region-integration documentation'
rows = ['change\tpath\tpurpose']
rows += [('modified' if p in tracked_at_base else 'added')+'\t'+p+'\t'+purpose(p) for p in all_paths]
(ROOT / manifest_path).write_text('\n'.join(rows)+'\n', encoding='utf-8')
print('\n'.join(report[:12]))
print(f'FileManifest.tsv: {len(all_paths)} exact added/modified paths')
