# Hive Tyrant 本轮交付

状态：**首版逻辑完成；工程接入完成；实际 Editor Play Mode 自动检查通过；正式动作素材未完成，因此不标记 Boss 正式完成。** 首轮数值仅用于测试。没有制作 Exploder / Support，也没有修改撤离、Storm、毒雾、背包或普通敌人 AI。

## 1. 备份与分支

- 修改前已确认 Unity 处于 Edit Mode，保存并正常退出。
- 备份绝对路径：`D:\AlienPurge\AlienPurge_Backup_PreHiveTyrant_20261017_152640`。
- 6260 个源文件，2,591,856,316 字节；每个文件的大小和 SHA256 全部匹配，包括源素材、全部 `.meta`、Git 元数据与未入库文件。仅排除工程根级 Library / Temp / Logs / obj；清单和原始校验报告位于备份目录。
- 原分支 `feat/level01-integration`，原 HEAD `e2ea18910c7cf3a04d8bd7b1c3494cafa9dff1fd`，原工作区干净；本轮分支 `codex/hivetyrant-combat`。
- 未推送、未合并 main，未移动远端备份分支。项目内保存校验副本 `HiveTyrant_Backup_Verification.json`。

## 2. 文件位置与用途

以下路径相对实际项目根目录 `D:\AlienPurge\AlienPurge`，完整改动文件清单见 `HiveTyrant_ChangedFiles.txt`。

| 位置 | 作用 |
| --- | --- |
| `Assets/_Project/Scripts/Boss/HiveTyrantBossMotor.cs` | 原位完善八方向、步态延续、动作控制权、碰撞检测与死亡优先级；保留已有外部接口 |
| 同目录 `HiveTyrantCombat.cs` / `HiveTyrantCombatConfig.cs` | 单一战斗调度、技能、阶段、召唤归属、重置和可调配置 |
| 同目录 `HiveTyrantTelegraph.cs` / `HiveTyrantSummonOwner.cs` | 临时填充区域预警及对象池召唤归属 |
| 同目录 `HiveTyrantCombatDebug.cs` / `HiveTyrantHealthBar.cs` / `IEnemyDeathPresentation.cs` | 测试控制、读取原 EnemyHealth 的血条和死亡表现接口 |
| `Assets/_Project/Scripts/Enemy/EnemyHealth.cs` | 最小可选接口接入：Boss 同步中断并等待完整死亡表现；普通敌人保留原奖励与回池流程 |
| `Assets/_Project/Data/Boss/HiveTyrant_Combat.asset` | 集中配置生命、技能、阶段和召唤测试数值 |
| `Assets/_Project/Editor/HiveTyrantCombatSetup.cs` | 可重复配置导入参数、现有 Clip / Animator、Prefab、测试场景及正式场景引用 |
| `Assets/_Project/Editor/HiveTyrantCombatVerification.cs` | 真实 Unity Editor Play Mode 集成检查和截图；不引入测试插件 |
| `Assets/_Project/Prefabs/Enemies/Boss/HiveTyrant.prefab` | 原 Prefab 接入战斗、配置、血条；保留 GUID |
| `Assets/_Project/Animations/Boss/HiveTyrant/Clips/Death/` 与 `Clips/Hit/` | 原片段保留身份；死亡改为 8fps，受击元数据统一 12fps |
| `Assets/_Project/Scenes/HiveTyrant_CombatTest.unity` | 现有 Player、场地、Boss、相机、管理器及真实对象池的专用测试场景 |
| `Assets/_Project/Scenes/ArtTests/Level01_FullWorld.unity` | 只配置原 `L01World.boss` 指向的活动 Boss 和战斗边界，继续使用现有关卡桥接 |
| `Docs/Boss/` | 备份、配置、素材缺口、测试结果、截图及本报告 |

原 `HiveTyrant_Arena.unity` 内容保持不变；正式场景原有 739 个 Transform 的位置、旋转、缩放全部保持不变；没有改动已有 `.meta`。原先未激活的 `HiveTyrant_Boss` 预览对象继续保持未激活。参见 `HiveTyrant_PreservationCheck.json`。

## 3. 最终行为与测试参数

