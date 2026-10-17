# Support / Exploder 首版交付

交付目标：两种敌人在独立测试场景可用。正式地图由 Region Loop V02 对话统一接入。

工程：Unity 2022.3.62f3c1，URP 14.0.12，A* Pathfinding Project Free 4.2.17。起点为本地 Boss 验收分支 `codex/hivetyrant-combat` 的 `609c7ff523b8f6a04db9e978bbd994eb6d155655`。检查时原工程干净。开发仅发生于 `D:\AlienPurge_SupportExploder` / `feature/enemies-support-exploder`；未 reset、clean、合并、推送或更改区域任务工作目录。

## 直接试玩

用相同 Unity 版本打开上述独立 worktree。

1. `AlienPurge/Support + Exploder/1 Configure and Validate`：创建/修复本交付资产及测试场景，重复执行不叠加管理器或测试根节点，已有生成资产保留 GUID。
2. `AlienPurge/Support + Exploder/2 Open Test Scene`，按 Play。
3. WASD 移动、鼠标左键射击、左 Shift Dash、Space Jet Jump；F1 补充组合、F2 生成 Exploder、F3 直接回收、F4 击杀两类新敌人、F9 Restart。
4. `AlienPurge/Support + Exploder/3 Validate Assets` 检查配置；`4 Run Play Mode Verification` 自动进入真实 Play Mode，结束后回到测试场景。

场景路径：`Assets/_Project/Scenes/SupportExploder_Test.unity`。使用现有玩家、武器、伤害、经验池、敌人池和 A*，加入有实体阻挡的独立试验场。不会将测试场景插入正式 Build Settings。

## 代码与配置

准确 EnemyData / Prefab 路径、对象池注册、能力数值、三个公共脚本差异和区域接入约束见 [Integration.md](Integration.md)。测试场景 EnemyPool 显式配置六个类型，新增容量为 Support 2 / Exploder 3，禁止这两类自动扩容；没有给正式刷怪器追加任何权重。

能力配置独立于 EnemyData。所有临时增益、计时器和目标属于实例，不写共享数据。Support 的增益带来源与目标代次；Exploder 的警告中心、半径、持续时间在预警开始时锁定。

## 美术完成状态：临时，未完成正式美术

检索原工程的已跟踪资产，没有 Support/Exploder 的独立 Sprite 或动画。用户概念图只作为外形参考，没有导入为游戏 Sprite，也没有把整张概念图裁切后冒充动画。

当前使用现有 Swarmer 四足岩甲步行图作为临时身体，加上程序绘制的紫色三瓣器官／橙红囊体。新 Animator 和 16 个 clip 使用独立 GUID；它们引用原始 Sprite，并以 `TEMP_SharedSwarmer` 命名。动画使用真实的原八方向帧，不是旋转单张图片，但**不能算作这两个新敌人的正式八方向动画**。每个方向 8 帧，12 fps，Direction 为 D=0、DR=1、R=2、UR=3、U=4、UL=5、L=6、DL=7。

动画目录：`Assets/_Project/Animations/Support/` 与 `Assets/_Project/Animations/Exploder/`。效果材质：`Assets/_Project/Art/Enemies/Animated/AbilityTest/EnemyAbility_VertexTint.mat`。临时器官和技能标识由 `EnemyAbilityPresentation` 生成 Mesh，并在回收时隐藏、销毁时释放。身体保持原 Sprite 材质和 EnemyYSorting；特效与器官分别使用现有排序层。

现有导入实际参数为 PPU 256、512 像素画布、中心 Pivot，并非脚底 Pivot；脚底/阴影/碰撞体对齐沿用原 Prefab，没有修改旧 Sprite Pivot。四类原敌人的实际路径、材质、组件与导入参数记录在 [ExistingEnemyAudit.txt](ExistingEnemyAudit.txt)。

正式美术缺项：

- 两种敌人各自的八方向身体与器官组合 Sprite，以及各方向独立步行帧；需保持深棕岩甲、浅灰白筋膜和一致光照。
- Support 的站立/支援循环、受击和死亡表现；紫色三瓣器官的八方向遮挡与随步态运动。
- Exploder 的站立、预警膨胀、受击打断、主动爆破与受击死亡表现；橙红囊体的八方向遮挡。
- 技能音效与最终 VFX 美术。当前可辨识范围圈、连线、强化箭头、填充计时环是行为测试表现，死亡音效沿用 AudioManager 的默认普通敌人分支。

后续正式 Sprite 放到 `Assets/_Project/Art/Enemies/Animated/Support/`、`Exploder/`，按既有 `Move/<方向>/` 组织。替换独立 clip 中的 Sprite 引用，保持 Direction 参数、PPU、视觉对齐及 Collider 规则。正式器官已经烘焙进 Sprite 时关闭 `EnemyAbilityPresentation.temporaryArt`，保留范围/状态提示。不得覆盖旧敌人 .meta。

## 验证证据

最终一轮 Unity 2022.3.62f3c1 / Direct3D11 的真实 Editor Play Mode **119 项检查通过，0 运行错误/异常**。覆盖出生、范围与目标上限、来源与目标回收复用、死亡撤销、原生寻路与动画换帧、预警中击杀、单次范围结算、碰撞体边界、Dash/Jet Jump、原生子弹、经验拾取、Restart，以及注册新类型后的 Boss 实际召唤。编译仅有基线原有的 SafeZoneController / ExtractionController 两条 CS0414 警告。

自动化通过原生能力入口启动 Dash/Jet Jump；Dash 测例设置最后移动输入朝圈外，保持原生速度、距离和默认关闭无敌帧的配置。它是可重复的行为检查，不等同于真人游玩验收。首轮发现的场景卸载特效清理问题已修复并由最终 Restart 用例验证。

- [AssetValidation.txt](AssetValidation.txt)：资产身份、动画绑定、旧枚举值、Boss 名单。
- [Validation/PlayMode_Result.txt](Validation/PlayMode_Result.txt)：本次实际 Unity Play Mode 检查结果，以文件中的最终 RESULT 为准。
- `Validation/*.png`：实际摄像机截图，文件名标明临时美术；摄像机截图不包含 IMGUI 测试说明，试玩时 HUD 会显示 TEMP ART。
- `Preservation.txt`：与 Boss 基线比较的旧资产、场景、.meta 保护检查。

这里的自动化仅验证首版行为与集成。未把它宣称为最终美术验收或完整地图平衡验收。
