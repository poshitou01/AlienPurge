# 原有四种敌人外观与动画交付

Swarmer、Rusher、Tank、Spitter 保留各自轮廓，统一岩棕甲壳、浅灰筋膜的表现，补齐八方向 Idle、Move、Death；Rusher、Tank、Spitter 增加对应原有技能的 Windup、Attack、Recover。受击使用原生闪白。

工作目录：`D:\AlienPurge_CoreEnemies`。分支：`feature/enemies-core-art-animation`。基线：`727645fb7ce2acce8910ecf1d053522b394c7afc`，继承 Support/Exploder 正式美术和已验收 Boss。交付 SHA 见聊天最终回执，或在本分支执行 `git log -1 --format=%H -- Docs/Enemies/CoreEnemies/Delivery.md`。

## 直接试玩

1. Unity 2022.3.62f3c1 打开上述独立项目目录。
2. 运行 `AlienPurge/Core Enemies/1 Configure and Validate`，然后 Play。配置可重复运行。
3. 已配置时可直接使用 `2 Open Test Scene`；无伤害预览使用 `5 Play Animation Preview`。
4. 自动验证使用 `4 Run Play Mode Verification`；只查资源用 `3 Validate Assets`。

场景：`Assets/_Project/Scenes/CoreEnemies_Art_Test.unity`。

WASD 移动、鼠标左键射击、Shift Dash、Space Jet Jump。F1 四类遭遇、F2 单类轮换、F3 回收、F4 击杀、F9 Restart。F5 预览/实战、F6 八方向、F7 动作、F8 暂停、F10 缩放、F11 半速。

## 资产与预览

准确的 EnemyData、Prefab、EnemyType、对象池条目和依赖见 [Integration.md](Integration.md)，Prefab GUID、Collider、原生材质和技能时长见 [PrefabAudit.txt](PrefabAudit.txt)。四个既有 Prefab 在原路径更新；数据和旧资源保留。

正式资源规格和各状态数量见 [ArtProduction.md](ArtProduction.md)：736 张独立 512×512 RGBA、PPU 256、Center Pivot、168 个动画片段。Move 采用现有八帧 / 12fps；不是旋转单张图，也不复制静帧凑数。Recover 复用各自收势帧的范围已在该文档明确列出。没有待补的必需身体动画；音效沿用原系统，本轮没有新增独立音效包。

| 敌人 | 八方向静态对照 | 八方向行走 GIF | 动作 GIF |
|---|---|---|---|
| Swarmer | [方向图](ArtReview/Swarmer_EightDirections.png) | [行走](ArtReview/Swarmer_Move.gif) | [待机/死亡](ArtReview/Swarmer_States_DR.gif) |
| Rusher | [方向图](ArtReview/Rusher_EightDirections.png) | [行走](ArtReview/Rusher_Move.gif) | [技能/死亡](ArtReview/Rusher_States_DR.gif) |
| Tank | [方向图](ArtReview/Tank_EightDirections.png) | [行走](ArtReview/Tank_Move.gif) | [技能/死亡](ArtReview/Tank_States_DR.gif) |
| Spitter | [方向图](ArtReview/Spitter_EightDirections.png) | [行走](ArtReview/Spitter_Move.gif) | [技能/死亡](ArtReview/Spitter_States_DR.gif) |

素材由内置 imagegen 制作；完整提示词在 `Tools/CoreEnemyArt/Prompts/`，32 个最终源图集在 `Tools/CoreEnemyArt/Source/`，可重现导出脚本和审计也在该工具目录。

真实 Unity 捕获：[六类同屏](Validation/SixEnemy_StyleComparison.png)、[测试场景](Validation/TestScene_Overview.png)、[四类行走](Validation/Four_Move_Live.gif)、[Rusher 冲锋](Validation/Rusher_Live.gif)、[Tank 震地](Validation/Tank_Live.gif)、[Spitter 吐弹](Validation/Spitter_Live.gif)。技能时序 CSV 和逐帧 PNG 同在 `Validation/`；素材 GIF 与真实 Play Mode 捕获分别标识。

## 公共脚本改动

| 既有脚本 | 改动与用途 |
|---|---|
| `Scripts/Enemy/FastChargeAttack.cs` | 只读查询冲锋阶段、进度和方向。 |
| `Scripts/Enemy/Attacks/HeavyShockwaveAttack.cs` | 只读查询阶段、进度和实际伤害半径。 |
| `Scripts/Enemy/Attacks/RangedEnemyAttack.cs` | 只读查询吐弹阶段、进度和锁定方向。 |
| `Scripts/Enemy/EnemyContactDamage.cs` | 禁用组件不再响应接触伤害；修复冲锋时原有双重扣血。 |
| `Editor/SupportExploderSetup.cs` | 首次从 Swarmer 克隆缺失的 S/E Prefab 时移除 CoreEnemyAnimation，避免双死亡播放器。 |

以上路径均相对 `Assets/_Project/`。新增脚本包括 CoreEnemyAnimation、EnemyAttackVisualSample、CoreEnemyArtLab，以及配置、导入、验证和独立测试存档隔离的 Editor 脚本。全部新增/修改文件及用途见 [FileManifest.csv](FileManifest.csv)。

动画只修改 Sprite，完全不使用 Animation Event 结算伤害。原生 EnemyHealth 继续唯一负责死亡、击杀、经验和回收。死亡画面延长至 0.55 秒，伤害碰撞及能力在死亡时立即停止。

## 验证范围

四类完整 Play Mode 验证已通过 411 项检查，包含八方向、原生技能、伤害、死亡、经验、复用、A*、暂停/半速、Boss 实际召唤和 Restart。实际结果见 [Validation/PlayMode_Result.txt](Validation/PlayMode_Result.txt)、[AssetValidation.txt](AssetValidation.txt)、[StaticAudit.json](StaticAudit.json) 和 [TestNotes.md](TestNotes.md)。自动验证由真实 Unity Editor Play Mode 执行，不将静态审计算作试玩。

原有 Support/Exploder 完整回归也已通过 296 项检查，报告：[SupportExploder_Regression.txt](Validation/SupportExploder_Regression.txt)。回归配置工具生成的 S/E 资源及场景改写已按运行前快照逐文件恢复，回归结果单独保存，本次交付不改写其既有资产。

本轮停止在独立测试场景。正式区域、Arena、任务、Storm、毒雾、成长和撤离未接入；最终区域组合、密集敌人性能以及目标设备画面仍由区域任务在整合后验收。未更改 Boss 名单，不合并、不推送，也没有修改其他 Git 工作目录。
