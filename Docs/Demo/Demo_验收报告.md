# AlienPurge Windows Demo v0.1 验收记录

日期：2026-10-19。交付范围按最后确认执行：直接构建现有主菜单和最终集成场景，保留当前玩法与界面。

## 构建来源

- 工程：`D:\AlienPurge_IntegratedV01`
- 分支：`feature/demo-v0.1`，仅本地工作，未推送、未合入 main。
- 游戏内容基线：`d98e8957019c14a8075042242e3f2120ed133be1`。本次制作前工作区干净。
- 主菜单：`Assets/_Project/Scenes/MainMenu_IntegratedV01.unity`
- 游戏场景：`Assets/_Project/Scenes/ArtTests/Level01_FullWorld_IntegratedV01.unity`
- 之前的移动朝向与动画、区域敌人组合、道路碰撞、设置 UI、爆炸/闪电表现、背包层级修复均已包含在该基线中；本次未再次修改这些内容。

集成基线继承的三份来源如下，本次没有重新合并它们：

| 成果 | 实际来源 | 来源提交 |
| --- | --- | --- |
| Hive Tyrant | `D:\AlienPurge\AlienPurge`，`codex/hivetyrant-combat` | `609c7ff523b8f6a04db9e978bbd994eb6d155655` |
| 区域玩法 | `D:\AlienPurge_RegionLoopV01`，`feature/level01-region-loop-v02` | `89cab820f95bea19f47aa1b28c548f2466236599` |
| Support / Exploder | `D:\AlienPurge_CoreEnemies`，`feature/enemies-core-art-animation` | `051073378dab7a723b94d50d5c82f85d05a8ca78`，以及先前整合时保存的工作区资源 |

Boss 和区域来源当时没有未提交成果；新敌人来源的正式资源及 `.meta` 已随先前整合导入，清单见工程 `Docs/Integration/ImportedWorkingFiles.json`。本次重新只读核验了三个来源：分支、HEAD、Git 状态和文件哈希全部保持不变，分别核验 5,062 / 5,241 / 8,731 个文件。

## 构建结果

| 项目 | 实际结果 |
| --- | --- |
| Unity | 2022.3.62f3c1 |
| 平台 | Windows x64 / StandaloneWindows64 |
| 脚本后端 | Mono2x，沿用工程设置 |
| Development / Script Debugging / Profiler | 均关闭 |
| 构建 | Succeeded，0 errors，2 warnings，42.58 秒 |
| BuildReport 内容大小 | 2,950,375,968 bytes；与最终 ZIP 压缩大小不是同一个指标 |
| Company / Product | DefaultCompany / AlienPurge Demo |
| 版本 | 0.1 |

两条警告都是原有 CS0414：`SafeZoneController.usedFallbackLastGeneration` 和 `ExtractionController.lastSelectionUsedFallback` 私有字段赋值后未使用。没有为消除警告而修改玩法脚本。

输出目录：`D:\AlienPurge_IntegratedV01\Builds\Demo\20261019_123458\AlienPurge_Demo_v0.1_Win64`

程序：上述目录中的 `AlienPurge.exe`。

交付 ZIP：`D:\AlienPurge_IntegratedV01\Builds\Demo\20261019_123458\AlienPurge_Demo_v0.1_Win64.zip`

包内保留 Unity 输出的完整运行依赖：`AlienPurge_Data`、`MonoBleedingEdge`、`UnityPlayer.dll`、`UnityCrashHandler64.exe` 等，并附试玩说明、此报告与 CREDITS。Burst 调试符号留在构建目录外层，没有装入试玩包。没有装入源工程、Library、Temp、Git、个人存档或开发机配置。

## 本轮文件改动

- `Assets/_Project/Editor/WindowsDemoBuilder.cs` 及 `.meta`：增加校验、原场景 Windows 构建、封包及打开输出目录菜单。脚本位于 Editor 目录，不进入游戏运行程序集。
- `Tools/Demo/native_smoke.py`：外部 Windows 键鼠输入与截图检查工具，不注入游戏脚本、不改运行状态。需 Python 和 Pillow；本机使用既有 `Logs/PlayerPreviewDependencies`。
- `Docs/Demo/`：版本基线、硬件信息、构建报告、保护校验、实际截图、试玩说明和验收记录。

构建时仅临时设置产品名与版本，使 Demo 使用独立存档目录；原工程设置随后恢复。构建工具也恢复 Unity/TMP 构建过程改变的字体缓存与资源文件。`BuildResult.txt` 中的 Uncommitted 是构建进行时的即时状态，包含这些临时变化；最终状态以 `ProtectionAudit.json` 为准。

30 个原场景与基线 SHA-256 一致；ProjectSettings 与基线一致；现有 Assets、Packages、ProjectSettings 的已跟踪内容没有差异。没有改写原场景、Boss 数值、区域配置、敌人能力或原菜单布局。早期未采用的 Demo 草稿仅保存在工程的 Logs 归档中，不参与最终构建。

## 实际运行验证

测试对象是上述原样构建的 Windows 程序，使用普通鼠标/键盘输入，没有使用额外的运行时观察器、传送或无敌功能。下列结果不借用先前其他构建的测试记录。

