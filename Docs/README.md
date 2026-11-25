# 设计文档

| 文档 | 内容 |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 模块划分、程序集依赖、Combat API、时序图、状态机、里程碑 |
| [BALANCE.md](BALANCE.md) | 战斗数值初稿（帧数口径）、TTK 推导、怒气节奏、调参敏感度 |
| [ART_SPEC.md](ART_SPEC.md) | 像素规格：PPU/分辨率、角色尺寸、动画帧率、图集、导入设置 |
| [COMBAT.md](COMBAT.md) | 战斗规则实现与调参：状态机、逐文件说明、改数值、调试、测试覆盖 |

代码结构：`Assets/Scripts/Game/<Module>/{Runtime,Editor}`，程序集名 `Game.<Module>`，命名空间同名。

依赖方向只有一条：`Core ← Combat ← {Player, Enemy, Battle, UI, Data}`，`Level` 只依赖 `Core`，`Boot` 依赖全部。
