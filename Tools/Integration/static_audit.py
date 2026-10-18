import pathlib,re,json,hashlib,subprocess
root=pathlib.Path(__file__).resolve().parents[2]
out=root/'Docs/Integration'
guid_paths={}
for p in (root/'Assets').rglob('*.meta'):
    m=re.search(rb'^guid: ([0-9a-f]{32})',p.read_bytes(),re.M)
    if m: guid_paths.setdefault(m[1].decode(),[]).append(p.relative_to(root).as_posix()[:-5])
duplicates={g:paths for g,paths in guid_paths.items() if len(paths)>1}
known=set(guid_paths)
cache=root/'Library/PackageCache'
if cache.exists():
    for p in cache.rglob('*.meta'):
        m=re.search(rb'^guid: ([0-9a-f]{32})',p.read_bytes(),re.M)
        if m: known.add(m[1].decode())
missing={}
suffixes={'.unity','.prefab','.asset','.mat','.anim','.controller','.overrideController'}
for p in (root/'Assets').rglob('*'):
    if not p.is_file() or p.suffix not in suffixes:continue
    data=p.read_bytes()
    if not data.startswith(b'%YAML'):continue
    for guid in set(re.findall(rb'guid: ([0-9a-f]{32})',data)):
        g=guid.decode()
        if g not in known and not g.startswith('0000000000000000'):
            missing.setdefault(g,[]).append(p.relative_to(root).as_posix())
boss=pathlib.Path('D:/AlienPurge/AlienPurge')
prefixes=('Assets/_Project/Scripts/Boss/','Assets/_Project/Art/Boss/','Assets/_Project/Animations/Boss/','Assets/_Project/Prefabs/Enemies/Boss/','Assets/_Project/Data/Boss/')
protected=[];different=[]
for path in subprocess.check_output(['git','-C',str(boss),'ls-files','-z']).split(b'\0'):
    if not path:continue
    name=path.decode()
    if not name.startswith(prefixes):continue
    protected.append(name)
    a=(boss/name).read_bytes();b=(root/name).read_bytes() if (root/name).exists() else b''
    # Git working trees may use CRLF or LF; other bytes must remain identical.
    if a.replace(b'\r\n',b'\n')!=b.replace(b'\r\n',b'\n'):different.append(name)
scenes=['Assets/_Project/Scenes/HiveTyrant_Arena.unity','Assets/_Project/Scenes/ArtTests/Level01_FullWorld.unity']
arena={s:dict(byteIdentical=(boss/s).read_bytes()==(root/s).read_bytes(),normalizedIdentical=(boss/s).read_bytes().replace(b'\r\n',b'\n')==(root/s).read_bytes().replace(b'\r\n',b'\n')) for s in scenes}
target='Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity'
seen=set();queue=[target];reachable_missing={}
while queue:
    name=queue.pop()
    if name in seen:continue
    seen.add(name);p=root/name
    if p.suffix not in suffixes or not p.is_file():continue
    for raw in set(re.findall(rb'guid: ([0-9a-f]{32})',p.read_bytes())):
        g=raw.decode()
        if g in guid_paths:queue.extend(guid_paths[g])
        elif g not in known and not g.startswith('0000000000000000'):reachable_missing.setdefault(g,[]).append(name)
result=dict(guidCount=len(guid_paths),duplicateGuids=duplicates,unresolvedGuids=missing,integratedDependencyCount=len(seen),integratedMissingGuids=reachable_missing,bossProtectedCount=len(protected),bossDifferences=different,acceptedArenaAndFullWorld=arena)
(out/'StaticAudit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='unresolvedGuids'},indent=2))
print('Unresolved GUIDs:',len(missing))
for g,paths in missing.items(): print(g,paths[:3])
