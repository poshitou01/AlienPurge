# Level01 Region Loop V02

工程：`D:\AlienPurge_RegionLoopV01`。分支：`feature/level01-region-loop-v02`。基于 V01 提交 `35775fbfaa9a64671c4556811fe6ae37061508f1`。Unity：2022.3.62f3c1。

本轮在 V01 的三个区域内接通驻守、持续增援、可选防守、高风险箱、Storm、推进毒雾和事件成长。没有合并或推送分支，没有写入并行 Hive Tyrant 工作目录。

## 打开与运行

1. 在 Unity Hub 打开上述区域玩法工程。
2. 执行 `AlienPurge/Gameplay/Level01/Install Region Loop V02`。安装器打开并保存 `Assets/_Project/Scenes/ArtTests/Level01_FullWorld_GameplayV02.unity`。
3. 执行 `AlienPurge/Gameplay/Level01/Validate Region Loop V02`，然后 Play。
4. 自动行为检查入口：`AlienPurge/Gameplay/Level01/Run Region Loop V02 Regression`。运行前保存自己的场景修改；测试期间不要手动操作这个 Editor。

已有 V02 会增量连接，保留已摆放对象和独立配置的调参。配置位于 `Assets/_Project/Data/Level01/RegionLoopV02/`。安装器不会保存未保存的源场景，也不会覆盖未知归属的目标场景。V01 和其他旧场景均保留。V02 已追加到 Build Settings，Restart 返回 V02。

## 基础配置

以下是首轮调参值，不是平衡结论。S/F/T 分别为 Swarmer / Rusher / Tank。

| 项目 | ColonyEntry | CargoYard | PowerMaintenance |
|---|---|---|---|
| 驻守 | 8 S，其中 2 个开门守卫 | 6 S + 2 F，其中 2 S 为开门守卫 | 5 S + 2 F + 1 T，其中 2 S 为开门守卫 |
| 环境小队循环 | 3 S；4 S；3 S | 4 S；3 S+1 F；2 S+2 F | 3 S+1 F；3 S+1 T；2 S+2 F |
| 单只经验 S/F/T | 1 / — / — | 2 / 2 / — | 2 / 3 / 4 |
| 单只血量 S/F/T | 3 / — / — | 4 / 5 / — | 4 / 6 / 16 |
| 区域存活上限 | 16 | 16 | 16 |
| 普通人口上限 / 任务预留 | 10 / 6 | 10 / 6 | 10 / 6 |
| 特殊敌人上限 | 0 | F 同时最多 2 | F 最多 2，T 最多 1，共最多 3 |
| 小队间隔 / 实际低压后的搜索窗 | 12s / 12s | 12s / 12s | 12s / 12s |
| 首次驻守清场奖励 | 2 XP | 3 XP | 4 XP |
| 主线奖励 | 2 XP | 6 XP + 第 1 核心选择 | 8 XP + 第 2 核心选择 |
| 主线与出口 | Arrival → G1 | Beacon → G2 | Power → G3 |
| Storm 最早首次进入后 | 42s | 42s | 42s |
| Storm 预警 / Active / Recovery | 至少 10s、按实际路径加长 / 12s / 3s | 同左 | 同左 |
| 安全区半径 | 4 | 4 | 4 |
| 毒雾探索 / 告警 / 本段推进 | 120 / 30 / 70s | 150 / 30 / 80s | 180 / 30 / 90s |
| 主线首次完成额外缓冲 | 20s，一次 | 20s，一次 | 20s，一次 |

全局池活动上限为 28，普通来源最多使用 22 个名额。达到上限只延后当前小队，不积压多轮欠账。普通小队没有终身总预算；当前区域被毒雾完全收回后停止。任务和 Storm 转移阶段暂停普通补充，现存敌人继续行动。完成普通小队后，只有存活压力降到 ≤2 且该轮普通小队已清理，才开始下一段完整搜索时间。

24 个驻守点分散布置；靠近出口 32 单位内准备下一区驻守。尚未进入的区域不启动停留时钟。远区保留本局死亡与剩余血量，池回收不算死亡。驻守感知使用视距、距离、区域和原生寻路；不会隔墙全区追击。

## 任务与高风险箱

