# Hive Tyrant：死亡反馈、二技能镜头与顶部 UI 修复

起点：`codex/hivetyrant-combat`，`a4605aa`，工作区干净。修改前快照：`D:\AlienPurge\AlienPurge_Snapshot_HiveTyrant_Repair_20261017_1958`，含文件、原 .meta、Git 起点及 SHA256 清单。交互 Unity 已通过正常关闭保存流程退出，验证期间始终只有一个工程写入者。

## 问题与修复

1. **死亡后看似卡死。** 用户截图和实际日志均为玩家降到 0/5 HP，原 `GameManager.EnterGameOver` 将 `Time.timeScale` 设为 0；专用场景没有绑定 `gameOverPanel`。保留原死亡/结算/暂停规则，在测试场景补上明确的失败界面、Boss 剩余血量和可点击的重试按钮。按钮在时间暂停时仍可操作，F9 也可直接重开。正式 Level01 保留原本已绑定的失败界面。
2. **F2 仿佛传送角色。** 原 Boss 镜头在波面出现时，把长达 14 单位的整条波面加入取景边界，随后直接写入新的中心。修复前实测玩家世界坐标未变，但镜头单帧跳了 5.1692 单位，静止玩家的屏幕位置跳了 18.54%。现在构图只跟随 Boss 与玩家，使用稳定的角色画布边界及平滑限速移动；预警、释放和波面消失都不会重新定位角色或镜头。原 `CameraFollow` 配置、地图及 F2 伤害几何未改。
3. **施法计算开销。** 保留原网格分区与动作轨迹，把每格重复的 6 个顶点改为共享的 4 个角点：55,296 → 36,864 个变形顶点，三角形位置与 UV 对应不变。肩部、躯干、翅膀和核心权重在生成时缓存，颜色读取移出逐顶点循环，去掉施法请求中的重复网格计算，并在生成时预热。没有改 PNG、PPU、角色比例或现有动作设计。
4. **调试黑框与顶部 UI。** 截图里的大黑框属于 `HiveTyrantCombatDebug`，仅安装于专用测试场景；正式场景没有它。测试面板现在默认收起，F8 展开/收起，底部仅保留简短操作提示。顶部 Boss HUD 改为独立 uGUI：保持原头像比例，分开名称/阶段/血量，加入分段血条及扣血缓动；Phase2 明确标识 Riftwing。继续读取原 EnemyHealth，不重建用户其他 HUD 或场景 Canvas。原头像、装饰框引用和资源身份保留。
5. **Editor 自动暂停及日志。** A* 旧更新检查使用 HTTP，会触发 `Insecure connection not allowed` 和 Error Pause。对应官方 HTTPS 地址已实际返回 200，仅将该请求地址改为 HTTPS，没有关闭网络安全或 Error Pause。专用测试导航改为 `OnlyErrors`，不再为每条正常寻路刷 Console；保留错误。自动独立验证退出等待改用真实时间，玩家死亡后不再被 `timeScale=0` 卡住。

## 实际验证

`RepairValidation/Before_Result.txt` 与 `After_Result.txt` 来自同一真实 Unity 2022.3.62f3c1 / DX11 测试场景。先安排初始位置并等待镜头稳定，再记录整个 F2 的开始、释放和结束；这些位置安排仅在 Editor 测试代码中。

| 测量 | 修复前 | 修复后 |
| --- | ---: | ---: |
| 首次 F2 请求 CPU 用时 | 53.079 ms | 13.993 ms |
| 镜头最大单帧世界位移 | 5.1692 | 0.0091 |
| 静止玩家最大单帧世界位移 | 0 | 0 |
| 静止玩家最大单帧屏幕比例位移 | 0.1854 | 0.0003 |
| Editor 帧时间 P95 | 28.34 ms | 11.11 ms |

帧时间包含截图和 PNG 写盘，最大值约 236 ms，不能视为普通施法帧或作为普适 FPS 保证。中位数为 8.66 → 9.33 ms。早期一次隐藏窗口未实际绘制 Game View 的数据被排除；最终验证强制真实绘制并检查截图存在。

