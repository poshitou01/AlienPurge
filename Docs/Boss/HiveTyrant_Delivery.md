# Hive Tyrant 本轮交付与验收

2026-10-17；工程 D:\AlienPurge\AlienPurge；Unity 2022.3.62f3c1 / URP 2D。

**已交付可试玩、可检查的战斗版本；正式动作尚未完全达到要求，因此不宣称 Hive Tyrant 本轮制作完成。** 两阶段真实关节动作已接入，但部分方向的遮挡接缝仍不合格。所有 72 项素材需求保留，未以更改完成标记代替视觉验收。

| 项目 | 状态 |
| --- | --- |
| 技能逻辑 | F1 扩大范围、F2 前向三段波/真实缺口、F3 四类型原生 AI 已实现并实测 |
| 正式动作 | **部分完成，最终视觉验收未通过**：已有实际肢体运动；遮挡切口、小片脱离、死亡倒伏仍需完善 |
| 技能特效 | 斩击弧/碎屑、分段地面波、落点生成及核心转换反馈已实际接入 |
| 工程接入 | 原 Prefab、测试场景和原 Level01 桥接可运行；UI、身份和布局保护校验通过 |
| 实际验证 | 216 项完整 Editor 检查、正式关卡入口/三技能/击杀/镜头恢复通过；Windows 构建及独立两阶段战斗通过 |
| 平衡状态 | 完成首轮原玩家条件下可打调整；不是长期平衡或人工手感验收 |

## 1. 起点、保护、改动与恢复

实查起点为 codex/hivetyrant-combat / 7190c330e9cb543d587e775a44568c244da74715，工作区干净，无运行中的 Unity。没有回退对齐旧记录。

原完整备份 D:\AlienPurge\AlienPurge_Backup_PreHiveTyrant_20261017_152640 已核实：6260 文件、2,591,856,316 字节，原 SHA256 校验记录保留。本轮修改前快照 D:\AlienPurge\AlienPurge_Snapshot_HiveTyrant_Completion_20261017_172335：1968 文件、378,287,565 字节，逐文件 SHA256 全部匹配，含涉及文件及 .meta。工程内基线副本为 CompletionBaseline.json。

Unity 配置、保存、Play Mode 和构建顺序执行；未并行写场景/资源，未执行 reset/clean、main 合并或远端推送，未制作 Exploder / Support。主要变更见 HiveTyrant_ChangedFiles.txt：

- Boss Combat / Config / Motor / Telegraph，新增 WavePlan / GroundWave / Geometry / Summoning / Effects / Presentation / PoseLibrary / ArenaCamera / TestNavigation。
- 现有 Setup / Verification 扩展，新增 PoseAuthoring / SkinAuthoring / BuildVerification。
- Boss Prefab、战斗配置、关节资产、URP 材质、测试场景和正式场景 Boss 专用相机引用。
- 共享代码仅增加 PlayerMovement.MoveSpeed 只读访问及 EnemyPool.TryGetConfiguredPrefab 只读查询。未改原玩家操控、原 Player Prefab、共享 EnemyData、撤离、Storm、毒雾、背包或普通敌人的全局行为。

CompletionValidation/Preservation.json：Boss Prefab 的 10 个、测试场景 20 个、正式场景 1363 个原 Transform/RectTransform/Canvas/CanvasRenderer 序列化块未改变；1045 个快照范围内原 .meta 不变；血条脚本和原 Player Prefab SHA256 不变。Setup 不创建或重设用户 UI。CompletionValidation/RepeatSetup_Result.txt：对全部 4751 个 Assets 文件重复配置前后 SHA256 比较，零变化、零新增/删除。

恢复时先保存退出 Unity、另存本轮之后的新工作，再按快照清单逐文件恢复所需文件和配套 .meta；新增文件按本轮清单识别。快照不包含后来工作，不能直接覆盖之后修改。不要全工程 reset/clean。更早完整备份仅用于明确要回到初始状态的情况。

## 2. F1 / F2 / F3 行为与测试参数

集中配置：Assets/_Project/Data/Boss/HiveTyrant_Combat.asset。Boss HP 600，50% 且仍存活时一次进入 Phase2，不回血；转换 1.8 秒，死亡表现 1.6 秒，全局技能恢复间隔 1.1 秒。

