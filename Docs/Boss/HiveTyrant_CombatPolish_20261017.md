# Hive Tyrant：连射反馈、自动战斗、原图血条与二阶段提示

本轮基于 `codex/hivetyrant-combat` / `5c8e0ce`，工作区起点干净。按用户最新要求修改 HUD 外观，取代旧文档中“血条外观不属于修改范围”的限制。工程仍为 `D:\AlienPurge\AlienPurge`，Unity 2022.3.62f3c1 / URP 2D。

## 修改内容

### 命中抽搐

根因在 `HiveTyrantPresentation.LateUpdate`：普通子弹每隔约 0.3 秒触发一次整身受击，切离 Sprite 动画、启用中性姿态网格，再切回 Sprite。既打断视觉上的步态连续性，又让连续射击重复进入整套网格计算。基线实际连射 8 秒、52 次命中，发生了 21 次姿势重启；同期镜头和玩家几乎没有位移。

普通命中现在只保留原武器命中火花及 0.055 秒的轻微暖色反馈，继续播放当前动作；不会因每颗子弹重新摆动躯干和双臂。技能、转阶段和死亡动作仍正常播放。`EnemyHealth` 的逐次伤害日志改为默认关闭的 `logDamageEvents` 诊断开关，不关闭 Error/Warning，也不改变扣血、死亡、奖励和对象池流程。共享脚本原编码保留。

| 相同 8 秒真实连射 | 修改前 | 最终 UI 验证版本 |
| --- | ---: | ---: |
| 发射 / 实际命中 | 52 / 52 | 52 / 52 |
| 整身受击姿势重启 | 21 | 0 |
| 每次伤害的 Console 日志 | 52 | 0 |
| 帧间隔中位数 | 9.835 ms | 8.939 ms |
| P95 | 11.461 ms | 10.204 ms |
| 最大帧间隔 | 19.475 ms | 12.756 ms |

这是本机 2540 × 1445 Game View 渲染的 Editor 测量，截图写入在测量结束之后；不作为所有硬件或独立版的帧率保证。原始记录：`CombatPolishValidation/Before_HitFrames.csv`、`FinalVisual_HitFrames.csv`。

### 原图血条

继续使用 `HiveTyrant_BossHPFrame_01.png`，没有重画或修改原 PNG。两端甲壳按原比例显示，中央横梁通过切片延长，避免将整张图压成细线。橙色填充增加顶部高光、底部阴影和有限长度的扣血残影；Boss 名称、阶段、数值分层排列。去掉大块常驻深色面板；原头像引用保留，默认隐藏，可通过 `showPortrait` 开启。血量与阶段仍来自同一个 Boss / EnemyHealth。

### 默认自动 AI

测试场景及 Setup 工具均默认启用自动技能，打开场景按 Play 即可交战。F1–F6 仅保留为可选调试操作，F8 查看面板，F9 重开。

AI 每 0.25 秒评估一次可用技能，考虑距离、冷却、上一技能和当前召唤数量。近身优先扫击，中距离优先地波；场上小兵不超过 2 只时才考虑补充召唤，空场时提高召唤优先级。无效地波路线可以退选其他合法技能；不会每帧反复规划。开场留 2.2 秒，最早召唤为接战后 8 秒。攻击预警、冷却、收招和真实安全通道继续生效。

### 召唤平衡

配置位置：`Assets/_Project/Data/Boss/HiveTyrant_Combat.asset`。

| 参数 | 一阶段 | 二阶段 |
| --- | ---: | ---: |
| 每波最多召唤 | 4 | 5 |
| 同时存活上限 | 6 | 8 |
| 最小召唤间隔 | 16 秒 | 16 秒 |
| 整场预算 | 20 只，共享 | 20 只，共享 |

沿用 Normal / Fast / Heavy / Ranged 原生敌人及 45 / 25 / 10 / 20 权重。类型上限仍是 5 / 3 / 1 / 2，防止多个重型或远程敌人同时堆叠。出生位置会再次验证墙体、边界、玩家距离与可达性，故被阻挡或玩家进入的落点会跳过，实际数量可少于单波上限；不会强行把敌人塞到玩家脚下。

### 二阶段转场

生命降至 50% 且仍存活时触发一次，沿用原血量，不回血：

1. 取消未释放的攻击和待召唤，Boss 停下蓄光，保留一阶段轮廓。
2. 约 1.26 秒时展现 Riftwing 翼形，伴随核心亮度变化、淡紫弧光与金色能量碎屑。
3. 画面上方显示 `PHASE II / RIFTWING AWAKENS`，柔和淡入淡出，血条保留二阶段标签。
4. 3 秒转场结束，随后保留原 1.1 秒恢复窗口，再交回自主 AI。二阶段保留独立预警的二连扫击，并使用更大的召唤波次。

转场特效没有伤害碰撞；不传送玩家、不开全屏闪光、不改变 Time.timeScale。玩家可以继续移动和射击。死亡或禁用立即清理阶段字幕和转场特效，重开清空阶段状态。仍在场的原生小兵继续正常行动。