| 任务 | 可接取期限，首次进入后 | 累计驻守 | 接取后期限 | 有限增援 | 完成额外 XP | 尝试次数 |
|---|---:|---:|---:|---|---:|---:|
| Entry 可选防守 | 90s | 18s | 50s | 3 S；10s 后 2 S | 10 | 1 |
| Cargo 可选防守 | 90s | 21s | 55s | 3 S；10s 后 1 F | 14 | 1 |
| Power 可选防守 | 90s | 24s | 60s | 3 S；10s 后 1 F | 18 | 1 |
| Cargo 高风险 | 110s | 24s | 65s | 3 S+1 F；12s 后 3 S+1 F | 18 + High-Risk 箱 | 2 |

任务必须在毒雾到达前留出完成期限及领奖离开时间：普通至少 35s，高风险至少 40s，因此实际可接取窗口可能早于表中期限关闭。主动按 E 接取；活动时再按 E 放弃。离开范围只暂停驻守进度，期限继续；所有任务敌人实际死亡后才能完成。一次只接受一个可选战斗任务，主线设备一直独立可用。可选 Beacon 完成不会写入 Cargo 的 `Beacon` 主线里程碑。

附近可接任务使用原倒计时行显示 `ACCEPT` 的有效剩余窗口（已扣除毒雾前的完成与领奖时间），风险行分别标出 `hold` 驻守秒数和 `limit` 接取后期限；高风险同时标出 Mixed。接取后倒计时行切为 `DEADLINE`，目标行显示已累计/所需驻守时间，避免把三种时间混为一个进度数值。

右上角使用原生 MissionHUD 和箭头：活动可选任务优先，结束后恢复主线。保留原有布局、尺寸、颜色配置、字体样式、背景、进度条和层级，只更新文字、值和可见性。V01 增加的 RegionLoopHUD 在 V02 关闭，没有叠加第二套任务面板。邻近可选目标显示接取时间、驻守时间、完成期限和奖励。

原 Beacon Prefab 的设备精灵引用为空，本次仅在 V02 实例补接已有 Ground_Device 和目标箭头；修正范围圈、出生预警和环境覆盖在地板下方的排序层。毒雾使用独立材质副本适配工程现有 2D Shader，并接回已有 SoftMist 贴图，保留原配色和原生几何、粒子逻辑。主线计时标签改为短文字 `FOG IN`，避免原面板宽度内换行。通过 G3 到达 QuarantineCommons 后原任务箭头结束，不再指向身后的 Power 区域。

| 掉落规则 | 普通 FieldSupplyCrate | V02 独立 High-Risk 表 |
|---|---|---|
| 随机抽取次数 | 2–5 | 4–5，另有 1 件保底 |
| 稀有及以上总权重占比 | 100 / 1515，约 6.60% | 91 / 98，约 92.86% |
| Rare / Epic / Legendary 单项权重 | 原表 45、40 / 8、6 / 1 | 25、25 / 18、18 / 5 |
| 最低保障 | 沿用原容器生成规则 | 独立 guaranteedEntries 中至少 1 件 Rare Advanced Circuit（2×1）；先安排保底占格，再安排随机项 |
| 物品数量 | 原表数量范围 | 保留每项原数量范围，保底 1；相同物品可能合并堆叠 |
| 容器网格 | 原容器尺寸 | 奖励箱实例 6×5 |

保底、次数、权重、数量都可编辑。普通共享表和共享箱 Prefab 未修改。高风险箱成功前不激活；首次打开后内容固定，重开不会重掷；搜索、识别、背包格子与可放置规则仍由原生容器处理。没有恢复旧 330 秒高风险事件或撤离时间表。

## 经验供给与进化

先做了 V01 实际死亡和经验球拾取审计，未复现掉落或拾取失效。V02 保持每次升级依次需要 5、10、15……XP；Lv2–Lv7 累计阈值为 5、15、30、50、75、105。没有直接设置等级，没有击杀同时直接加 XP。仅 V02 的独立经验球 Prefab 将吸附半径从场景原引用的 1.5 调到 3.25，仍需接近并实际拾取。

| 区域 | 驻守掉落 | 三轮环境战斗 | 首清 | 主线 | 本区合计 | 累计 / 目标 | 可选额外，含任务敌人 |
|---|---:|---:|---:|---:|---:|---|---|
| Entry | 8 | 10 | 2 | 2 | 22 | 22 / Lv3 | 普通 +15 |
| Cargo | 16 | 24 | 3 | 6 | 49 | 71 / Lv5 | 普通 +22；高风险 +34 |
| Power | 20 | 29 | 4 | 8 | 61 | 132 / Lv7 | 普通 +27 |

