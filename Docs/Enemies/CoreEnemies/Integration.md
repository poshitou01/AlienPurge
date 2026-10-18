# 原有四种敌人：区域版本接入说明

本分支仅升级现有普通敌人的外观、动画和独立测试入口，不修改区域池、任务增援、Boss 配置、正式 Arena 或 Level01。合并时机由区域适配任务统一决定。

## 原有身份保持不变

| 名称 | EnemyType | 现有 Prefab | 现有 EnemyData |
|---|---|---|---|
| Swarmer | Normal = 0 | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Normal_Swarmer.prefab` | `Assets/_Project/Data/Enemies/Enemy_Normal_Data.asset` |
| Rusher | Fast = 1 | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Fast_Rusher.prefab` | `Assets/_Project/Data/Enemies/Enemy_Fast_Data.asset` |
| Tank | Heavy = 2 | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Heavy_Tank.prefab` | `Assets/_Project/Data/Enemies/Enemy_Heavy_Data.asset` |
| Spitter | Ranged = 3 | `Assets/_Project/Prefabs/Enemies/Animated/Enemy_Ranged_Spitter.prefab` | `Assets/_Project/Data/Enemies/Enemy_Ranged_Data.asset` |

Prefab 和 Data 路径与 GUID 保持不变。数据、伤害、技能计时、碰撞体和身体根缩放保留。旧 Move Sprite、Clip 和 Controller 也保留。对象池仍使用 `EnemyPool.poolEntries` 的原有四个条目；本轮没有新增 EnemyType，也无需注册第二份 Prefab。

## 必要依赖与变更

基线 `727645fb7ce2acce8910ecf1d053522b394c7afc`，包含 Support/Exploder 正式美术、玩法以及 Boss 已验收基线。整合时需要该基线中的 `IEnemyDeathPresentation` 和原生 EnemyHealth 死亡表现接口；不可只复制 PNG 而遗漏播放脚本或 `.meta`。

- 新图像：`Assets/_Project/Art/Enemies/Animated/{Swarmer,Rusher,Tank,Spitter}/Authored/`。
- 新动画：`Assets/_Project/Animations/<名称>/Authored/<名称>_Animator.controller` 及同目录的 `.anim`。
- `CoreEnemyAnimation` 使用现有 Animator、SpriteRenderer、EnemyAnimationController；死亡通过原生 EnemyHealth 结算与回收，技能由原来的行为脚本结算。
- `EnemyAttackVisualSample` 提供只读的阶段、进度、时长、锁定方向。
- 公共运行脚本修改：FastChargeAttack、HeavyShockwaveAttack、RangedEnemyAttack 仅新增只读视觉查询；Heavy 额外公开只读的实际震地半径。EnemyContactDamage 增加 `isActiveAndEnabled` 检查，修复 Unity 在组件禁用时仍发送碰撞回调、导致 Rusher 同帧叠加接触与冲锋伤害的问题；伤害数值及启用时的接触规则不变。
- 公共编辑器脚本修改：SupportExploderSetup 仅在“两个 Prefab 不存在、从 Swarmer 首次创建”分支移除 CoreEnemyAnimation，避免同一个对象存在两种死亡播放组件。现有 Support/Exploder Prefab 不经过此分支。
- Tank 的冲击边界复用 `Art/Enemies/Animated/AbilityTest/EnemyAbility_VertexTint.mat`，显示实际伤害半径，不缩放碰撞根对象。
- 新增的 Editor 专用 CoreEnemyTestProfileIsolation 只在两个独立敌人测试场景进入 Play Mode 时临时切换存档命名空间，退出时恢复项目名称。正式场景和 SaveManager 未改动。

## 接入检查

1. 在用户批准整合后，通过 Git 审阅和引入本任务交付，不替换整个 Assets 目录。
2. 区域池中的原有 Normal/Fast/Heavy/Ranged 权重、阶段、上限和任务增援规则保持区域任务当前版本。此次美术升级不需要额外增援，也不改变敌人强度。
3. 检查每个区域的 `EnemyPool` 注册是否引用上表 Animated Prefab。若已经引用，GUID 不变即可获得新外观；若引用其他旧基础 Prefab，由区域任务逐项确认后更换。
4. 检查场景中的 Prefab 实例是否单独覆盖 Animator Controller 或 Sprite。此类覆盖可能继续显示旧 Move 动画；只审阅与动画有关的覆盖，不应用或清除整个实例的 Collider/Transform 覆盖。
5. 不给一个对象同时挂 CoreEnemyAnimation 和 SupportExploderAnimation。不要将 CoreEnemyAnimation 加到 Boss。
6. 不修改 Boss 的四类型召唤名单，不从所有注册 Prefab 自动推导召唤池。
7. 先运行独立测试，再由区域任务验证区域内遮挡、脚底排序、任务增援、死亡统计和 Restart。

推荐出现阶段与同时存在上限：沿用区域任务已经配置的四类规则；本轮没有调整这些玩法参数。任务增援可以继续使用四个原有类型，但不要因新增动画而提高数量或额外添加 Support/Exploder。后两种敌人的区域使用限制仍以其既有 Integration 文档为准。

## 独立测试

场景：`Assets/_Project/Scenes/CoreEnemies_Art_Test.unity`。

菜单 `AlienPurge/Core Enemies/1 Configure and Validate` 可重复运行；只更新本轮四个现有 Prefab 的美术连接及独立测试场景，不改正式关卡。`2 Open Test Scene` 打开场景，`4 Run Play Mode Verification` 执行真实 Editor Play Mode 检查，`5 Play Animation Preview` 进入无伤害的八方向预览。

实战：WASD 移动，鼠标左键射击，Shift Dash，Space Jet Jump。F1 四种敌人，F2 轮换单种对战，F3 回收，F4 击杀，F9 Restart。F5 切换预览/实战，F6 方向，F7 动作，F8 暂停，F10 预览缩放，F11 半速。

Swarmer 只有原生接触攻击，预览的主动技能槽显示 Idle；没有新增虚假的主动技能。预览中的 Death 仅展示姿态；验证真实奖励与回收请在实战按 F4 或用武器击杀。

四类死亡均通过已有 IEnemyDeathPresentation 接口呈现 0.55 秒倒伏，末 0.15 秒淡出。死亡时立即停止攻击、关闭伤害碰撞并结算一次奖励，随后回收到原生对象池；这会比旧版的短暂消失多占用约 0.4 秒死亡对象，不延后击杀或经验奖励。
