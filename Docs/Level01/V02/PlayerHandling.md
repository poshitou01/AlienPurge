# V02 玩家移动、持枪与左上状态栏

最新 HUD 修订：恢复 HP、EXP、时间、击杀四枚原有图标，HP / EXP 使用英文，采用更紧凑的深色金边布局。当前截图为 Player_StatusHUD.png / Player_StatusHUD_Low.png；界面与战斗反馈的新说明见 [CombatImpact.md](CombatImpact.md)。下文保留移动与持枪实施记录。

本次在 `D:\AlienPurge_RegionLoopV01` 的 `Level01_FullWorld_GameplayV02` 场景内安装，基于 `1e73b8c`。入口仍是 `MainMenu_DeskV1`。

## 操作与画面

- WASD 保留原有速度和速度升级。采用 45% 起步速度、短加速和刹停，换向立即响应；斜向输入不会增加总速度。暂停、技能锁定和死亡清理移动状态。
- 身体与枪械面向最后一次有效瞄准，允许侧移、后退和边跳边射。八方向切换带 4° 滞回，鼠标落在身体附近时保持姿态，减少左右抖动。
- 持枪上半身与腿部步态分开驱动，避免原始步行素材的摆臂把双手带离枪身。保留现有八方向角色造型，使用场景私有 HeldStride 材质对下肢作连续、小幅步态变形，髋部与握枪手保持稳定。步态按物理实际位移推进，后退反向推进；原地顶墙不会因持续按键原地快跑。旧的徒手走路帧和动画资源保留，但 V02 持枪移动不再播放其摆臂。
- 调整握把到手掌的挂点，枪械缩放为原来的 72%，上下瞄准再缩短枪身至 78%，保持枪口沿真实射击方向。向后瞄准时枪身位于身体后方。
- 用现有角色图片的手部子 Sprite 实现手掌压住枪身的遮挡，既有源图、导入设置与共享预制体不变。子 Sprite 只引用源纹理，场景退出时释放。
- 原生枪械后坐力保留，并带动肩部小幅回动。跳跃与冲刺继续作用于整个 VisualRoot，手部、枪身和枪口一起运动。暂停会冻结本场景枪口闪光与后坐力时间。
- 为等离子步枪、雷霆贯穿和集束爆破分别校准握把；伤害、弹道、射击冷却、武器进化和对象池仍由原生组件负责。

## 左上状态栏

使用 Stash / 背包 / Trader 同一套 `Panel_Main_9Slice`、`Panel_Header_9Slice`、`Panel_Detail_9Slice`，与任务面板、技能栏和结算面板一致。

布局为“战斗状态 / 等级 → 生命条 → 经验条 → 用时 / 击杀”。复用 HUDManager 的原生文本与 Slider 引用，生命、经验、等级、时间和击杀均显示中文。生命不足 30% 使用警示色，受伤短暂提亮；面板不拦截鼠标瞄准。

## 安装与参数

已保存到 V02 场景，直接 Play 即可。重新安装使用 `AlienPurge/Gameplay/Level01/Install Region Loop V02`；安装器拒绝覆盖未保存的场景，并通过 revision 保留安装后手动调整的参数。无需修改原始角色预制体。

| 位置 | 参数 | 当前值 |
| --- | --- | --- |
| Player / PlayerMovement | Tactical Locomotion | 开启，仅 V02 |
| Player / PlayerMovement | Acceleration Time / Braking Time | 0.075 / 0.045 秒 |
| Player / PlayerMovement | Initial Speed Fraction | 0.45 |
| Player / PlayerCombatPresentation | Stride Length | 2.5 世界单位 / 周期 |
| Player / PlayerCombatPresentation | Direction Hysteresis | 4° |
| Player / PlayerCombatPresentation | Movement Lean / Step Bob | 1.4° / 0.012 单位 |
| Player / PlayerCombatPresentation | Vertical Weapon Length | 0.78 |
| WeaponController | Recoil Distance / Recovery | 0.055 / 0.095 秒 |

旧的 PlayerAnimationDriver、Body Animator 和 WeaponSocketController 仅在本场景禁用，避免与新的瞄准朝向驱动重复写入。碰撞体、脚部碰撞位置与世界 Y 排序保留。

## 验证

`AlienPurge/Gameplay/Level01/Run V02 Player and Status Checks` 使用独立测试存档，重复安装检查幂等，再执行真实 Play Mode 移动、墙体碰撞、瞄准、原生射击、进化握点、伤害/UI、Dash/Jump 衔接与死亡检查，输出 `PlayerChecks.txt`。

`Player_StatusHUD.png` 是正常镜头的状态栏；`Player_Pose_E/NE/N/NW/W/SW/S/SE.png` 是临时放大镜头的八方向检查图，`Player_Walking_E/S.png` 检查移动中的稳定握持，放大设置未写入场景。技能、完整战斗流程的回归分别见 `AbilityChecks.txt`、`CombatUIChecks.txt`。

806 项原始资源保护清单中，805 项保持原样；唯一例外仍是前一轮已授权的 DeskV1 主菜单目标场景字段，本轮没有新增例外。

最终验证包含：玩家与状态栏 48 项、技能/菜单 40 项、完整战斗/UI 89 项。报告保存实际通过时间与检查范围。

自动测试覆盖功能与渲染结果；移动“手感”仍属于主观体验。本次没有导出独立 Player 构建，新的持枪表现使用原有八方向平面素材，并非替换成骨骼 IK 动画。