| 技能 | 当前参数 |
| --- | --- |
| F1 Sweep | 半径 7、扇形 120°、伤害 1、前摇 1 秒、释放 0.18 秒、收招 0.75 秒、冷却 4.5 秒 |
| F2 Ground wave | 三段各宽 3 / 厚 1；脚前 3.3 起步、速度 8、距离 14、前摇 1.55 秒、伤害 2、身体释放 0.22 秒、收招 1 秒、冷却 8 秒 |
| F2 缺口 | max(1.65, 玩家真实碰撞体横向投影宽 × 1.8 + 0.35)；两条空通道；包含 0.25 秒反应余量，至少一条可走路线，否则取消 |
| F3 Summon | 每波最多 3、存活上限 5、预算 12、间隔 14 秒；预警 1.4 秒、释放 0.2 秒、收招 0.8 秒 |
| F3 候选 | Normal/Fast/Heavy/Ranged；权重 45/25/10/20；逐类型上限 5/3/1/2；各类专属倍率均为 1 |

伤害从建议起点 12/18 改为 1/2 的依据：原 Player Prefab 和正式场景实查只有 **5 HP**，原伤害起点会单击秒杀；测试场景此前的 100 HP 覆写掩盖了差异。本轮只移除已知测试覆写，恢复原 5 HP / 速度 5，没有通过加玩家生命、作弊移速或额外无敌完成正常战斗验证。

F1 整段预警锁方向、隔墙无伤、按 PlayerHealth 去重。Phase2 第二段重新瞄准，独立 0.85 秒预警。

F2 由同一 WavePlan 驱动预警、特效和实际几何，相对扫掠覆盖玩家与波面整个物理步，每波最多一次命中。墙体裁切有效长度、墙后无伤、经过后无持续伤害；释放后不追踪玩家，传播生命周期独立于身体收招。沿用 PlayerHealth 无敌规则，并遵循原腾空状态对地面攻击的豁免。死亡、禁用和重开清除实际伤害几何。旧圆盘字段仅保留序列化兼容，已不参与伤害或选技。

Phase2 保留 RiftWing，F2 不堆波数，F3 使用同一预算；转换取消未释放攻击/波面/待召唤，致死优先跳过转换。生命仍是原 EnemyHealth，原穿透、爆炸、连锁、分裂链路通过真实 Bullet 回归。

## 3. 召唤物不行动的根因与修复

先复现再修复，见 CompletionValidation/Summon_Before.txt：四类型目标已绑定、移动启用、锁解除、速度为正、刚体 simulated=true、导航组件存在，但 AstarPath.active=False、path=False，两秒内移动全部 0。**专用场景缺少 A* 导航图**是此次复现的根因，并非漏绑目标。

Setup 保存有效图配置，HiveTyrantTestNavigation 按真实 Environment 扫描；正式场景继续使用原 L01Navigation。召唤按各 Prefab 基础属性与原 EnemyData 调用原初始化接口，保留移动、特色攻击、奖励、池化，不每帧改 Transform 掩盖导航问题。

| 类型 | 实测 HP / 速度 / 接触伤害 | 首次与同一实例回池再生 |
| --- | --- | --- |
| Normal / Swarmer | 3 / 1.5 / 1 | 两轮各移动约 8.31，近身伤害、死亡、原击杀/经验奖励、回池通过 |
| Fast / Rusher | 2 / 2.25 / 1 | 两轮各移动约 8.51，原冲锋状态和伤害通过 |
| Heavy / Tank | 11 / 0.975 / 2 | 两轮各移动约 6.61，原重击状态和伤害通过 |
| Ranged / Spitter | 3 / 1.35 / 1 | 两轮各移动约 2.62，进入射程后实际发射原投射物并命中 |

按真实碰撞体和原视觉缩放计算净空，当前约 0.51 / 0.3825 / 0.668 / 0.65。先选类型再选点，检查场地、墙、玩家、敌人、待落点和 A* 连通性；释放时复检，失败不花成功召唤预算。候选选择有界。回池清归属，清理前复核 Owner，避免删除其他刷怪器复用的实例。

完整结果见 CompletionPass_Result.txt；最终逐类型上限、候选退让、释放时复检、旧归属复用及重开资源测试见 PolicyAndStress_Result.txt，均位于 CompletionValidation。

## 4. 动作与特效制作、来源和接入

