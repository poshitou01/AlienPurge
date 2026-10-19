"""Read-only comparison of every protected, tracked file against the pre-production baseline."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib,json,subprocess
ROOT=Path(__file__).resolve().parents[2];QA=ROOT/'TapTap_ReleaseAssets/10_QA'
base=json.loads((QA/'BASELINE.json').read_text('utf-8-sig'))
allowed={'.gitignore','Packages/manifest.json','Packages/packages-lock.json'}
def inspect(pair):
    path,expected=pair;f=ROOT/path
    if not f.exists():return {'path':path,'status':'MISSING'}
    h=hashlib.sha256()
    with f.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    actual=h.hexdigest()
    if actual!=expected:return {'path':path,'status':'CHANGED','expected':expected,'actual':actual,'allowed_tooling_change':path in allowed}
with ThreadPoolExecutor(max_workers=4) as pool:changed=[x for x in pool.map(inspect,base['sha256'].items()) if x]
bad=[x for x in changed if not x.get('allowed_tooling_change',False)]
result={'source_commit':base['commit'],'tracked_files_checked':len(base['sha256']),'changes':changed,'unexpected_changes':bad,
 'new_files':subprocess.check_output(['git','ls-files','--others','--exclude-standard'],cwd=ROOT,text=True).splitlines(),
 'protected_files_unchanged':not bad}
(QA/'PROJECT_PRESERVATION.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),'utf-8')
print(json.dumps({'tracked_files_checked':len(base['sha256']),'unexpected_changes':bad,'intentional_changes':[x['path'] for x in changed]},ensure_ascii=False))
