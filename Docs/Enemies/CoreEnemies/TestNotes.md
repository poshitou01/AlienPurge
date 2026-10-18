# 验证说明

环境：Windows，Unity 2022.3.62f3c1，URP 2D，D3D11，独立 `D:\AlienPurge_CoreEnemies` 工作目录。以本目录的最终 `Validation/PlayMode_Result.txt`、`Validation/SupportExploder_Regression.txt` 为实际结果依据。

四种旧敌人的最终完整运行：411 个 PASS，RESULT: PASS；编译与资源配置通过，无 Unity 运行错误、异常或断言。批处理日志保留在本地忽略目录 `Logs/CorePlayMode4.log`，可重复执行的验证代码及报告随交付保存。

原有 Support/Exploder 的完整验证运行：296 个 PASS，RESULT: PASS；包含自身动画、支援增益及清理、主动爆炸/击杀/回收区别、对象池复用及 Restart。日志在 `Logs/SpecialistRegression.log`。两次最终批处理都正常完成；退出时 PlayerSettings 已恢复，无配置差异。

## 编辑器与资源

配置工具在自动验证开头连续执行两次，检查管理器和对象池无重复。检查 736 个正式 Sprite、168 个 Sprite-only 动画片段、四个旧 Prefab 的类型、数据、寻路组件、控制器和精灵引用。所有动画无 Animation Event，无 Transform 或 Collider 曲线。

素材独立审计逐张检查 RGBA、512 尺寸、Alpha、留白、唯一像素哈希与 `.meta`；同时检查 Assets GUID 唯一、既有 `.meta` 不变、正式场景与数据不变、既有文件仅修改允许的五个公共脚本及四个 Prefab。详见 `StaticAudit.json`。

## 实际 Play Mode

- 四类八方向运动选择、每方向八张独立行走帧、其他状态自身方向资源。
- 原生受击闪白、武器池子弹碰撞扣血、A* 路径与障碍碰撞、实际经验拾取。
- 每类同一池对象连续三次死亡/回收/再生；重复致死命中只结算一次击杀和经验；暂停期间死亡计时冻结；提前回收不被旧延迟回调影响。
- Rusher、Tank、Spitter 的实际原生准备、释放、收势，暂停、半速，真实伤害一次，Sprite 动作不改变碰撞根，准备阶段击杀立即停止能力和预警。
- Tank 的瞬时冲击边界使用原生 3.2 世界单位伤害半径；Spitter 只发射一颗原生对象池弹丸。
- 更新后的四类对象仍可接受 Support 增益，来源回收时撤销；原生 Boss 实际生成召唤计划与单位，在六类都注册时仍限原四类。
- 八方向预览进入/退出不影响后续战斗；Restart 重新加载一个场景、一个池和四个敌人，清空旧动画、死亡和击杀状态。

技能截图使用 Unity 的 `Time.captureDeltaTime = 1/60` 进行离线逐帧捕获，确保 PNG 编码卡顿不会跨过 Spitter 0.2 秒收势。行为、物理、伤害均在实际 Editor Play Mode 执行；其他运行检查使用正常实时步进。结束或失败时恢复捕获时钟及时间倍率。`*_Live/timing.csv` 记录缩放后的游戏时间和真实状态，GIF 按这些间隔编码，未合成插帧。

## 验证中定位并处理的问题

1. Rusher 原生冲锋禁用了 EnemyContactDamage，但 Unity 仍会把碰撞事件发给禁用的 MonoBehaviour。首次实测同一帧发生接触伤害与冲锋伤害。现在由公共伤害入口检查 `isActiveAndEnabled`，不改变数值和原生冲锋结算入口。
2. 验证工具曾把用于武器测试的攻击组件禁用，死亡回池后又在后续测试中默认它已启用。修复测试夹具为每次生成显式配置所需组件状态，没有为此修改敌人玩法。
3. 首次截图时 PNG 编码造成 0.333 秒帧间隔，跳过 Spitter 的短收势。改用上述离线捕获时钟，保留原生 0.85 秒准备、0.4 秒恢复参数。
4. Editor 的原生 SaveManager 会在结束 Play Mode 时保存。增加仅限两个敌人测试场景的存档隔离，路径为 `%USERPROFILE%/AppData/LocalLow/AlienPurgeTests/EnemyArt_AlienPurge_CoreEnemies/`，退出恢复 PlayerSettings。最初未隔离的测试触发过一次默认存档保存；随后只读对比主存档与自动备份，JSON 字段一致，未覆盖或回滚共享存档。

## 交付边界

真实 Play Mode 自动验证与截图并不替代用户对动作自然度和最终美术的审美验收。未在正式区域或 Arena 中执行地图整合；未做目标设备或大量敌人压力测试。旧数据、地图、Boss 外观/技能/召唤名单及其他工作目录保持原样。