表中为三轮小队全部成功出生并拾取的名义供给，不是强制战斗数量，也不是进门等级限制。Storm 或任务优先时会取消尚未出现的普通增援，实际经验按真实击杀与拾取计算，不补发；已跑通的三轮路线得到 22/71/132，另一次受转移影响为 21/70/131，均对应 Lv3/5/7。快速只清驻守与主线、不等待环境小队时，累计为 12 / 37 / 69 XP，即 Lv2 / Lv4 / Lv5。额外停留可以继续刷普通经验，但第一进入时间不会重置，毒雾最终收回区域。

Cargo、Power 主线分别通过 `mechanism/<region>` 发一次核心选择。配置可改为首次进入，但同一份奖励只采用一种来源。旧 45/150/240 秒入口在 V02 关闭。模块实际写入构筑后通过 `ModulesChanged` 触发原生进化判断：

- Piercing + ChainLightning：立即 ThunderPiercer，顺序不限。
- Explosive + SplitShot：立即 ClusterBurst，顺序不限。
- 其他两模块：队列追加一次原生 Overclock；全部升满时走原生兜底。

不要求模块满级。进化与这次超频互斥；进化显示等当前选择关闭，实际武器状态已立即改变。普通升级先于同次奖励的核心选择入队；暂停、搜索、背包和其他选择不会被错误解除。

## 复用系统与新增接口

| 原系统 | 本次适配 |
|---|---|
| V01 RegionLoop / RegionDefinition / RegionRunState | 原控制器增加显式 V02 模式和配置；统一驻守、增援、任务、天气、搜索和毒雾节奏 |
| L01World / L01Progression / L01Gate / RegionTerminal | 原唯一里程碑来源与门禁接口；关键守卫死亡后 E 立即激活；不使用可选任务事件开主线门 |
| EnemySpawner / EnemyPool / PooledEnemy / EnemyHealth | 原池与真实死亡掉落；旧全局 spawner 时间表关闭；沿用 V01 实例属性与所有权租约 |
| MissionManager / BeaconMissionObjective | 同一 MissionManager 增加一个区域可选实例槽，与显示槽分离；复用原 Beacon 范围、累计、视觉和交互 |
| AlienCoreRecovery / L01Terminal 旧实现 | 已审阅；主线采用可靠的 RegionTerminal→L01Progression 接口，不强行用掉落核心事件开门 |
| PlanetStormController / SafeZoneController | 增加外部发布已验证安全区的入口，之后运行原 Warning/Active/Recovery 状态机、伤害、音效、特效及 HUD |
| L01SafeZoneBridge | 活动区域风暴锁定后跳过旧搬移，避免预警后安全区漂移 |
| ToxicFogRouteController / Area / Ground / Front / Mist / HUD / Damage | 外部累计距离推进，复用原横截面覆盖、渲染、伤害与提示；独立路线沿房间和弯道收回后方 |
| PlayerExperience / UpgradeManager / RegionRewardQueue | 原经验曲线、经验球和升级按钮；队列增加原生 Overclock 请求 |
| PlayerWeaponModifiers / ModuleSelection / WeaponEvolutionController | 模块写入事件，小范围 opt-in；原进化定义、枪械、Bullet、视觉和音效继续生效 |
| LootContainer / LootTableData / ExtractionController / HighRiskCacheHUD | 独立掉落表的可配置保底；解锁已有箱实例并登记到原生高风险方向提示，保留搜索和背包 |

原生脚本扩展使用同一 partial 类，默认模式保持原行为；新脚本中的 partial 接口不是第二套管理器。所有 V02 调度标志均在场景中序列化，早于 Awake，未靠场景名称判断模式，未禁用核心单例。

Storm 发布前检查整个安全圆盘（含 0.35 单位边缘余量）的物理碰撞，再对半径内三圈和中心共 73 个点做可走空间检查，并用玩家当前位置的实际 A* 路径估计转移时间；预警不少于路径耗时 + 5s。当前区域毒雾安全余量不足则延后，记录原因；发布后固定位置。任务活动期间不发布普通 Storm。毒雾距离是受控累计量，不用总生存时间反算，暂停恢复不会补跳。房间宽覆盖与连接路横截面共用原毒雾伤害和美术；弯道额外截面避免道路边缘留下空楔。

