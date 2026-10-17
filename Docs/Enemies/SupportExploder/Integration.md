# Support / Exploder 区域接入说明

本交付仅接入 `Assets/_Project/Scenes/SupportExploder_Test.unity`。区域任务负责最终地图、区域池、任务增援和难度验收。本分支没有改 Level01_FullWorld_GameplayV02、正式 Arena、旧敌人 Prefab 或 Boss 配置。

## 基线与合入边界

- 基线是实际本地 Boss 提交 `609c7ff523b8f6a04db9e978bbd994eb6d155655`，不是较旧的 `origin/main`。
- 开发目录：`D:\AlienPurge_SupportExploder`；分支：`feature/enemies-support-exploder`。
- 核对时区域分支 `feature/level01-region-loop-v02` 与本分支的公共祖先是 `5c8e0cef9449aa722e6f32c35c3269aa7f0d1f67`，不是 `609c7ff`。区域任务需另行保留/整合 `5c8e0ce` 之后的 Boss 验收改进；单独挑选本任务的新敌人提交不会顺带包含那些 Boss 提交。
- 不复制整个 Assets，不覆盖已有 .meta。通过交付提交进行审查与合入；合入操作由区域任务统一执行，本任务没有自动合并或推送。
- 既有枚举值保持 Normal=0、Fast=1、Heavy=2、Ranged=3；新增 Support=4、Exploder=5。检查区域分支是否也新增了 4/5，发生冲突时先统一序列化编号，不按数组索引猜类型。

## 正式行为配置与临时美术

| 类型 | EnemyData | Prefab | 能力配置 |
|---|---|---|---|
| Support | `Assets/_Project/Data/Enemies/Enemy_Support_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab` | `Assets/_Project/Data/Enemies/Support_Ability.asset` |
| Exploder | `Assets/_Project/Data/Enemies/Enemy_Exploder_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab` | `Assets/_Project/Data/Enemies/Exploder_Ability.asset` |

行为和数据可以供区域试接入；**美术仍为 TEMP**。两个 Prefab 有独立 GUID，使用独立临时 Animator/clip 资产，借用现有 Swarmer 八方向步行帧，叠加程序绘制器官。它们不是已经完成的新敌人八方向动画。美术缺项与替换规范见 [Delivery.md](Delivery.md)。

## 注册与生成

1. 在目标场景唯一的 `EnemyPool.poolEntries` 中分别添加 Support/Exploder，`enemyType` 与对应 Prefab 的 `PooledEnemy.Type`、`EnemyDefinition.Data.Type` 必须一致。注册仅表示可生成，不代表允许随机选择。
2. 将类型明确加入目标区域的候选名单。沿用原 `EnemySpawnEntry` 时设置 `enemyType`、`enemyPrefab`、`unlockTime`、`spawnWeight`；自定义区域选择器也必须使用显式候选名单，不能枚举所有 EnemyType 或所有注册 Prefab。
3. 沿用原 `EnemyPool.GetEnemy(type, position, Quaternion.identity)`，随后在同一帧调用 `EnemyDefinition.InitializeFromGlobalDifficulty(globalHP, globalSpeed, globalDamage)`。不直接 Instantiate，不调用 EnemyData 的写入接口。
4. 生成点继续通过现有 `EnemySpawnPositionValidator`、可走节点和 Environment clearance 检查。Exploder 推荐离玩家至少 6 米，禁止任务脚下生成；其内建安全条件是出生至少 1.5 秒，并在本次出生后观察到玩家距离至少 4.5 米，才允许接近触发。安全检查不是位置验证的替代品。
5. 支援目标与玩家目前都使用 **Default 物理 Layer**，而不是名称相似的 Sorting Layer。Support 的 `allyLayers=1`，Exploder 的 `victimLayers=1`；若区域任务改了物理分层，必须同步配置并回归。

建议预热 Support=2、Exploder=3。区域首测可关闭这两类池的扩容，以池容量作为额外保护；生成器仍应检查 `GetActiveCount(type)`，其中包含尚未完成死亡回收的实例。不能只限制单个区域的数量而忽略正在运行的任务增援。

## 推荐节奏与数量

