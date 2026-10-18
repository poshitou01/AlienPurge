# Support / Exploder 正式美术与动画交付

本轮在 `D:\AlienPurge_SupportExploder`、`feature/enemies-support-exploder` 中完成正式外观、动画和独立测试场景升级，等待用户验收。没有接入正式区域，没有合并、rebase、cherry-pick 或推送。

本轮起点：`23114de4866261ceb0e540fabaeb4b423e1a8135`。该原型基于已验收 Boss 提交 `609c7ff523b8f6a04db9e978bbd994eb6d155655`。最终交付 SHA 由本轮提交及最终回复提供。

## 直接试玩

用 Unity **2022.3.62f3c1** 打开本独立目录。场景：
`Assets/_Project/Scenes/SupportExploder_Test.unity`。

沿用菜单 `AlienPurge/Support + Exploder/`：

1. **1 Configure and Validate**：配置正式资源并校验；可重复运行。
2. **2 Open Test Scene**：打开独立场景，按 Play 试玩。
3. **3 Validate Assets**：只校验资产。
4. **4 Run Play Mode Verification**：运行真实场景、物理、对象池、寻路、武器与奖励回归。
5. **5 Play Animation Preview**：直接进入身体动画预览。该模式也从现有敌人池取对象。

操作：WASD 移动、鼠标左键射击、Shift Dash、Space Jet Jump。F1 添加遭遇，F2 在安全距离生成 Exploder，F3 回收，F4 对两种新敌人施加致命伤害，F9 Restart。F5 切换身体预览/战斗；预览中 F6 切方向、F7 切状态、F10 切换实际镜头/放大；F8 暂停/恢复。

预览只检查身体动画，禁用能力和碰撞；退出时恢复真实能力。实际支援、预警、范围伤害与打断用战斗模式检查。测试场景没有加入正式 Build Settings。

## 正式资产