复用原 1024 RGBA / 中心 pivot / 256 PPU 母版，保留八方向 Idle/Move Clip、Animator、步态延续、VisualRoot 比例和静态阴影。每阶段每方向独立关节/分区，共 16 份视角配置，不旋转单张图替代八方向。

关节数据为 Assets/_Project/Animations/Boss/HiveTyrant/HiveTyrant_PoseLibrary.asset；制作源为 Editor/HiveTyrantPoseAuthoring.cs、HiveTyrantSkinAuthoring.cs，运行时为 Scripts/Boss/HiveTyrantPresentation.cs。当前采用局部刚性三角片、软肩部权重及源 PNG 透明连通分区；临时读取 PNG，不改变源图/导入身份。交付源图引用、网格分区、关节与代码，没有虚构未导出的逐帧 PNG。

两阶段 Cue6/7/8 为扫击/地面波/召唤；Cue9 转换、Cue2 受击、Cue3 死亡使用 RiftWing 源图。Combat 统一控制 Motor 与表现所有权，接触关键点对齐调度释放时刻；原静态 Sprite 在关节动作期间隐藏，受击不抢占正在执行的技能，死亡优先且不闪回 Phase1。

特效在 HiveTyrantEffects.cs / HiveTyrantTelegraph.cs，材质为 Art/Boss/HiveTyrant/FX/HiveTyrant_VertexTint.mat：F1 局部蓄力亮色、短弧和碎片；F2 独立亮边、石块和薄尘，限制在危险段内；F3 紫灰落点预警与生成反馈；转换有核心/翼缘局部变化。一个 Boss 预建 10 个释放面、3 个波面、12 个预警面及 1 个身体网格，独立拥有并释放资源。未全局改 Bloom、曝光、相机规则或 UI。

新生成 DR 单姿势样例因角色比例/肢体一致性失败而拒收，存于 ArtReview/REJECTED_DR_Anticipation_20261017.png，未接入业务资源；上一轮被拒六格图亦未使用。官方参考提炼及出处见 CompletionReferences.md，没有复制角色/贴图。

## 5. 素材缺口与完成依据

HiveTyrant_ArtGaps.csv 原 72 个 Action/Phase/Direction 标识和顺序保留，增加 Method / Source / ExportResource / RuntimeState / ReleaseSeconds / AcceptanceEvidence；源文件均存在，保存后重新读取验证 72 项。旧 Slam 圆盘需求已替换为前向分段波。

**正式动作仍未通过。** D 后臂、R 后拳、UR 拳/翼交界、UL 后臂/髋部、L/DL 前肢遮挡在较大动作中存在切口或小片脱离；死亡保持正确阶段并完整播放，但倒伏力度和接缝仍需修整。U/DR 主轮廓较稳定，不由此推定全部连续帧合格。CSV 逐项标为 PARTIAL，具体图集与检查见 CompletionValidation/VisualReview.md。

当前输入是单层合成 PNG，原前臂后方的身体内容没有保存在这些像素中；权重和连通分区不能替代干净遮挡层补绘。生成样例也未通过一致性验收。需补完整躯干、前臂、后肢遮挡层，再沿现有时序/接口做逐方向完整播放验收。此次运行接入不能冒充该美术缺口已补齐。

## 6. Play Mode、场景、性能、构建与风险

