# 原有四种敌人正式美术与动画升级

用户选择：保留 Swarmer、Rusher、Tank、Spitter 的识别轮廓，重绘并补齐动画。

工作目录 `D:\AlienPurge_CoreEnemies`，分支 `feature/enemies-core-art-animation`，起点 `727645fb7ce2acce8910ecf1d053522b394c7afc`，继承已验收 Boss 基线 `609c7ff523b8f6a04db9e978bbd994eb6d155655`。开始时无未提交修改，未发现适用 AGENTS.md。

四种现有正式 Prefab 仅有 Move 八方向 8 帧 / 12fps；生命系统支持 IEnemyDeathPresentation，可以接入倒伏动画并保留原生经验/击杀/回收入口。Rusher 使用 FastChargeAttack，Tank 使用 HeavyShockwaveAttack，Spitter 使用 RangedEnemyAttack；Swarmer 使用原生接触攻击，没有额外主动技能。

目标：独立透明外观，Idle / Move / Death；三种主动技能单位补齐准备、执行、恢复身体状态；受击沿用原生闪白。保留 EnemyType 数值、EnemyData、能力参数、伤害判定、Prefab 路径与 GUID、Collider、寻路、排序、对象池。原 Sprite/Clip 资产保留，新状态资源置于原敌人目录内。

仅建立独立测试场景及配置/验证菜单，不修改正式区域刷怪、任务、Storm、毒雾、成长、撤离、Boss 或其他工作目录。完成实际 Unity 验证及交付提交后等待验收；不合并、不推送。
