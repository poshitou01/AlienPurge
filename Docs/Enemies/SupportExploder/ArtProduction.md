# Support / Exploder 美术制作记录

本轮使用 Codex 内置 imagegen 生成与编辑透明位图。参考是本对话中的 AlienPurge 生态概念图，以及工程原有 Swarmer / Rusher / Tank / Spitter 的实际素材、Unity 导入设置和摄像机表现。原始概念图的临时磁盘路径已失效，但对话内附件可见；没有将概念板裁剪成正式 Sprite。

## 可复现的资产来源

最终源图保存在 `Tools/EnemyArt/Source/`，不依赖用户缓存目录。`sheets.json` 保存 16 张身体方向图的初始完整提示词；`active_sheets.json` 保存移动支援的提示词；`blast_prompt.txt` 保存圆形爆炸提示词；`support_R_refinement.txt` 保存右向后甲的局部修订提示词。JSON 中的生成缓存路径仅作来源记录，实际导出从本仓库 Source 目录读取。

源图组织：

| 源文件 | 内容与使用方式 |
|---|---|
| `Support_<方向>.png`，共 8 张 | 每张 20 姿态：Move 8、Idle 4、支援展开 4、Death 4 |
| `Exploder_<方向>.png`，共 8 张 | 每张 24 姿态：Move 8、Idle 4、Warning 4、Death 4、Burst 4 |
| `Support_Active_*.png`，共 7 张 | 共 64 个持续支援行走姿态；D/DR 共用一张源图，其余每方向单独一张 |
| `Exploder_Blast.png` | 8 个圆形爆炸姿态，无方向，可共用 |
| 两张 `*_Turnaround.png` | 制作参考，不被 Unity 引用；正式帧按正确方向分别生成 |

所有方向均独立绘制。没有把单张图旋转或镜像成八方向，也没有以整体缩放/平移伪造行走。每方向的八个 Move 绘制帧有不同肢体姿态；SupportMove 另有八个展开器官的行走帧。身体、器官和腿属于同一张完整透明 Sprite，不是附加图标。

初始 Support 方向参考中 UR/UL 布局有歧义，最终方向图按实际朝向重绘、单独核对；一次混合方向生成中的无效 UR 行被弃用，未进入正式资产。右向后甲经过局部修订，避免小亮点误读为第二个头部。

## 机械导出与导入

从项目根运行 `python Tools/EnemyArt/export_frames.py`，需要 Python 3 与 Pillow（本轮使用 3.12.10 / 11.3）。脚本只做透明连通区域切分、每张源图统一比例缩放、512 画布注册和预览输出，不绘制或扭曲肢体。每张图按完整轮廓提取，避免按理想等分网格切掉脚趾。它不改 `.meta` 或旧敌人目录。

所有身体帧注册到同一画布脚底基线；每张方向源图固定统一缩放比例，不能通过逐帧缩放模拟呼吸或步态。Unity 仍使用原工程中心 Pivot 与原碰撞根。详见 [SpriteAudit.json](ArtReview/SpriteAudit.json) 的源尺寸、切分框、比例和最终 SHA256。源图边界提示属于人工复核项；最终导出帧另外通过透明边缘和安全留白检查。

Unity 的 `SupportExploderArtImport` 仅处理这两个新敌人的 Art 子目录；`SupportExploderArtSetup` 创建 Sprite-only clip，并保留已存在资源的 GUID。重复配置重用 controller、clip、Prefab、EnemyData，只重建本任务测试场景；不会堆叠管理器或注册条目。

正式输出为 **424 张 512×512 RGBA PNG**：Support 224、Exploder 身体 192、共用爆炸 8。正式动画为 **96 clip**：Support 7 状态 × 8 方向、Exploder 5 状态 × 8 方向。完整规格和各状态时序见 [Delivery.md](Delivery.md)。SupportLoop/End 合理复用独立绘制的开合帧，不虚报新增帧数；循环终点闭合 key 也不算新增绘制帧。

## 预览的含义

- `ArtReview/*_Move_AllDirections.gif` 与 `Support_SupportMove_AllDirections.gif` 是正式帧的八方向循环拼图，保持约 12 fps。
- `ArtReview/*_States_DR.gif` 是方便并排检查的四帧状态拼图，统一慢速轮播；它不是技能实际时序的录屏。
- `ArtReview/Exploder_Unity_Runtime.gif` 从实际 Unity 摄像机连续捕获，帧间隔取录制时的 Time.time，展示真实 1.5 秒预警、主动破裂和清理；末尾额外停留便于查看回收。
- `Validation/Authored_Characters_Close.png` 为独立测试场景放大摄像机截图；`TestScene_Overview.png` 为实际游戏尺度截图。查看与操作仍应以 Unity Play 为准。

请求的身体/能力视觉状态均已接入。未新增专属音效、法线贴图或独立材质系统，继续沿用工程现有配置。美术风格和操作手感等待用户验收；没有声称已经通过真人试玩、独立播放器构建或正式区域平衡验收。
