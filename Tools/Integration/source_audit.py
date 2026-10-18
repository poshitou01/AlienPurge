"""Read-only source provenance; writes evidence only in this integration checkout."""
import hashlib, json, os, pathlib, subprocess, sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/Integration'
SOURCES = {'boss': pathlib.Path('D:/AlienPurge/AlienPurge'),
           'region': pathlib.Path('D:/AlienPurge_RegionLoopV01'),
           'enemies': pathlib.Path('D:/AlienPurge_CoreEnemies')}

def git(root, *args):
    env = dict(os.environ, GIT_OPTIONAL_LOCKS='0')
    p = subprocess.run(['git', '-C', str(root), *args], capture_output=True, env=env)
    if p.returncode: raise RuntimeError(p.stderr.decode('utf-8', 'replace'))
    return p.stdout

def capture(root):
    files = sorted(set(p.decode('utf-8') for p in git(root, 'ls-files', '-z', '--cached', '--others', '--exclude-standard').split(b'\0') if p))
    hashes = {p: hashlib.sha256((root/p).read_bytes()).hexdigest() if (root/p).is_file() else None for p in files}
    raw_status = git(root, 'status', '--porcelain=v1', '-z', '-uall').decode('utf-8')
    return dict(path=str(root), head=git(root,'rev-parse','HEAD').decode().strip(),
                branch=git(root,'symbolic-ref','--short','HEAD').decode().strip(),
                commonDir=git(root,'rev-parse','--path-format=absolute','--git-common-dir').decode().strip(),
                status=raw_status.split('\0')[:-1], sha256=hashes)

OUT.mkdir(parents=True, exist_ok=True)
if sys.argv[1] == 'capture':
    evidence = {name:capture(root) for name,root in SOURCES.items()}
    (OUT/'Sources.before.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding='utf-8')
    for name, info in evidence.items():
        root = SOURCES[name]
        patch = git(root,'diff','--binary','HEAD')
        (OUT/(name+'.uncommitted.patch')).write_bytes(patch)
        changed = git(root,'diff','--name-only','HEAD').decode().splitlines()
        print(name, info['head'], 'status',len(info['status']),'content diffs',len(changed),'files',len(info['sha256']))
        print('non-art changes:',[p for p in changed if '/Art/' not in p and '/Animations/' not in p])
elif sys.argv[1] == 'verify':
    before=json.loads((OUT/'Sources.before.json').read_text(encoding='utf-8'))
    results={}
    for name,root in SOURCES.items():
        after=capture(root); old=before[name]
        differences=[p for p in set(old['sha256'])|set(after['sha256']) if old['sha256'].get(p)!=after['sha256'].get(p)]
        results[name]=dict(headUnchanged=old['head']==after['head'],branchUnchanged=old['branch']==after['branch'],statusUnchanged=old['status']==after['status'],fileCount=len(after['sha256']),changedFiles=differences)
    (OUT/'Sources.after.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
    print(json.dumps(results,indent=2))
