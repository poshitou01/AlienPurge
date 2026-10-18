# 普通敌人动作与技能 V2

工作目录：`D:\AlienPurge_CoreEnemies`。分支：`feature/enemies-core-art-animation`。
基线：`577e01a394a7c920b94e70af14531bef5f98c61d`（四种旧敌人正式外观与动画）；继承 Support / Exploder 正式版本 `727645fb7ce2acce8910ecf1d053522b394c7afc`。

## 试玩

Unity 菜单 `AlienPurge > Enemy Polish V2 > 3 Play Six Enemy Test Scene`。
场景仍为 `Assets/_Project/Scenes/CoreEnemies_Art_Test.unity`；默认通过现有 EnemyPool 生成六种敌人。
F1 重新生成六敌遭遇，F2 轮换六种单挑，F3 回收，F4 击杀，F9 Restart。
F5 切换原有四敌动画预览，F6 方向，F7 动作，F8 暂停，F11 半速。
配置菜单 `1 Configure and Validate` 可重复执行，保持一个测试管理器和一个原生对象池。

## 动作修正

- 六个现有 Animated Prefab 增加 `EnemyMotionPresentation`，关闭这些 Prefab 上旧的无缓冲朝向控制，保持其它 Prefab 与 Boss 的原控制器。
- Rigidbody2D 使用 Interpolate；读取实际位移，不另建移动或寻路系统。
- 朝向采用速度平滑、10°方向迟滞、0.1 秒最短切换间隔；明显反向允许立即响应。停止保留 0.14 秒，减少边界与物理帧之间反复切换。
- 行走相位来自路程，换方向、Support 开关支援时保持步态。暂停不推进，再生清空旧相位。
- `MotionRegistration.json` 对 1,152 张身体帧配置自定义导入 Pivot。以岩甲主体为参考修正行走帧的漂移；技能/死亡采用每个方向的固定偏移，保留动作本身的位移。PNG 像素、原 GUID、512 画布、PPU 256、碰撞根节点、Sorting 与材质均保留。
- 这次没有重新绘制动画。仍是原有八方向、每方向八张独立行走帧；锚点校正不会消除原画本身的形变差异，也不把插值或旋转计作新动画帧。

校正表可用现有美术工具目录下的 `python Tools/EnemyArt/register_motion.py` 重新计算（Pillow 11.3），随后执行 V2 配置菜单导入。脚本只分析原 PNG 并写校正表，不改 PNG 像素。录像导出工具为 `Tools/CoreEnemyArt/encode_polish_captures.py`。

## 技能参数与交互

| 敌人 | V2 行为 |
| --- | --- |
| Swarmer | 保留接触攻击，统一移动与转向表现。 |
| Rusher | 0.85 秒蓄力，橙色冲锋走廊、箭头与推进线；方向锁定。宽度读取现有碰撞体，长度读取速度 × 持续时间，并按现有障碍层裁切。冷却 3.8 秒，仍由原生冲锋逻辑造成一次伤害。 |
| Spitter | 0.95 秒蓄力，绿色瞄准线与源点倒计时弧；锁定吐弹方向。弹速 8，仍是一次原生对象池弹体，保留视线检查。瞄准线是方向提示，不是弹体最大射程边界。 |
| Tank | 3 单位触发，1 秒橙色震地预警，最大半径 3.8；0.6 秒向外传播的地面波，波宽 0.3，随后 0.7 秒恢复、4.8 秒冷却。前沿扫过玩家碰撞体时结算一次，已通过的中心区域安全。离开圈或 Jet Jump 可躲避；蓄力中死亡/回收取消。 |
| Support | 14 单位支援半径、最多 8 个原有四类普通敌人，+1 伤害；18 单位寻找友军，保持距玩家 6 单位并处于友军后方。紫色目标圈、传递光点和低透明连线显示实际受益对象。多个来源取最大值，不叠加；不支援 Boss、Support、Exploder。 |
| Exploder | 移速倍率 1.3，2.2 单位触发、3.6 半径、1.15 秒蓄爆。外圈固定，内圈与填充随倒计时推进，不使用频闪。1.2 秒出生保护，且必须曾与玩家相距至少 4.8 单位。蓄爆时锁定位置和刚体约束，击杀可打断。 |

Support 保留范围边界，未改为跨完整地图的光环。整张大地图的无条件强化会让尚未进入战斗的区域也依赖远处敌人；14 单位覆盖当前战区，同时保留玩家拉开距离解除增益的选择。