| 项目 | Support | Exploder |
|---|---|---|
| 首次出现 | 玩家已见过普通与远程单位后；第二阶段区域或约 90 秒后 | 玩家已学会 Dash / 地面预警后；第二阶段区域或约 120 秒后 |
| 初始权重参考 | 5（相对普通单位约 40） | 4（相对普通单位约 40） |
| 常态同时存在上限 | 1 | 2 |
| 压力段硬上限 | 全场 2 | 全场 3；同时进入预警建议最多 2，需生成器负责节奏 |
| 增援安排 | 至少有 2 个旧类普通敌人陪同，每批最多 1 | 每批最多 1，批次间隔至少 4 秒，不在狭窄安全点或交互点附近刷出 |

这是接入起点，不是已经在完整地图验证的最终平衡。不要让主区域刷怪与任务增援各自拥有一份独立上限；两者应共享全场预算。封锁、撤离读条、拾取交互等需要停留的任务，首版不要同时投放 Support 和 Exploder，也不要叠加无逃生路线的地面机制。

## 玩法与生命周期契约

**Support**：半径 4.5 米，最多 3 个目标，伤害 +1。整数伤害原本为 1 时变为 2，并非百分比微增。多个来源取最大值。首版只强化 Normal/Fast/Heavy/Ranged，不强化 Support、Exploder 或 Boss。优先靠近友军后方，玩家 5 米内尝试退让；没有友军时保持距离。目标切换每 0.2 秒，伤害读取时另外即时校验距离、生命和池生命周期。已有 Spitter 子弹按发射时伤害快照继续飞行；撤销增益不会追溯改写已经发射的子弹。

**Exploder**：距离玩家 2.4 米内启动 1.5 秒预警；固定爆点，停止原生移动，冻结步行动画。实际爆炸半径 3 米，与地面圈一致；按碰撞体是否与圆相交判命中，每个 PlayerHealth 最多结算一次。基础伤害取本次生成后的接触伤害 ×2；仅命中带 Player Tag 的 PlayerHealth，普通敌人和 Boss 都不受伤。地面爆炸可由原生 Jet Jump 躲避；Dash 保留原生位移/无敌规则。当前 Player Prefab 的 Dash 默认没有无敌帧，需朝圈外冲刺。V1 为完整圆形范围，不做墙体遮蔽，因此墙后仍在圈内也会受伤。不要把墙当作该技能的避伤条件。

主动自爆走 `EnemyHealth.TakeDamage(CurrentHealth)` 的原生死亡链，计一次击杀、掉一次经验；预警中被击杀取消爆炸，也按原生死亡奖励。纯回收不爆炸、不掉落、不计击杀。禁用和再次出生清空旧倒计时、特效和目标；Support 的租约以目标启用代次校验，目标复用不继承旧增益。

回收仍调用 `PooledEnemy.ReturnToPool()`，不要只 SetActive(false) 绕过池集合。换局/Restart 应卸载旧场景，或先通过池回收所有敌人。两个能力也会在 GameManager 离开 Playing 时撤销增益/取消未完成预警。

## Boss 保护与公共文件冲突

`HiveTyrantCombatConfig.summonChoices` 保持原有四项；`HiveTyrantSummoning` 按此名单选取，而不是遍历池注册。不要把新的区域候选池反写进 Boss 名单。

现有公共文件仅修改三处：

- `Assets/_Project/Scripts/Enemy/EnemyType.cs`：在末尾添加 4/5。
- `Assets/_Project/Scripts/Enemy/EnemyMovement.cs`：新增 `SetNavigationTarget(Transform)`，供 Support 提供导航目标；位移、碰撞和寻路算法保持原路径。
- `Assets/_Project/Scripts/Enemy/EnemyContactDamage.cs`：`Damage` 返回基础值加运行时增益，接触伤害使用该读取口；新增默认开启的 `canDealContactDamage`，仅新 Prefab 配置为关闭（Unity 的物理消息也可能发给禁用组件，单独禁用不足以禁止接触伤害）。旧 Prefab 行为保持原样。Fast 冲锋、Heavy 冲击和 Spitter 发射本来就读取 `Damage`。

新 `EnemySupportBuff` 只在被支援的实例上按需添加，没有修改旧敌人 Prefab。EnemyData、EnemyPool、PooledEnemy、EnemyHealth、EnemyPathNavigator、EnemyAnimationController、EnemyYSorting、Boss 脚本均保留原文件。

## 接入后复测

运行菜单 `AlienPurge/Support + Exploder/4 Run Play Mode Verification`；再在区域任务的正式接入场景确认可走节点、同时存在上限、玩家生成距离、任务增援预算与 Boss 原名单。不要仅以独立测试场景通过替代最终地图验收。