| 类型 | EnemyData（路径和 GUID 保留） | Prefab（路径和 GUID 保留） |
|---|---|---|
| Support | `Assets/_Project/Data/Enemies/Enemy_Support_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Support.prefab` |
| Exploder | `Assets/_Project/Data/Enemies/Enemy_Exploder_Data.asset` | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Exploder.prefab` |

Sprite 位于 `Assets/_Project/Art/Enemies/Animated/Support/` 与 `Exploder/`，按 `动作/方向/类型_动作_方向_帧号.png` 组织。共 **424 张独立透明 PNG**：Support 224、Exploder 身体 192、圆形爆炸 8。

Animator：`Assets/_Project/Animations/Support/Support_Animator.controller`、`Assets/_Project/Animations/Exploder/Exploder_Animator.controller`。共 **96 个方向/状态 clip**。原型的 TEMP 资源保留但已无正式 Prefab 引用；重新配置也不会回退到借用帧。

身体和器官一同绘制在独立 Sprite 中。Support 使用紫色三瓣器官，Exploder 使用橙红双囊和较低的负重身体；没有借用 Swarmer 的 Sprite，没有通过旋转或整体缩放模拟步行，也没有用镜像补方向。

## 动画状态与规格

所有身体状态都有八方向，索引保持 **0 D、1 DR、2 R、3 UR、4 U、5 UL、6 L、7 DL**。

| 状态 | Support | Exploder | 播放规则 |
|---|---|---|---|
| Idle | 每方向 4 帧 | 每方向 4 帧 | 4 fps，1 秒循环 |
| Move | 每方向 8 帧 | 每方向 8 帧 | 12 fps，2/3 秒循环，沿用原工程稳定标准 |
| SupportStart | 每方向 4 帧 | — | 1/3 秒展开 |
| SupportLoop | 使用已绘制器官展开帧组成 6 步脉冲 | — | 6 fps，1 秒循环 |
| SupportMove | 每方向独立 8 帧 | — | 12 fps；持续支援时仍播放四足步态 |
| SupportEnd | 反向播放对应展开帧 | — | 1/3 秒收拢 |
| Warning | — | 每方向 4 个蓄爆姿态 | 按能力真实倒计时进度取样，当前为 1.5 秒 |
| Death | 每方向 4 帧 | 每方向 4 帧 | 0.55 秒倒伏、熄灭并淡出 |
| Burst | — | 每方向 4 帧 | 主动自爆专用，0.75 秒内完成破裂和回收 |
| Explosion | — | 共用 8 帧 | 12 fps，约 0.7 秒内消散；仅视觉子对象按伤害半径缩放 |

循环 clip 的末尾回到首帧；末尾闭合 key 不算新的绘制帧。支援开始/结束与持续光环保持同一玩法，没有改成主动施法。移动支援使用单独步态，站立时使用器官开合与脉冲。

导入沿用现有规范：**512×512、PPU 256、中心 Pivot (0.5,0.5)、Bilinear、Clamp、无 Mipmap、默认不压缩、sRGB、Alpha Is Transparency、Full Rect**。实际旧敌人也使用中心 Pivot；没有把它误写为脚底 Pivot。新帧以统一脚底基线注册到画布，保留原碰撞根与阴影结构。

身体沿用 URP `Sprite-Lit-Default`、`Enemies` Sorting Layer 和 `EnemyYSorting` 的根位置 Y 排序。动画只改 Sprite，不改碰撞根位置、缩放、旋转。特效沿用测试分支已有材质/排序方式；预警边界仍等于实际 OverlapCircle 半径。

受击继续使用原生 EnemyHealth 的 0.08 秒反馈，仅在两个新 Prefab 配置可见的亮色倍率。没有新增专属音效包，音效继续使用已有系统。

## 行为与复用

范围、目标数、增益、预警时间、伤害和安全出生配置均未改变。Support 仍为 4.5 米内最多 3 个普通目标、伤害 +1、多个来源取最大值；不支援 Support、Exploder 或 Boss。Exploder 仍有出生保护，2.4 米内触发 1.5 秒固定位置预警，3 米范围伤害；预警中击杀取消主动爆炸。

`SupportExploderAnimation` 读取现有方向和能力状态，只选择 Animator clip。**没有 Animation Event 结算入口**。生命、伤害、经验、击杀与回收仍由原生链路负责。仅死亡展示等待从原型的短暂延迟改为 0.55/0.75 秒，奖励仍在原生死亡入口结算一次。

启用时重新绑定 Animator，恢复默认 DR、Idle、帧时钟、颜色与透明度；禁用/回收时清理展示状态。预警直接按能力进度采样，暂停和变速遵循 Time.timeScale。死亡和爆炸展示不创建跨生命的延迟伤害回调。

测试场景继续显式注册六类对象池，Support 预热 2、Exploder 预热 3，禁止这两类扩容。注册不等于加入正式刷怪候选；Boss 四类召唤名单保持原样。

## 预览与验证

- [Support 八方向](ArtReview/Support_EightDirections.png)、[Exploder 八方向](ArtReview/Exploder_EightDirections.png)
- [Support 八方向行走 GIF](ArtReview/Support_Move_AllDirections.gif)、[移动支援 GIF](ArtReview/Support_SupportMove_AllDirections.gif)
- [Exploder 八方向行走 GIF](ArtReview/Exploder_Move_AllDirections.gif)
- [Support 状态 GIF](ArtReview/Support_States_DR.gif)、[Exploder 状态 GIF](ArtReview/Exploder_States_DR.gif)
- [Unity 实际镜头](Validation/Authored_Characters_Close.png)、[实际蓄爆与爆炸录制](ArtReview/Exploder_Unity_Runtime.gif)
- [真实 Play Mode 结果](Validation/PlayMode_Result.txt)、[资产检查](AssetValidation.txt)、[保护范围检查](Preservation_ArtUpgrade.txt)

最终 Play Mode 运行 **296 项 PASS，RESULT: PASS**。测试涵盖原有玩法、八方向真正换帧、全部方向状态引用、移动支援、受击恢复、暂停/半速、死亡打断、同一池实例连续三轮复用、效果清理、Boss 真实召唤和 Restart。最后的右向后甲图像修订另经 Unity 资产校验通过。Unity 编译仅有基线既存的 SafeZoneController / ExtractionController 两条 CS0414 警告。

这些是实际 Windows Unity Editor / D3D11 自动化和摄像机检查；没有把静态检查当作真人试玩。尚未进行用户美术/操作验收、独立播放器及其他平台构建、正式区域平衡验收。

## 文件与后续交接

完整新增/修改路径及用途见 [FileManifest.tsv](FileManifest.tsv)，状态清单与制作来源见 [ArtProduction.md](ArtProduction.md)，区域接入依赖和限制见 [Integration.md](Integration.md)。

本轮修改的运行脚本均为本任务已有或新增的专用脚本：SupportEnemyAbility、ExploderEnemyAbility、EnemyAbilityPresentation、EnemyAbilityTestLab，以及新增 SupportExploderAnimation。编辑器配置/验证工具随之更新。

**本轮未修改任何旧敌人共用运行脚本。** 原型提交曾增加的 EnemyType、EnemyMovement、EnemyContactDamage 三处兼容改动仍保留；本轮没有进一步修改它们，也没有修改 EnemyHealth、EnemyPool、PooledEnemy、EnemyAnimationController 或 EnemyYSorting。

本阶段停在独立场景可验收。后续正式地图整合须等待用户明确指令，由区域适配任务统一负责。
