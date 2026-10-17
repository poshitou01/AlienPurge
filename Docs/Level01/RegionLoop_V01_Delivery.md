# Level01 区域玩法升级 V01

本成果位于独立工程 `D:\AlienPurge_RegionLoopV01`，分支 `feature/level01-region-loop-v01`。
起始提交：`5c8e0cef9449aa722e6f32c35c3269aa7f0d1f67`。
原工程 `D:\AlienPurge\AlienPurge` 的 Boss 工作目录未用于本任务写入，也没有自动合并或 push。
完整文件清单见 `RegionLoop_Changes.txt`；交付提交可用 `git log -1 --format=%H feature/level01-region-loop-v01` 查看。

## 场景和菜单

- 受保护源场景：`Assets/_Project/Scenes/ArtTests/Level01_FullWorld.unity`。
- 可玩副本：`Assets/_Project/Scenes/ArtTests/Level01_FullWorld_GameplayV01.unity`。
- 安装：`AlienPurge/Gameplay/Level01/Install Region Loop V01`。
- 验证：`AlienPurge/Gameplay/Level01/Validate Region Loop V01`。
- 行为回归：`AlienPurge/Gameplay/Level01/Run Region Loop V01 Regression`。

请用 Unity `2022.3.62f3c1` 打开 **独立工程路径**，不要在尚未合并的原工程中寻找菜单。
菜单会保存并打开副本，不改变启动场景。Build Settings 保留原来的 MainMenu、Day01_TestGround 条目、顺序和启用状态，只在末尾追加 GameplayV01，保证 Restart 正常加载。

## 已接入的流程

| 区域 | 敌人 | 主线条件 | 奖励 | 出口 |
| --- | --- | --- | --- | --- |
| ColonyEntry | Swarmer | 杀死指定驻守组，再操作接入设备 | 首次清场 EXP、Arrival EXP | G1 / Arrival |
| CargoYard | Swarmer、Rusher | 主动启动 Beacon，在设备范围内传输；敌人近身争夺会暂停传输并损伤设备；消灭有限任务守卫 | 清场 EXP、Beacon EXP、核心模块槽 1 选择 | G2 / Beacon |
| PowerMaintenance | 前述敌人、少量 Tank | 主动供电、防守、两波有限增援及一名必须击杀的 Tank 守卫 | 清场 EXP、Power EXP、核心模块槽 2 选择 | G3 / Power |

任务普通巡逻与必要守卫分开计数。等待设备读条不能跳过守卫。设备完整度归零或超时进入失败状态，交互重试会先撤销旧任务波次，生成新 attempt；已杀驻守、已用巡逻预算和已发奖励不会重置。旧 BeaconRecovery 不再能几秒完成 Beacon。

驻守组每局仅建立一次。远区存活敌人退回池中，保存剩余血量和票据；再次进入时恢复剩余敌人，不复活死者、不补预算、不重新发清场奖励。只运行当前区域及正在进行任务的区域，全局同时存活上限默认 24。驻守具有感知和追击范围。

出生点使用地图 walk polygon、实际非 Trigger 碰撞、A* 可走节点及设备空间连通性共同检查。每次出生有橙色圈预警、玩家距离要求、有限候选和重试次数。关键出生失败会显示明确诊断并进入可重试状态，失败出生不会算击杀。18 个入口锚点已有首轮可用位置，可在场景中调整并查看 Gizmos。

全部基础数值来自独立区域资产，应用到对象实例；玩家升级和总生存时间不改变现存敌人的基础属性。现有死亡、经验球、伤害、对象池和 A* 仍负责各自功能。`RegionEnemyLease` 只跟踪明确取得的对象，因此 Boss 自己召唤的敌人不会自动加入区域任务。

普通击杀经验由原 `EnemyHealth` 每次真实死亡生成经验球；区域额外奖励按 encounter/mission ID 去重。等级连续提升时，每一级都进入 FIFO 选择队列，模块选择使用原有面板、按钮和合法选项。开门时模块奖励可以处于“待选择”，只有成功应用后才记为 applied。没有接入最终进化；`MilestoneApplied` 事件保留后续区域里程碑入口。

本阶段到 G3 打开为止。G4 之后的主线终端保持离线。Storm、毒雾、撤离、高风险箱、旧时间刷怪和 45/150/240 秒模块/进化/超频调度在副本中停用；总生存时间仍记录，HUD 使用正向时间。GameManager 原代码已经没有 360 秒自动胜利兜底，因此本次没有添加或移除这类结局。