| 项目 | 结果与范围 |
| --- | --- |
| 1280×720 启动 | 原有桌面/平板主菜单出现，Start Game 可进入最终集成场景 |
| 基础输入 | 实际输入移动、射击、Q、Space；确认移动后画面变化与暂停响应。未据此宣称所有技能数值均已验收 |
| 背包 | Tab 打开，Esc 关闭；720p、1080p 中背包覆盖左右上角 HUD，没有被角落 UI 压住 |
| 暂停与返回 | Esc 暂停，原暂停菜单可返回原主菜单 |
| 死亡结算 | 原生敌人伤害造成死亡，显示本局结算及原有两个按钮 |
| 再次行动 | 从死亡结算重开；可见 HP 5/5、Lv.1、EXP 0、击杀 0、空背包及殖民地入口初始任务。未外推为全部后期状态已验证 |
| 设置保存 | 原 Settings 中 Master 调为 50%，退出程序，再从 ZIP 解压目录启动仍为 50%；随后通过原按钮恢复默认设置 |
| 正常退出 | 原 Quit Game 按钮退出，进程退出码 0 |
| 独立解压 | ZIP 解压到 `D:\AlienPurge_Demo_Verification\20261019_124025` 后，以 1920×1080 启动、进入场景、开关背包、暂停、返回、退出均成功 |
| 字体与资源 | 实际查看菜单、设置、背包、暂停和死亡结算画面；已查看文字没有缺字方框，未见粉色材质 |
| 日志 | 两次上述运行日志未发现 Exception、Error、MissingReference 或 Unicode 缺字报告；原生初始化及测试日志仍存在 |
| 编辑器引用 | 两个构建场景没有 Missing Script；菜单进入与暂停返回场景名匹配；关卡 GameManager / EnemyPool 各一个 |

封包后的完整性、文件哈希及最终 ZIP 解压启动记录另存工程 `Docs/Demo/PackageVerification.json`，避免把 ZIP 自身校验值嵌入 ZIP 造成循环。关键实际画面保存在工程 `Docs/Demo/Standalone/`；原始运行日志在工程 `Logs/DemoNative_*.log`。这些日志不是全程通关证据。

## 硬件与性能验证边界

Windows 11 家庭中文版 10.0.26200；Intel Core i7-14700HX；约 16 GiB 内存；NVIDIA GeForce RTX 4070 Laptop GPU，驱动 32.0.15.6624。Player.log 确认实际使用该 NVIDIA 显卡和 Direct3D 11。

测试分辨率为窗口化 1280×720 和 1920×1080。独立解压目录的首次 Mono 程序集加载日志为 14.607 秒；原输出目录的该项为 0.758 秒，这是程序集加载阶段而非完整启动耗时。首次启动可能在 Unity 标志处停留一段时间。

本轮未采集可靠的逐帧 FPS、1% low 或长时间内存曲线，也未完成 Boss、Storm 与大量特殊敌人并发压力测试，因此不提供最低配置或稳定 60 FPS 承诺。没有为性能检查关闭现有玩法。

## 保留的限制及未验证项

1. 当前原场景的撤离门仍要求击败 Boss；本次没有增加独立提前撤离路线。
2. 没有重做主菜单、增加操作说明页面、分辨率设置页面或结算退出按钮；操作说明在包内 README。
3. 原场景中的开发测试组件仍保留。U 和 F1–F5 等原有测试快捷键可能影响试玩，正常试玩请使用 README 的常规按键。`PendingRecoveryUITestSeeder` 的播种代码受 UNITY_EDITOR 限制，不会在独立程序自动填入仓库。
4. 当前 Unity 许可证生成的程序右下角可见 `trial version` 水印，本次保留。
5. 未在这份最终程序内完整通关所有区域、完成研究、击败 Boss、成功撤离或验证成功撤离后的仓库/待领取流程。
6. 未在这份程序内完整复核 Support 增益回收、Exploder 爆炸、Storm/毒雾、Signature 解锁、升级卡与任务奖励同时出现、所有道路碰撞及长期对象池稳定性；它们沿用已整合内容。
7. 当前仅验证了本机 Windows 环境；其他电脑、分辨率与显卡仍需试玩反馈。

最短人工补验：新开一局，按任务完成开门并搜索一个容器 → 在货运场/动力区观察特殊敌人与经验升级 → 经历一次 Storm/毒雾 → 完成研究并击败 Boss → 撤离、返回仓库检查物资 → 再开一局确认状态清空。期间查看爆炸/闪电升级及道路碰撞是否符合原已验收版本。

## 下次打包

用 Unity 2022.3.62f3c1 打开 `D:\AlienPurge_IntegratedV01`，保存当前编辑后执行：

1. `AlienPurge/Demo/Validate`
2. `AlienPurge/Demo/Build Windows Demo`
3. `AlienPurge/Demo/Open Build Folder`

仅更新说明时使用 `AlienPurge/Demo/Package Last Build`。工具显式构建上述两个既有场景，不必手动改 Build Settings；每次构建创建新的时间戳输出目录。