| 项目 | 当前行为 / 配置初值 |
| --- | --- |
| 追踪与技能选择 | 速度 1.7，停止距离 4.2，接战距离 18；实际碰撞体 Cast 阻挡墙壁并尝试局部绕行；遵守已有场地 footprint；轮换可用技能，受距离和冷却约束 |
| 扫击 | 半径 6、110° 填充扇形，前摇开始即锁定方向和位置；1 秒预警后一次扣 12，释放显示 0.18 秒，收招 0.75 秒，冷却 4.5 秒；多个玩家碰撞体不重复计伤，墙壁阻挡伤害 |
| 震地 | 半径 7 的填充圆盘；1.55 秒预警后一次扣 18，释放 0.22 秒，收招 1 秒，冷却 8 秒；保留原相机震动反馈 |
| 召唤 | 使用现有 Normal / Swarmer 对象池；每波 3、同时活着最多 5、本场最多 12、最小间隔 14 秒；1.4 秒落点预警，收招 0.8 秒 |
| 召唤落点 | 距 Boss 5～10，距玩家至少 3，净空半径 0.6，最多尝试 32 次；须在原场地内，有通向 Boss 的净空走廊，并避开墙、敌人及玩家；释放时再次校验，无落点则跳过且不消费预算 |
| 召唤属性 | HP 4、移动速度 2、接触伤害 5；只清理本 Boss 持有归属的召唤物，池中归属释放后不误删其他系统复用的对象 |
| Phase2 | 原 EnemyHealth 降到 50% 且仍存活时只进入一次；取消未释放技能，切换已确认 RiftWing；转换 1.8 秒，无回血；扫击新增第二段，间隔收招 0.75 秒，第二段独立重新锁方向并预警 0.85 秒 |
| 技能衔接 | 技能独占动作与移动，统一额外恢复间隔 1.1 秒；普通受击保留原闪白反馈，不反复打断技能 |
| 生命与死亡 | HP 600；继续使用同一个 EnemyHealth，原子弹/穿透/爆炸/连锁/分裂链路直接生效；致死同步停止移动、取消预警和待召唤、清理本场召唤物，Death 动作不会被 Idle 覆盖；至少保留 1.6 秒且不短于死亡 Clip 后清理 |
| 血条与关卡 | 血条读取同一生命和阶段，复用现有头像/框素材；正式场景继续由原 L01ArenaBridge 入口激活与观察击杀完成，不切换胜利场景 |

生命、范围、伤害、时长、冷却、召唤等在配置资产中调整；Motor 的移动速度和停止距离保留在原组件中。当前绕行是保守局部避障，复杂凹障碍的全局寻路不在本轮范围。

## 4. 正式素材、临时表现与缺口

复用现有 Phase1 Idle / Move / Hit / Death 片段与八方向资源，以及已导入并实际查看的 RiftWing Phase2 Idle / Move。保留现有画布、中心 pivot、256 PPU、视觉层位置、阴影与身体比例。移动方向切换延续当前 normalized gait，不重回第 1 帧。受击主要使用原 EnemyHealth 闪白；正式技能期间不插播受击片段。

**临时表现：**三个技能和转换期间冻结当前阶段对应的中性身体姿势，配合真实填充扇形/圆盘、释放变色及震地反馈；不将此称为爪击、抬手、砸地的正式身体动作。Phase2 暂时沿用 Phase1 的受击/死亡资源，缺少 RiftWing 对应片段。预警 Mesh 是逻辑验证表现，尚未完成正式 FX 的统一绑定。

已基于实际母版生成一次 DR 扫击六姿势样例并做视觉检查。结果存在不透明背景、跨格和落脚点/比例不一致，已拒收，未导入 Assets、未批量扩展或替代正式 Animator。记录见 `ArtReview/README.md` 和拒收图。

`HiveTyrant_ArtGaps.csv` 按动作、阶段和八方向记录 72 项缺口，包括已有参考路径、缺少姿势、循环规则、分动作帧数/时长、1024 RGBA 画布、固定锚点、释放时刻与 FX/阴影分层。主要缺口：两阶段扫击/震地/召唤，Phase2 转换、受击和死亡。正式动作全部到位前，比例一致性、攻击姿势连贯性和 RiftWing 死亡衔接仍不能宣称完成。