## 文件和配置

- Runtime：`Assets/_Project/Scripts/Level01/RegionLoop/`。
- Editor：`Assets/_Project/Editor/Level01/`。
- 配置：`Assets/_Project/Data/Level01/RegionLoop/ColonyEntry.asset`、`CargoYard.asset`、`PowerMaintenance.asset`。
- 专用动态字体：`Assets/_Project/Data/Level01/RegionLoop/Fonts/`，由现有字体复制，防止 TMP 运行时向共享字体补字、写入纹理；仅替换副本 UI 引用。
- 场景管理根：`Level01_RegionLoop_V01`；设备 ID `device/<region>`，入口 ID `ingress/<region>/<index>`。
- 文档/结果：`Docs/Level01/`；本地 Unity 详细日志：`Logs/Region*.log`（Git 忽略）。

初始 EXP 位于场景控制器；敌人生命/速度/伤害/EXP、驻守、增援波次、预算、间隔、上限、设备目标、奖励、安静期均可配置。
数值均为**首轮试玩建议值**，加速回归不代表平衡测试。
Spitter 可通过现有 `EnemyType.Ranged` 配置；Exploder、Support 保留配置标识，需要后续接入实际池类型/Prefab，本次没有新行为或动画。

重复安装只补缺失内容，并保留已有锚点位置、区域数值及波次。未知同名目标拒绝覆盖；明确的引用/ID 冲突会报错。安装会重新确认新模式安全接管项（例如旧终端和环境伤害不能重新开启）。工具不 Apply 共享 Prefab、不重建地图或 Boss Arena。
源场景发现一个位于 inactive `WeaponModulePanel` 下、与有效管理器引用完全相同的重复模块管理组件；只在副本移除该重复组件，保留其 GameObject/UI。不同引用的重复管理器会报错，不会猜测删除。

## 实际验证与边界

Unity 2022.3.62f3c1 的 Editor 编译、安装、地图碰撞/连通性、790 项保护资产校验和加速 Play Mode 回归已实际运行通过。最终回归报告包含 46 项成功断言（4 项重复安装检查、42 项 Play Mode 检查）。24 个新增资产/文件夹 GUID 均唯一。
详细结果分别见 `RegionLoop_InstallReport.txt`、`RegionLoop_Validation.txt`、`RegionLoop_PlayMode.txt`。Play Mode 退出后再次核对全部 790 项原资产/元文件，结果见 `RegionLoop_PostPlayProtection.txt`。
Play Mode 回归使用真实场景、对象池、死亡逻辑和 UI 按钮，但通过脚本传送玩家、确定性伤害和加速时间执行；设备失败通过测试注入完整度为零。它不替代玩家手动瞄准、移动路线、手感、性能和数值平衡试玩。
已覆盖：入口停留 380 秒；未完成主线的门禁；前三个区域闭环；有限巡逻和任务波次；设备失败重试；回区不重置；池复用血量/归属；回收及临时停用不算死亡；模块 1/2 各发一次；连升多级排队；暂停、死亡、Restart；切换原场景后恢复旧 45 秒模块调度。界面截图为 `RegionLoop_Entry.png`、`RegionLoop_Choice.png`。
未运行：人工完整走图/瞄准试玩、长时间性能及平衡测试、独立 Player 构建、与正在变化的 Boss 分支集成后的行为回归。现有两个未使用私有字段编译警告仍保留，无新增编译错误。

回归菜单会临时使用 `AlienPurge_RegionLoopV01_Regression` productName 隔离自动存档目录，退出 Play 后还原项目设置。请在没有未保存场景编辑时运行。测试覆盖了重复安装保留调整；临时测试调整会在 finally 中恢复。
早期调试失败的记录保留在 `Regression_Attempt*.txt`；`RegionLoop_InstallFailure.txt` 是历史保护检查失败记录，不代表当前安装状态。早期两次测试尚未隔离存档时触发过原 SaveManager 的退出自动保存；没有进行撤离/结算，也没有手工回写或还原原工程的存档。后续验证均使用独立测试存档。动态字体写入问题通过专用字体资产修复；临时停用敌人的回收延后一帧，避开 Unity 在 OnDisable 中重设父对象的限制。

## 试玩步骤（8 步）