- **Editor：** CompletionValidation/CompletionPass_Result.txt 有 216 个 PASS。覆盖扩大扫击、锁定/墙/去重、二段独立预警；F2 近/中/远两缺口正常走位（原速度、0.25 秒反应）、完整碰撞体边缘、到达前/经过后安全、两秒大跨步和相对跨越、全封路线取消、腾空和禁用清理；原武器链路、各类原生攻击/奖励/回池。
- **正式场景：** 实际加载 Level01_FullWorld，原 Research/入口激活既有 Boss，运行 F1/F2/F3，原 BossDefeated 桥接完成；未激活预览保持原样。Boss 上部完整入镜，死亡后恢复原相机尺寸/释放构图。这不是从开局到撤离的人手全关卡通关。
- **正常自动战斗：** 输入自动化仅驱动原移动和 WeaponManager.TryFire，未加速、加无敌、治疗或直接给 Boss 调试伤害。首轮 101.6 秒胜出剩 1 HP；完整回归轮 102.1 秒胜出剩 5 HP、658 次射击、1 次转换、12 只总召唤、31 次释放。出生组合会影响压力，尚未做广泛玩家样本/武器 Build 平衡。
- **Editor 性能：** i7-14700HX / RTX 4070 Laptop GPU，DX11；完整回归 10751 帧含截图，中位 9.18ms / P95 11.73ms（该轮为较早关节网格版本）。三轮重开资源维持 26 个 Boss 网格、3 个匹配材质条目（含资产/实例），无本 Boss 预警、波面、召唤、释放残留。GC 后托管 585.30 → 584.16 → 584.16 MB；Unity 总分配 1048.01 → 1049.38 → 1049.89 MB，包含编辑器/场景，不是单 Boss 内存。最终网格资源检查另见 PolicyAndStress_Result.txt。
- **Windows 构建：** WindowsBuild_Result.txt：Development Windows64 成功，0 Error，2 个项目原有 CS0414 未使用字段警告，52.08 秒，约 1.79 GB。只打专用测试场景，原 Build Scenes 未改。产物 D:\AlienPurge\Builds\HiveTyrantCompletion\AlienPurge.exe，包含最终关节候选版本。
- **独立运行：** CompletionValidation/Standalone/Standalone_Result.txt 为 PASSED。原 5 HP / 速度 5，106.6 秒（含结束等待）胜出剩 5 HP，647 次射击、1 次转换、12 只总召唤、32 次释放，零运行错误，死亡后 EncounterEffects 根对象为 0。隐藏窗口帧时间中位 0.28ms / P95 26.36ms，受窗口调度影响，不能换算成前台正常 FPS；含采图帧，未验证低配/其他平台。
- **视觉：** PoseAtlases 是受控运行姿态采样；DR_Sweep_PlayMode.gif 是一次实际扫击每约 0.08 秒的连续采帧；Battle 是完整自动战斗每约 10 秒截图；Standalone 是独立程序截图。主相机 RenderTexture 不包含 Overlay UI，不据此声称人工验收用户血条外观。视觉失败项明确保留。
- **诊断：** 原 A* Editor HTTP 更新检查器仍会报 Insecure connection not allowed；只识别该精确旧堆栈并恢复 Error Pause，保留日志，没有关闭全局网络安全或忽略其他运行/退出错误。独立程序没有该 Editor 诊断。修复过程中的 Failed_*.txt 是历史失败证据，不是当前通过结果。

最终关节版本的 PolicyAndStress_Result.txt 再次通过 35 项检查：逐类型上限、失效候选、预算/释放复检、归属复用、致死清召唤以及三轮重开，Boss 网格/材质计数仍是 26/3。托管内存约 583.71 → 583.53 → 583.59 MB；Unity 总分配约 393.15 → 412.60 → 429.40 MB。冷启动后三轮原生分配增加约 36.25 MB，尚未进一步区分场景/引擎缓存；因此只确认 Boss 对象计数稳定，**不宣称零内存增长或长期无泄漏**。

构建过程自动清空了 TMP 动态后备字体的缓存数据，结束后已将该非任务变更精确恢复至本轮起点；原字体资产不包含在交付变更中。

未完成/未测：正式动画遮挡与倒伏，长期战斗平衡、真人手感、从头到尾的正式关卡流程、低配与长时间压力测试。可运行、可战胜不等于上述验收已完成。

## 7. 最少试玩步骤

1. 打开 Assets/_Project/Scenes/HiveTyrant_CombatTest.unity，按 Play，组件/配置/材质/导航均已保存，不需手动接线。
2. 使用原移动和鼠标左键射击；按 F6 开启自动选技，开始完整战斗。测试场景初始自动选技关闭。
3. F1 扫击、F2 前向三段波、F3 召唤、F4 Phase2、F5 击杀、F6 切换自动、F9 重载。死亡后可直接 F9。
4. 正式场景使用原 Research/竞技场入口流程，没有添加作弊键。Windows 产物双击可试玩；只有显式传入 -hivetyrant-smoke 才会启动 Development 输入自动化。

复验菜单：AlienPurge/Hive Tyrant/Configure and Validate Combat、Run Play Mode Verification。完整批处理入口 HiveTyrantCombatVerification.RunCompletionPass；最终资源/策略入口 RunPolicyAndStress；构建 HiveTyrantBuildVerification.BuildWindows。批处理前正常保存退出交互 Unity，避免同时写同一工程。