## 5. 实际验证与限制

使用本机 Unity **2022.3.62f3c1** 完成实际导入、编译与配置；使用 **Direct3D11 的真实 Editor Play Mode** 执行集成验证，结果见 `Validation/PlayMode_Result.txt`，不是把编译通过当作战斗通过。

最终一轮通过 **86 项断言**：八方向映射和步态延续、技能动作锁、前摇不提前扣血、扫击多碰撞体单次扣血、锁方向后绕后闪避、震地圆盘内/外判定、收招归还控制权、原武器及物理穿透/爆炸/连锁/分裂子弹、召唤波次/间隔/硬上限/总预算/无落点退出、禁用清理归属、普通敌人死亡/经验/回池再生、Phase2 中断/只进入一次/不回血/二段独立提示、致死跳过转换、死亡中断与完整延迟清理、新实例状态干净、F9 实际场景重开、真实追踪刚体被墙阻挡、隔墙扫击无伤害、重置回到原位置，以及正式 Level01 原入口激活、边界绑定、实际技能与原击杀完成桥接。

重复运行配置工具后，对 4719 个 Assets 文件逐一比较 SHA256，零变化、零新增，结果见 `Validation/RepeatSetup_Result.json`。原资源与布局保护记录见 `HiveTyrant_PreservationCheck.json`。

实际渲染截图位于 `Validation/`：Sweep_Warning、Slam_Warning、Summon_Warning、Phase2_RiftWing、Death、Level01_BossFight。已查看预警、RiftWing 与正式场景截图。相机截图不包含 IMGUI 血条；HUD 已接线，但尚未进行逐像素 UI 验收。

已知既有诊断：原 A* Editor HTTP 更新检查器报告 `InvalidOperationException: Insecure connection not allowed`，会触发 Console Error Pause。验证器保留该诊断，仅恢复这个明确的 Editor 检查导致的暂停；其他运行时 Error / Exception / Assert 均使结果失败。未修改该第三方插件或项目网络安全设置。

未完成/未验证：正式攻击与转换动画、Phase2 独立受击/死亡、最终战斗平衡、长时间人工完整关卡游玩、全部分辨率的相机构图与 HUD、独立打包版本。当前近距离镜头可能裁切 Boss 上部外轮廓；未改动原正式关卡相机规则，需要后续专门视觉验收。

## 6. 最少试玩步骤

1. 打开 `Assets/_Project/Scenes/HiveTyrant_CombatTest.unity`，按 Play。配置已保存，无须逐项挂脚本或补引用。
2. 测试场景默认关闭自动技能；原 Player 保持可移动/射击。走到 Boss 附近，用 F1 扫击、F2 震地、F3 召唤，观察前摇后躲避；F4 扣血到 Phase2 阈值，F5 致死，F6 切换自动技能，F9 或 Restart 按钮完整重开场景。忙碌、玩家死亡或召唤预算耗尽时，相应操作会按规则被拒绝。
3. 退出 Play 后，可运行菜单 `AlienPurge > Hive Tyrant > Configure and Validate Combat` 重新配置，或 `Run Play Mode Verification` 重跑自动检查。自动检查会切换场景并写入结果，运行前先保存其他工作。
4. 正式场景仍打开 `Assets/_Project/Scenes/ArtTests/Level01_FullWorld.unity`，沿原 Research → Boss 入口流程试玩；正式场景没有新增调试快捷键。

批处理入口保存在项目外 `D:\AlienPurge\Run-HiveTyrantUnity.ps1`；Setup / Verify 均拒绝并发 Unity 工程写入。现有场景和组件引用已实际保存。

## 7. 进入 Exploder / Support 的条件

应先补齐并验收单方向攻击样例，再扩展对应八方向与 RiftWing 片段；将释放时刻与现有逻辑预警对齐，完成比例/锚点/阴影/相机构图及血条人工验收，再跑回归并做完整战斗数值试玩。逻辑、正式素材、工程接入、实际验证四项全部满足后，才能将 Hive Tyrant 标为正式完成并进入 Exploder / Support。当前代码与接入可继续使用，剩余主要门槛是正式动作素材和视觉/平衡验收。
