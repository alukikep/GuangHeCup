# GuangHeCup 项目进度

接手时先读「当前状态」，再查与任务相关的记录。实现或修复后在「实现记录」顶部添加条目，并在状态变化时更新摘要。

## 当前状态

- **地图编辑器**通过 Unity 菜单 **Tools → GuangHeCup → 地图编辑器** 打开。支持选择房间、建房、绘制和擦除。房间为 0.5 世界单位的固定格距，瓦片和碰撞随场景保存。 [地图编辑器](../Assets/Scripts/Game/Level/Editor/RoomMapWindow.cs) · [建房逻辑](../Assets/Scripts/Game/Level/Editor/MapLayout.cs)
- **绘制图层**包含背景、地面、装饰和环境摆件。地面和装饰分别使用 `Tiles/Ground` 和 `Tiles/Decoration` 的固定调色板。背景从 `Environment/<主题>/Background` 选整张图片。摆件从主题下的 `Decorations` 和 `Midground` 选 Sprite。 [美术资源放置规则](../Assets/Game/Art/资源放置规则.md)
- **样板场景**可用来查看室内背景、地面、实体平台和摆件。此前只验证了编辑器行为、背景边界和地面/平台碰撞，尚未做带玩家控制器的完整试玩。 [IndoorMapSample.unity](../Assets/Scenes/IndoorMapSample.unity)
- **战斗模块**已有 Git 提交 `b8ff848`，实现与部分用例位于 [Game.Combat](../Assets/Scripts/Game/Combat) 和 [Combat 测试](../Assets/Tests/Game/Combat)。本次补录未重跑战斗测试。

## 实现记录

### 2026-11-28 · 修复合并后的战斗契约重名

- 删除重复的 `Assets/Scripts/Game/Combat/Runtime/Contracts/IDamageable.cs`，其 `DamageInfo` / `IDamageable` 与合并分支的 [CombatBasics.cs](../Assets/Scripts/Game/Combat/Runtime/Contracts/CombatBasics.cs) 重名。
- [EnemyBrain.cs](../Assets/Scripts/Game/Enemy/Runtime/EnemyBrain.cs) 改为实现合并分支的 `IDamageable` / `IHealth`；命中改用新的 `DamageInfo` 字段，近战与投射物分别传 `DamageType.Melee` / `DamageType.Projectile`。
- **验证**用 Unity 生成的 `Game.Enemy.rsp` 外部 Roslyn 编译，exit 0。编辑器日志中的 CS0535 来自修改前的缓存构建，需在 Unity 刷新后复验。
- **待办**在 Unity 中确认脚本重编译无报错；敌人 prefab 与灰盒行为仍未验证。

### 2026-11-27 · 建立进度交接入口

- 新建根目录 [AGENTS.md](../AGENTS.md) 和本文件，约定接手先读进度，完成实现后记录变更、验证与待办。将本文件加入设计文档索引。

### 2026-11-27 · 环境摆件并入绘制

- 在「绘制 → 环境摆件」中用缩略图选素材，再在 Scene 视图内单击放置。支持顶部或底部对齐、缩放、选中后调整位置、删除和撤销。新摆件放在所属房间的 `Environment Props` 子节点下。
- **验证**在 Unity 中确认四个绘制图层各自读取对应资源，5 张摆件均可选。单独测试了摆放边界、房间归属、对齐、渲染顺序，以及放置和删除的撤销。 [RoomMapWindow.cs](../Assets/Scripts/Game/Level/Editor/RoomMapWindow.cs)

### 2026-11-27 · 房间背景改为图片

- 通过背景缩略图给房间指定整张图片。支持等比填满并裁切、完整显示留边和取景调整。新建房间直接配置图片背景，已有场景中手动放置的背景需要按房间迁移。室内样板房已迁移。
- **验证**测试了紧邻的宽房和窄房，两种适配方式及不同取景位置均保持比例和房间边界。确认样板场景保存重开后能加载背景，也验证了图片的应用与清除。 [MapRoomBackground.cs](../Assets/Scripts/Game/Level/Runtime/MapRoomBackground.cs) · [IndoorMapSample.unity](../Assets/Scenes/IndoorMapSample.unity)

### 2026-11-27 · 地图素材分类、导入与瓦片绘制

- 分类迁移 28 张地图 PNG，保留原贴图 `.meta` 和 GUID。生成室内外 14 个新 Tile，导入 13 张环境 Sprite，并搭建了室内样板房。
- 地面与装饰各自绑定调色板目录。瓦片网格支持缩放、旋转、水平或垂直翻转、吸取朝向和场景预览。修正了调色板缩略图翻转时可能消失的问题。
- **验证**在 Unity 中确认调色板扫描、瓦片变换、吸取和撤销。验证了样板场景保存后可重开，地面和平台碰撞正常，实际铺下的瓦片翻转后可渲染。调色板翻转修复后的缩略图未通过截图确认。 [RoomMapWindow.cs](../Assets/Scripts/Game/Level/Editor/RoomMapWindow.cs) · [美术资源放置规则](../Assets/Game/Art/资源放置规则.md)

## 新记录格式

```markdown
### YYYY-MM-DD · 功能或修复名称

- 完成的行为与关键文件或资源位置。
- **验证**实际执行的检查及其结果。
- **待办**仍需验证、跟进或交接的事项。没有时可省略。
```