## 验证证据与范围

- `ExperienceBaseline.txt`：V01 原链路审计。
- `Installation.txt`、`Validation.txt`：Unity 编译后安装、重复安装及编辑器结构检查。
- `NativeHudStyle.txt`：源场景原生任务面板的布局和样式基线；验证器逐项比较位置、尺寸、字体样式、图片及滑条。
- `PlayMode.txt`：真实 Unity Play Mode 自动行为结果。
- `Presentation.txt` 和本目录 PNG：场景渲染专项检查与原始运行截图。
- `TaskPreview.txt`、`Optional_Preview.png`、`HighRisk_Preview.png`：原面板内的任务接取预览。
- `LegacyPlayMode.txt`：旧 V01 与原 FullWorld 的行为检查。
- `ProtectedBaseline.json`：806 个既有场景、Prefab、配置、材质、动画及对应 meta 的 Git blob 基线，安装和测试后校验。
- `ChangedFiles.txt`：完整交付文件清单。

自动回归在初始化前选择独立 `AlienPurge_RegionLoopV02_Regression` / `AlienPurge_RegionLoopV02_LegacyAudit` 存档，不使用正式玩家档。旧场景审计只在内存中替换测试字体引用，结束时丢弃这些测试修改，从不保存旧场景。

行为测试使用真实池死亡、物理经验球拾取、原生升级和机制按钮、容器搜索取物与枪械射击；用确定性伤害和传送控制战斗/移动输入，并加速游戏时间。为覆盖指定组合，首个机制卡池未提供所需机制时测试会重新抽取原生合法候选，仍通过原按钮选择；未绕过经验、模块写入或进化条件。公共机制 API 与满级兜底另作边界测试，已在报告中区分。

完整行为回归已通过：三轮普通战斗路线测得累计 22/71/132 XP、Lv3/5/7；两种签名组合的正反顺序均在 240s 前触发，实际子弹命中与连锁/爆炸分裂生效。普通与高风险任务、搜索取物、Storm 三阶段及内外伤害、毒雾房间和弯道覆盖及实际伤害、任务超时后主线、暂停/死亡/Restart 均有断言。旧 V01 在 380s 保持有限巡逻与旧隔离行为；原 FullWorld 保持 45s 第一次机制选择；806 项资产和 meta 哈希一致。

渲染复查确认：HighRisk_Defense 中设备与驻守圈可见；HighRisk_Search 中原搜索格子和实际物品可见；Storm_Active 中原安全圈、倒计时与 SAFE 提示正常。随后修复了毒雾材质和计时文字；最终 `Presentation_Main.png`、`Fog_Front.png`、`Fog_Inside.png` 分别记录正常任务面板、推进边界与收回区域覆盖。该专项沿真实区域时钟等待毒雾，没有强制设置毒雾位置。

补充覆盖检查已沿正常主线推进至 Power，并等待三个区域完整退役：Entry / Cargo / Power 分别通过 20 / 48 / 54 个有效可走采样点，弯折连接路和 G3 通道两侧也均在收回范围内。最终区域截图见 `Fog_Power.png`。

测试不等于人工通关或数值平衡；实际瞄准难度、移动手感、完整音画表现和长时间背包取舍仍需试玩。Boss 内容通过保护校验，本轮未重测 Boss 技能。

## 两条试玩路线

**A：跳过可选任务。** Entry 清理驻守并接近拾取经验球，清完一轮环境敌人后利用约 12s 空档搜索箱子；激活 Arrival 开 G1。Cargo、Power 同样推进，分别选择两个机制。选 Pierce+Chain 或 Explosive+Split 可立即进化，Power 剩余驻守和后续环境小队可用于体验新武器，再通过 G3。每区约三轮环境战斗可对照 Lv3/5/7 的供给结果；快速路线不强制等待。

**B：主动承担任务。** 在主线之外接取标有 OPTIONAL 的原 Beacon，观察离圈暂停驻守而期限继续。Cargo 找 HIGH RISK 目标，确认接取/完成期限与毒雾余量后挑战；成功搜索解锁的高风险箱并收取物资，再激活或通过 G2。Storm 预警时沿原方向提示进入固定安全区；毒雾告警后及时离开，不要用往返尝试重置时钟。