设计参考：Housemarque 在 [Hyperion 制作说明](https://blog.playstation.com/2021/05/28/returnal-the-making-of-that-unforgettable-hyperion-fight/) 中讨论了随阶段增强的表现与攻击可读性；其 [敌人视觉设计说明](https://blog.playstation.com/2021/04/14/creating-returnals-otherworldly-enemies-vfx-driven-tentacle-tech-and-deep-sea-inspirations/) 介绍了用局部发光提示攻击。这里据此采用分段蓄能和展翼、清楚的阶段字幕与稀疏特效，没有复制其角色、贴图或攻击图案。

## 实测与证据

- `After_Result.txt`：25 项检查通过；第一场完整默认 AI 战斗，约 107.2 秒；原玩家 5 HP / 速度 5，原武器，共 687 次射击，32 次技能释放，20 只累计召唤，一次转阶段。玩家存活，覆盖三种技能；没有手动技能键、补血、无敌或直接修改 Boss 血量。
- `FinalVisual_Result.txt`：22 项检查通过，覆盖连射、原图切片血条、4/5 只召唤、二阶段上限、分段展翼、阶段提示、禁用/死亡中断和重开。针对性场景测试中的摆位与阈值扣血属于测试夹具，不混作完整战斗结果。
- `Regression/PolishRegression_Result.txt`：218 项检查通过，包含原武器伤害链、地波通道、四类原生小兵与复用、阻挡/预算、转阶段、死亡清理和正式 Level01 原入口/完成桥接。该轮第二场完整自动战斗约 106.8 秒，结束剩余 3 HP，696 发，20 只累计召唤，34 次释放，一次转阶段。三次重开均为 26 个 Boss 网格、3 个对应材质，没有残留召唤/波面/预警/爆发；托管内存约 625.7 / 623.9 / 624.0 MB，非“零内存”声明。
- `WindowsBuild_Result.txt`：最终严格开发构建成功，0 错误、2 个既有 CS0414 警告（SafeZoneController / ExtractionController），最终增量构建约 13.37 秒。文件在 `D:\AlienPurge\Builds\HiveTyrantPolish\AlienPurge.exe`。
- `Standalone_Result.txt`：Windows 独立版默认 AI 完整战斗通过，约 109.6 秒，结束剩余 5 HP，702 发，20 只累计召唤，34 次技能释放、一次转阶段，0 运行错误，结束后没有 Boss 特效根对象残留。
- `Standalone_Visual_Result.txt`：最终 Windows 版 1280 × 720 真实相机与运行时 HUD 离屏渲染通过。隐藏窗口不绘制正常背缓冲，不能用其直接截屏或帧间隔当作视觉/帧率证据；验证探针仅在截图时把既有 Overlay Canvas 临时交给相机并放在场景上方，截图后恢复。这是单独的阈值视觉夹具，不能算作完整自然战斗。完整独立战斗之后仅修订了 DEVELOPMENT_BUILD 探针的截图方式，没有再调整战斗逻辑。

完整回归首次遇到旧夹具的范围前置条件失败：小兵将静止玩家推到 `(4.38, -18.79)`，Boss 在 `(0.90, -0.59)`，玩家已离开 18 单位接战范围，因而“全落点阻挡”检查得到 `request=False`。日志保留在 `Regression/FirstAttempt_Result.txt`。修正仅在该测试开始前把玩家放回接战区，并显式断言存活、接战范围和动作所有权；未改游戏中的范围限制，也未放宽“施法被接受但没有合法落点”的原断言。这也解释了上一轮同一测试的偶发失败。

图片由真实 Unity 画面捕获。最终独立版效果看 `Visual_HUD.png`、`Visual_Phase2Reveal.png`、`Visual_Phase2HUD.png`；Editor 的分段记录见 `FinalVisual_HUD.png`、`FinalVisual_Phase2_Charge.png`、`FinalVisual_Phase2_Reveal.png`。Editor 初版画面中的悬空小头像已在最终版隐藏；最终阶段字幕使用柔边 UI 网格背景。

这轮验证说明当前数值可用，并非长期平衡或所有八方向正式美术的最终验收。既有身体网格的局部遮挡/接缝问题没有在本轮全面重做；本轮解决的是普通命中的连续姿势切换与请求的战斗体验。

## 保护与恢复

修改前增量快照：`D:\AlienPurge\AlienPurge_Snapshot_HiveTyrant_CombatPolish_20261017_2050`，59 个文件，含 `.meta`、SHA256 清单、HEAD、Git 状态。原完整备份及 `Backup_SHA256_Manifest.csv` 仍在 `D:\AlienPurge\AlienPurge_Backup_PreHiveTyrant_20261017_152640`。

29 个原有 `.meta` 身份保持不变；未改原 PNG、ProjectSettings、Packages、正式 Level01 场景布局。现有测试场景仅把 `automaticSkills` 从 0 改为 1，Boss Prefab 仅微调受击色和闪色时长。原撤离、背包、风暴、玩家操控及普通敌人行为未调整。构建自动清空的 TMP 动态字体缓存已按修改前快照恢复。检查结果见 `CombatPolishValidation/Preservation.json`。

需要恢复时先退出 Play Mode并保存自己的后续修改，再针对本轮本地提交做 revert 或从快照逐文件比较恢复，不覆盖整个项目、不执行 reset/clean。无自动推送或合并 main。
