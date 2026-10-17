# 本轮制作参考（2026-10-17）

只参考开发方法，不复制角色、贴图或招式数值。以下为短时阅读的官方来源；应用建议是本工程的设计判断。

- [Supergiant《Hades》官方更新记录](https://www.supergiantgames.com/blog/hades-updates/)：记录了牛头人旋风招式预警的清晰度调整、Theseus 雷电判定改进和穿墙攻击修复。本工程据此优先验证可见预警、独立二段提示与实际墙体判定，而非仅增加伤害。
- [Housemarque VFX 主创的技术说明](https://blog.playstation.com/2021/09/16/from-resogun-to-returnal-the-evolving-vfx-magic-that-brought-atropos-to-life/)：介绍从俯视角作品到 Returnal 的特效实践，并说明准备攻击时的肢体特效需要与动画及设计团队共同调整时序。本工程将局部动作、预警、释放弧和波面统一交给战斗调度，控制碎片数量，避免特效遮盖缺口。
- [Double Fine / Beca Vessal，GDC 2023《Crafting Epic Boss Battles》](https://media.gdcvault.com/gdc2023/Slides/CraftingEpicBoss_Vessal_Beca.pdf)：第 25–29 页将攻击拆成预警、攻击和恢复；后续讨论阶段、条件和冷却。该案例不是俯视角，本轮仅借鉴调度结构：死亡优先、阶段转换可取消待释放动作，波面作为独立传播对象完成自己的生命周期。

F2 的三个危险段、两个通道以及按真实碰撞体计算净宽，是本次用户要求与工程几何共同确定的实现；上述来源没有被用作这些具体数值的出处。