Tank / Exploder 都是地面伤害。Exploder 只影响 PlayerHealth + Player Tag，按配置 LayerMask 查询；原有两倍接触伤害、出生保护、死亡/回收区别、单次爆炸及单份 XP 保留。Tank 的新波只在 Animated Tank 上启用，旧 Heavy Prefab 默认继续原技能。

## 区域任务接入

本版本没有修改 `Level01_FullWorld_GameplayV02`、正式 Arena、Boss 配置或其它工作目录。请区域任务在自己的分支统一整合。

仍使用以下现有 Prefab，GUID 不变：

| 类型数值 | EnemyData | Animated Prefab |
| --- | --- | --- |
| Normal = 0 | `Assets/_Project/Data/Enemies/Enemy_Normal_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Normal_Swarmer.prefab` |
| Fast = 1 | `Assets/_Project/Data/Enemies/Enemy_Fast_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Fast_Rusher.prefab` |
| Heavy = 2 | `Assets/_Project/Data/Enemies/Enemy_Heavy_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Heavy_Tank.prefab` |
| Ranged = 3 | `Assets/_Project/Data/Enemies/Enemy_Ranged_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Ranged_Spitter.prefab` |
| Support = 4 | `Assets/_Project/Data/Enemies/Enemy_Support_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab` |
| Exploder = 5 | `Assets/_Project/Data/Enemies/Enemy_Exploder_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab` |

对象池继续显式 `EnemyPool.poolEntries` 注册上述类型→Prefab。区域刷怪池单独显式加入类型，不枚举全部注册项。两种专精技能参数仍位于 `Support_Ability.asset`、`Exploder_Ability.asset`。

推荐前期 Swarmer；随后单独引入 Rusher/Spitter 预警；中期再引入 Tank；中后期使用 Support 与 Exploder 组合。建议同一活动战区 Support 1 个、难度提高后最多 2 个；Tank 最多 2 个；Exploder 同时最多 3 个、同时蓄爆最多 2 个（后者需区域生成节奏安排，能力脚本没有跨敌人的全局仲裁）。

任务增援应从玩家视野边缘或有效导航点进入；Exploder 至少距玩家 4.8 单位并保留出生保护，不要在玩家脚下直接投放。首次 Support 与普通敌人一同出现，避免单独出现无法展示价值；狭窄任务点不要同时释放多只 Tank/Exploder。Boss 召唤仍只使用验收时的 Normal/Fast/Heavy/Ranged 四项名单。

Boss 若引用同一组普通敌人 Prefab，召唤物也会使用新版普通敌人技能；Boss 自身技能、召唤名单及数量未改。本次验证了实际召唤的类型与注册兼容性，没有重新替代完整 Boss 战的人工难度验收。

## 验证

真实 Unity 2022.3.62f3c1 Editor Play Mode 结果见同目录 `Core/PlayMode_Result.txt` 与 `SupportExploder/PlayMode_Result.txt`。包含原生对象池、寻路、伤害、死亡、经验、重复生成、Restart、Boss 召唤白名单，以及边界转向、距离步态、锁定预警、扩散波、扩大光环的专项检查。

2026-10-18：Core + V2 专项 496 项通过，Support / Exploder 回归 296 项通过，无 Unity Error / Exception。死亡淡出和录像检查使用固定模拟步长，防止离线截图耗时跳过短暂的动画阶段；实际状态机、物理和对象池仍在真实 Editor Play Mode 执行。

实际截图与 Tank 移动/转向逐帧录像位于 `Core/`；锚点测量见 `MotionRegistration_Report.txt`。性能和主观流畅度还应在最终区域敌人数、目标机器帧率下复测，本阶段只集成独立测试场景。

## 公共脚本改动范围

运行时修改：`CoreEnemyAnimation.cs`、`SupportExploderAnimation.cs`、`FastChargeAttack.cs`、`Attacks/HeavyShockwaveAttack.cs`、`Attacks/RangedEnemyAttack.cs`、`SupportEnemyConfig.cs`、`SupportEnemyAbility.cs`、`ExploderEnemyAbility.cs`、`EnemyAbilityPresentation.cs`；独立测试控制器修改 `CoreEnemyArtLab.cs`。

新增：`EnemyMotionPresentation.cs`、`EnemyGroundTelegraph.cs`。
编辑器增加锚点导入、V2 配置菜单和专项验证；原有配置/验证适配新 Pivot 与技能时长。
`EnemyType` 数值、`EnemyData` 类、EnemyPool、EnemyHealth、伤害入口、经验、移动/寻路实现、Boss 脚本和召唤名单均未更改。