最终针对性检查 **29 项通过**：UI 默认收起及开关、原生导航错误日志保留、F2 位置连续、死亡暂停/可见失败卡片、按钮实际 UI 射线命中与点击、重开 HP/时间/状态恢复、无重复 HUD/EventSystem、正确阶段与血量、1280×720/1920×1080/1024×768 布局安全边距计算，以及原正式关卡接入。正式关卡 F2 的玩家世界位移为 0，最大屏幕比例位移 0.0042，完整 Boss 轮廓入镜。正式关卡测试通过原 Research/入口接口激活；测试夹具跳过跨地图步行后，先等待原跟随镜头稳定，再测 F2。

原战斗回归最终 **215 项通过**，见 `RepairValidation/Regression/RepairRegression_Result.txt`：新波面及缺口、原武器伤害链、四类召唤原生 AI/复用、两阶段、死亡中断、正式入口和原完成桥接、镜头恢复，以及三轮重开。原玩家 5 HP / 速度 5 的自动输入实战用时 99.3 秒，645 次原武器射击、一次转换、12 次成功召唤、30 次技能释放，剩余 5 HP；没有治疗、调试伤害或额外无敌。三轮资源维持 26 个 Boss 网格、3 个匹配材质，无警示/波面/召唤残留。完整回归的隐藏窗口帧时间不作为前台 FPS 指标。

首次长回归的“全部召唤点封锁”组合断言曾失败，记录保留在 `RepairValidation/Regression/FirstAttempt_Result.txt`。业务代码不变的复跑通过；新增诊断记录请求成功、待召唤 0、玩家位于竞技场且存活。首次未记录足够前置状态，无法准确归因，因此不将一次复跑解释为已修复所有潜在测试偶发问题；本次没有改动召唤业务逻辑。

Windows64 Development 构建成功：0 错误、2 个项目原有未使用字段警告，49.05 秒；产物为 `D:\AlienPurge\Builds\HiveTyrantRepair\AlienPurge.exe`。原 Build Scenes 未改变。构建自动清空的 TMP 动态后备字体缓存已精确恢复到本轮快照，字体资产不属于本轮变更。

该 Windows 程序独立运行也通过：原 5 HP / 速度 5，102.6 秒完成两阶段战斗，650 次原武器射击、一次转换、11 次成功召唤、31 次技能释放，剩余 5 HP；运行错误 0，死亡清理后 Boss FX 根对象 0。结果见 `RepairValidation/Standalone_Result.txt`。隐藏窗口的帧时间不换算为前台 FPS，UI 视觉证据使用上述包含 Overlay 的 Editor Game View 截图。

本轮没有更改已有场景、Prefab、配置资产、PNG 或原 .meta；新 UI 由原 Boss/测试组件在运行时创建并清理，新脚本 .meta 由 Unity 正常导入。`RepairValidation/Preservation.json` 记录了校验结果。

以下图片均为实际 Game View 截图，**包含 Overlay UI**，不是示意图：

- `RepairValidation/After_HUD.png`：新顶部血条与默认收起的测试面板。
- `RepairValidation/After_Phase2HUD.png`：Riftwing / 300 HP。
- `RepairValidation/After_Death.png`：时间暂停时的失败重试界面。
- `RepairValidation/After_Controls.png`：展开后的测试操作面板。
- `RepairValidation/After_Level01Wave.png`：正式关卡与既有玩家 HUD 共存、无测试黑框。

## 试玩

打开 `Assets/_Project/Scenes/HiveTyrant_CombatTest.unity`，按 Play；F2 单独测试地面波，F6 开启自动战斗。F8 显示或隐藏调试操作，死亡后点击 **RETRY FIGHT** 或按 F9。F1/F3/F4/F5 的既有用途保留。正式关卡不新增这些调试快捷键。

本次修复不将此前仍有遮挡接缝的正式身体动作标为完成；上一轮素材缺口状态继续有效。
