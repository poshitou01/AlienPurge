# 四种旧敌人的美术生产与动画接入

遵循用户选择：保留原有识别轮廓、岩棕甲壳与浅灰筋膜，重新绘制并补齐动画。身份参考来自项目内旧八方向第一帧；图像工具逐方向绘制各状态，不旋转或镜像单张图，不用概念板代替动画。

## 资源规格

- 正式导出：512×512 RGBA，真正透明背景；PPU 256，Center Pivot `(0.5, 0.5)`。这与现有项目一致，不擅自改成 Bottom Pivot。
- Bilinear，Clamp，无 Mipmap，无压缩，sRGB，Alpha Is Transparency，FullRect，extrude 1。
- 保持旧各方向参考体积，按每种敌人的原有脚底基线注册；同方向状态使用统一缩放，死亡姿态通过肢体绘制倒伏。
- 方向：D / DR / R / UR / U / UL / L / DL，对应索引 0–7，直接复用 EnemyAnimationController。
- 受击：原生 EnemyHealth 闪白，四个 Prefab 的 hitColor 使用 `(2,2,2,1)`，和 Support/Exploder 同样在 Sprite-Lit 材质上产生可见反馈。
- 根 SpriteRenderer、原 URP 材质、Enemies Sorting Layer、EnemyYSorting、Rigidbody2D、Collider 均沿用。

| 敌人 | 每方向独立 PNG | 总 PNG | Animator 状态 | Clip 总数 |
|---|---|---:|---|---:|
| Swarmer | Move 8、Idle 4、Death 4 | 128 | Idle / Move / Death | 24 |
| Rusher | Move 8、Idle 4、Windup 4、Attack 8、Death 4 | 224 | Idle / Move / Windup / Attack / Recover / Death | 48 |
| Tank | Move 8、Idle 4、Windup 4、Attack 4、Death 4 | 192 | 同上 | 48 |
| Spitter | Move 8、Idle 4、Windup 4、Attack 4、Death 4 | 192 | 同上 | 48 |

已导出并接入 736 张独立 PNG、168 个 Sprite-only Clip。数量、引用及规格检查见 `AssetValidation.txt`、`ArtReview/SpriteAudit.json`；运行验证另见 Play Mode 报告。

Idle 4fps 循环；Move 遵循现有 8 帧 / 12fps 循环。技能准备、执行、恢复通过现有计时的 normalized progress 取帧；Clip 元数据读取 Prefab 实际 windup/charge/recovery 时长。死亡 4 帧 / 0.55 秒，不循环，末尾 0.15 秒淡出；奖励入口仍在 EnemyHealth 的首次死亡时。

Rusher Recover 倒放自己已绘制的 Windup 4→1；Tank/Spitter Recover 使用自己 Attack 的第 3、4 帧收势。以上是明确的收势资源复用，不计入独立 PNG 数量。Tank/Spitter 原逻辑在进入 Recover 时即时震地/吐弹；前半恢复时间呈现释放/反冲，后半呈现收势，不延迟或重复伤害。

## 可追溯素材与机械处理

`Tools/CoreEnemyArt/Source/` 保存每个敌人每方向的生成图集。`Prompts/` 保存对应请求和来源记录。`References/` 保存旧素材审计与参考图，原素材不改名、不覆盖。

素材使用内置 imagegen 生成与定向修正；最终 32 个源图集与对应提示词全部保存于本工作目录。工具缓存路径仅作来源记录，Unity 资源不依赖缓存。

`export_frames.py` 只按透明轮廓分离图集、一次统一缩放、对齐画布、输出静态对照和 GIF；不绘制、不变形、不旋转、不镜像、不插值肢体。为防相邻帧矩形包围盒重叠造成脚尖串入，以连通轮廓归属分帧并保留原像素及抗锯齿边缘。若完整身体数量不匹配，拒绝该源图集并报告，不偷偷复制帧凑数。

部分源 PNG 的 Alpha=0 像素保留生成工具的棕色 RGB，预览工具可能显示这些 RGB；导出时透明画布合成，正式 Sprite 的空白保持透明。没有烘焙棋盘格或背景。

`ArtReview/` 是素材级方向对照与 GIF。`Validation/` 的 Unity 截图与技能序列来自真实 Editor Play Mode，二者应区别查看。技能序列的 timing.csv 记录真实游戏时间与阶段；GIF 是这些帧的编码，不重新绘制游戏画面。
