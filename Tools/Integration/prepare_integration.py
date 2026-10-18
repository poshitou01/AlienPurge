import pathlib, subprocess, shutil, uuid, json, re
root=pathlib.Path(__file__).resolve().parents[2]
assert root == pathlib.Path('D:/AlienPurge_IntegratedV01')
source=pathlib.Path('D:/AlienPurge_CoreEnemies')
entries=subprocess.check_output(['git','-C',str(source),'status','--porcelain=v1','-z','-uall']).decode().split('\0')
copied=[]
for entry in entries:
    if not entry: continue
    path=entry[3:]
    if not (path.startswith('Assets/') or path.startswith('Docs/Enemies/')): continue
    assert (source/path).is_file(),path
    target=root/path
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source/path,target)
    copied.append(path)
(root/'Docs/Integration').mkdir(parents=True,exist_ok=True)
(root/'Docs/Integration/ImportedWorkingFiles.json').write_text(json.dumps(copied,indent=2),encoding='utf-8')

def edit(path,old,new):
    p=root/path; raw=p.read_bytes(); a=old.encode(); b=new.encode()
    # Preserve the original byte encoding (some legacy comments are GBK).
    ending=b'\r\n' if b'\r\n' in raw else b'\n'
    a=a.replace(b'\n',ending); b=b.replace(b'\n',ending)
    assert a in raw,path+' missing expected text'
    p.write_bytes(raw.replace(a,b))

edit('Assets/_Project/Scripts/Enemy/EnemyContactDamage.cs',
'''<<<<<<< HEAD
        if (PlayerAbilityFeel.AvoidsGroundHit(playerHealth)) return;

        playerHealth.TakeDamage(damage);
=======
        playerHealth.TakeDamage(Damage);
>>>>>>> 051073378dab7a723b94d50d5c82f85d05a8ca78''',
'''        if (PlayerAbilityFeel.AvoidsGroundHit(playerHealth)) return;

        playerHealth.TakeDamage(Damage);''')
edit('Assets/_Project/Scripts/Enemy/EnemyContactDamage.cs','    public int Damage =>','    public int BaseDamage => damage;\n    public int Damage =>')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionEnemyLease.cs',
     'GetComponent<RangedEnemyAttack>() };','GetComponent<RangedEnemyAttack>(), GetComponent<SupportEnemyAbility>(), GetComponent<ExploderEnemyAbility>() };')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionEnemyLease.cs','priorDamage = contact.Damage;','priorDamage = contact.BaseDamage;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionDefinition.cs','public int simultaneousLimit;',
     'public int simultaneousLimit;\n        [Min(0)] public int globalSimultaneousLimit;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionDefinition.cs','public string id;\n        [Min(0)] public float delay;',
     'public string id;\n        [Min(0)] public float weight = 1;\n        [Min(0)] public float delay;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionV02Settings.cs','public RegionWave[] environmentSquads = Array.Empty<RegionWave>();',
     'public RegionWave[] environmentSquads = Array.Empty<RegionWave>();\n        public bool weightedEnvironmentSquads;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionLoopController.V02.cs','        void TickV02()\n        {',
     '        void TickV02()\n        {\n            if (TickIntegratedBoss()) return;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionLoopController.V02.cs','var wave = choices[state.environmentWave % choices.Length];',
     'var wave = SelectEnvironmentWave(state);')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionLoopController.V02.cs','                var profile = state.config.Profile(ticket.type);\n                var position = home.transform.position;',
     '                var profile = state.config.Profile(ticket.type);\n                if (!HasTypeCapacity(state, profile)) continue;\n                if (profile.special && CountAlive(state, true) >= state.config.specialLimit) continue;\n                var position = home.transform.position;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionLoopController.V02.cs',
     '            if (profile.simultaneousLimit > 0 && state.enemies.Count(t => t.type == ticket.type && t.lease != null && !t.dead) >= profile.simultaneousLimit) return false;',
     '            if (!HasTypeCapacity(state, profile)) return false;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionLoopController.Feedback.cs',
     'var destination = Terminal(states[index + 1].config.regionId).transform.position;',
     'var destination = IntegratedNeedsBoss(state) ? world.World(new Vector2(0, -8)) : Terminal(states[index + 1].config.regionId).transform.position;')
edit('Assets/_Project/Scripts/Level01/RegionLoop/RegionLoopController.Feedback.cs',
     '            float remaining = Mathf.Max(0, FogBeginsAt(current) - Time.time);',
     '            if (complete && IntegratedNeedsBoss(current)) objective = "研究已完成 → 进入虫巢竞技场，击败首领";\n            float remaining = Mathf.Max(0, FogBeginsAt(current) - Time.time);')
edit('Assets/AlienPurge/Level01ConnectedV01/Runtime/L01ArenaBridge.cs',
     '            if(!encounterStarted&&(world.progression.Has("Research")||world.progression.previewAllDoorsOpen))',
     '            var loop=world.progression.regionLoop;\n            if(loop!=null&&loop.integratedWorld&&(GameManager.Instance==null||!GameManager.Instance.IsPlaying||world.player.GetComponent<PlayerHealth>().IsDead))return;\n            if(!encounterStarted&&(world.progression.Has("Research")||world.progression.previewAllDoorsOpen))')

scenes=[('Assets/_Project/Scenes/ArtTests/Level01_FullWorld_GameplayV02.unity','Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity'),
        ('Assets/_Project/Scenes/MainMenu_DeskV1.unity','Assets/_Project/Scenes/MainMenu_IntegratedV01.unity')]
for src,dst in scenes:
    assert not (root/dst).exists(), 'Refuse to replace existing scene'
    shutil.copyfile(root/src,root/dst)
    (root/(dst+'.meta')).write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n',encoding='utf-8')
print('Copied',len(copied),'working files individually; resolved contact damage; cloned V02 scene and menu with new GUIDs.')