1. 在 Unity Hub 打开 `D:\AlienPurge_RegionLoopV01`，等待该工程自己的 Library 导入。
2. 点击 Install Region Loop V01，再点击 Validate Region Loop V01；打开的应是 GameplayV01 副本。
3. Play，观察 ColonyEntry 目标与 G1 关闭；停留不会解锁后期敌人或自动开启环境事件。
4. 清理入口驻守，靠近接入设备按 E，领取升级并穿过 G1。
5. 在 Cargo 搜索/战斗后主动按 E 启动 Beacon，守设备并清掉任务守卫；依次选普通升级与模块 1，确认 G2 打开。
6. 需要测试重试时，放任敌人在设备附近破坏设备；失败后重新交互，确认不会直接开门或叠加旧波次。
7. 在 Power 启动供电，完成设备目标、有限波次及 Tank 守卫；选择模块 2，确认 G3 打开，后续终端离线。
8. 回入口检查不会复活驻守/重复奖励；测试死亡和 Restart 后回到新的初始局。退出 Play 后可重新运行安装菜单检查保留调整。

## 公共接口与 Boss 合并

本任务没有改动 `EnemyPool`、`PooledEnemy`、`EnemyHealth`、`EnemyMovement`、`EnemyPathNavigator`、Boss 脚本/Prefab/动画/Arena 原场景。
实际公共脚本改动为：

- `EnemyDefinition.InitializeInstanceAttributes(int health, float speed, int damage, int experience)`：只应用实例配置，并设置原生攻击依赖的 `HasBeenInitialized`。`ClearInstanceInitialization()` 清除该初始化状态/缓存并还原原始缩放。租约回收时先还原生命、经验、移动、接触伤害、导航和攻击组件状态，再清除此初始化。原 `InitializeFromGlobalDifficulty` 和旧调用者保持原样。**这是最需要与 Boss 修改核对的公共敌人接口。**
- `EnemySpawner`、`MissionManager`、`PlanetStormController`、`ToxicFogRouteController`、`ExtractionController`：新增默认 false 的 `regionDriven`，只阻止自动调度入口；Extraction 同时拒绝旧 CallExtraction。Awake/Singleton/服务初始化保留。
- `MissionManager.SetRegionObjectiveView`：区域控制器写入只读展示/统计，不调用旧任务奖励逻辑；完成计数按 mission ID 去重。
- `UpgradeManager`：可选场景 `regionRewards` 引用，连续升级排队；增加显示/取消队列选择接口。无此引用继续原流程。
- `WeaponModuleSelectionManager`：默认 false 的 `regionDriven`、可选队列引用、里程碑模块展示/取消接口；原合法选项与槽位规则保留。
- `L01World.regionDriven`：区域模式禁止移动任务点。
- `L01Progression.regionLoop`：可选区域权威引用，门读取其 milestone；旧 Complete 调用不能绕过；旧场景无引用仍使用原 flags。
- `L01Navigation.ConfigureGrid`：从 private 改为 public，供同工程安装/验证使用已有导航配置。

后续请在单独集成分支按提交合并两边，**不要用本分支的旧 Boss 文件覆盖 Boss 新版本**，也不要重新复制整张 GameplayV01 地图。
先审查以上公共脚本及 Build Settings 的潜在交叉修改；Scene/Prefab 需要定向更新相关 Boss 实例/引用，保留手工调整。
收尾时只读观察到 Boss 分支已前进至 `2c6d9de719e41e277f75bd15536f162636398276`，且仍有未提交改动；其已提交 `EnemyHealth` 增加了伤害日志开关。本任务未把这些变更复制过来，也不修改该文件。此观察是当时状态，实际集成以 Boss 最终提交为准。
即使无文本冲突，也必须重跑：池复用、真实死亡/回收区分、EnemyHealth.IsDead 时序、玩家目标、Boss 召唤物归属、G1-G3、升级队列、Boss Arena 战斗与旧场景时间调度。
保护清单绑定交付基线；集成了已审查的 Boss 资产更改后，应通过 `RefreshProtectedBaseline.ps1 -BaselineCommit <集成提交SHA>` 明确更新清单，不能静默忽略保护错误。
若集成还明确修改了源 FullWorld 场景，安装根记录的 `sourceSceneHash` 也会报来源变化；应审查原图差异和已存在副本后，明确更新来源记录或定向迁移，不要删掉副本重新生成地图。
