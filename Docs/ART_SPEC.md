# GuangHeCup —— 2D 像素美术规格 v0.1

2026-11-23 · **待你确认 PPU 后定稿**

配套：`ARCHITECTURE.md`（结构）、`BALANCE.md`（数值）。
本文所有数字都是**硬约束**：定下来之后，所有美术、相机、UI、碰撞体都跟着走，改一次要全项目重导，所以先确认再开工。

---

## 1. 基准规格

| 项 | 值 | 说明 |
|---|---|---|
| PPU | **32** | 1 unit = 32 px = 1 瓦片 |
| 内部渲染分辨率 | **480 × 270** | 16:9 |
| 放大倍数 | 4× → 1920 × 1080 | 整数缩放 |
| 正交相机尺寸 | 4.21875 | = 270 / (2 × 32) |
| 可视格数 | 15 × 8.4 格 | 横版推进的视野基准 |
| 画布最大尺寸 | 2048 × 2048 | 图集上限 |

**为什么是 480×270**：

- 1080 / 270 = **4**，整数倍，没有半像素。
- 32px 瓦片下可见 15 格宽，横版战斗里"能看到一个敌人 + 出招距离"刚好。
- 角色 32×48 时占屏高约 18%，不会被放大成"大头娃娃"。

备选：320×180（6×）颗粒更粗、更复古；640×360（3×）细节更多但画量翻倍。**默认选 480×270**。

---

## 2. 角色尺寸

| 对象 | 画布 cell | 实际占用 | 说明 |
|---|---|---|---|
| 玩家 | 64 × 64 | 32 × 48 | cell 留出武器挥砍溢出 |
| 刀兵 | 64 × 64 | 32 × 32 | |
| 盾兵 | 96 × 96 | 48 × 48 | 盾牌可溢出 |
| Boss | 128 × 128 | 96 × 96 | 占位 |
| 投射物 | 32 × 32 | 8~24 | |
| 特效 | 64 × 64 | 可变 | |

**取景框（pivot）统一设在底边中心**，这样脚底永远对齐地面，换帧不会"上下抽动"。

---

## 3. 瓦片与场景

| 项 | 值 |
|---|---|
| 瓦片尺寸 | 32 × 32 |
| 地面层 | `Tilemap_Ground` |
| 墙体层 | `Tilemap_Walls`（含碰撞） |
| 装饰层 | `Tilemap_Decor`（无碰撞） |
| 关卡高度基准 | 8 格可视，实际关卡 10~14 格高 |
| 墙面厚度 | 1 格 |

不要用"大图拼接场景"（一张背景图拉满），必须走 Tilemap：以后做关卡编辑器、做地图解锁，都依赖瓦片数据。

---

## 4. 动画规格

**帧率必须是 60 的约数**，否则动画和 60fps 的判定对不齐，会出现"看着像 1 帧的错位"。

| 动画 | 帧率 | 帧数 | 时长 | 备注 |
|---|---|---|---|---|
| idle | 10 | 6 | 0.60 s | 呼吸循环 |
| run | 12 | 8 | 0.67 s | |
| jump | 20 | 3 | 0.15 s | 起跳/顶点/下落 |
| 轻击 | 20 | 7 | 0.35 s | 与 `BALANCE.md` 的 21 帧对齐（3:1） |
| 重击 | 20 | 15 | 0.75 s | 45 帧 / 3 |
| 重锤特殊 | 20 | 19 | 0.93 s | 56 帧 / 3 |
| 防御 idle | 10 | 2 | 0.20 s | |
| 完美防守 | 30 | 3 | 0.10 s | 短促闪 |
| 受击 | 20 | 3 | 0.15 s | |
| 死亡 | 10 | 5 | 0.50 s | |

判定窗口是"帧数据 → 美术帧"换算的：`AttackProfile.Windup = 7 游戏帧 = 2.3 美术帧 @20fps`。**允许美术帧与判定帧不完全对齐**，但前摇的最后 2 帧必须画出"出手"，让玩家读得到。

**不要用 2D Animation（骨骼变形）做角色**：像素精灵一变形就糊。序列帧更稳；2D IK 只在需要"武器跟手"时用。

---

## 5. 导入设置

### 5.1 单张 Sprite

| 项 | 值 |
|---|---|
| Texture Type | Sprite (2D and UI) |
| Sprite Mode | Single / Multiple（图集切片用 Multiple） |
| Pixels Per Unit | 32 |
| Pivot | Bottom Center |
| Mesh Type | Full Rect |
| Extrude Edges | 0 |
| Generate Mip Maps | off |
| Filter Mode | Point (no filter) |
| Compression | None |
| Max Size | 2048 |
| sRGB | on |
| Alpha Source | From Input |

### 5.2 Sprite Atlas

| 项 | 值 |
|---|---|
| 图集分组 | Characters / Enemies / Environment / UI |
| Max Texture Size | 2048 |
| Padding | 4 px |
| Allow Rotation | off |
| Tight Packing | off（像素网格必须对齐） |
| Filter / Compression | Point / None |
| Include in Build | on |

| 图集 | 预算 |
|---|---|
| Characters | 1 × 2048 |
| Enemies | 1~2 × 2048 |
| Environment | 2 × 2048 |
| UI | 1 × 1024 |

---

## 6. 排序与层级

`Sorting Layer`（从上到下）：

```text
UI               # Canvas 独立，不进世界层
VFX              # 刀光、命中火花
Foreground       # 前景遮挡（草丛、柱子）
Actors           # 玩家、敌人
Ground           # 地面、平台
Background_Near  # 视差 0.5
Background_Far   # 视差 0.2
```

`Order in Layer` 只在同一层内用：角色脚下阴影 -1、本体 0、武器 +1。

**视差速度**：远景 0.2、中景 0.5。远景不要低于 0.15，否则几乎不动，像贴纸。

---

## 7. 调色板

| 项 | 值 |
|---|---|
| 基础调色板 | 32 色 |
| 单角色上限 | 16 色 + 1 透明 |
| 禁用 | 半透明渐变、模糊、抗锯齿边缘 |
| 受击闪白 | 调色板内最亮色，不纯白（纯白会破色） |
| 阵营色 | 玩家冷色系，敌人暖色系 |

**受击闪白实现**：自写一个 Built-in RP 的 `SpriteFlash` 着色器（Unlit/Transparent，加一个 `_FlashAmount`），用 `MaterialPropertyBlock` 传参，避免每个角色实例化一份材质。

---

## 8. 特效

| 特效 | 帧数 | 说明 |
|---|---|---|
| 命中火花 | 5 | 20fps，0.25 s |
| 完美防守环 | 6 | 30fps，0.20 s，与 UI 完美环同色 |
| 重击尘土 | 4 | 10fps |
| 顿帧白闪 | — | 不加，靠顿帧本身和火花 |
| 慢放暗角 | — | 全屏 overlay，不用美术资源，用 UI 贴图 |

特效统一放 `Assets/Game/Art/VFX/`，走 `SimplePool` 池化，禁止运行时 `Instantiate`。

---

## 9. UI 规格

| 项 | 值 |
|---|---|
| Canvas 模式 | Screen Space - Overlay |
| Canvas Scaler | Scale With Screen Size |
| 参考分辨率 | 480 × 270 |
| Match | 0.5 |
| Pixel Perfect | ✅ 勾选 |
| 九宫格 | 边框 8 px |
| 图标尺寸 | 16 / 24 / 32 px 三档 |

**字体**：小字号（≤12px）用位图字体或把像素字体渲染成图集，避免 TMP 的 SDF 抗锯齿把字磨糊；大标题可以用 TMP。

**血条 / 完美环**：用 `Image` + Filled 模式（Radial 360），fill 值由事件驱动，UI 不自己算血量百分比。

---

## 10. 相机

```text
CameraRig
└─ Main Camera
   ├─ Pixel Perfect Camera
   │    Assets Pixels Per Unit = 32
   │    Reference Resolution = 480 × 270
   │    Upscale Render Texture = ✅
   │    Pixel Snapping = ✅
   │    Crop Frame = Letterbox
   ├─ CameraFollow
   └─ CameraShake
```

- `Upscale Render Texture` 勾上：世界先渲染到 480×270，再整数放大，边缘绝对干净。
- 注意：勾上之后**世界层**是像素化的，UI 走自己的 Canvas，不受影响。
- 跟随用 `LateUpdate` + 平滑阻尼；抖动用 `UnscaledDeltaTime`，暂停时也能抖完。

---

## 11. 命名与目录

```text
Assets/Game/Art/
├─ Characters/chr_player_{anim}_{nn}.png
├─ Enemies/enm_{type}_{anim}_{nn}.png
├─ Environment/{tiles,bg}_{name}_{nn}.png
├─ Tiles/tile_{name}_{nn}.png
├─ VFX/vfx_{name}_{nn}.png
└─ UI/ui_{element}_{nn}.png
```

规则：

- 全小写 + 下划线，**不用空格、不用中文**。
- 序号两位补零（`_01`），保证排序稳定。
- 动画名与 `BALANCE.md` / 代码里的枚举一致：`idle / run / jump / light / heavy / guard / perfect / hurt / death`。

---

## 12. Aseprite 工作流

项目已装 `com.unity.2d.aseprite` 1.1.9，可直接拖 `.aseprite` 进工程：

1. 在 Aseprite 里用 **Tag** 标好每段动画（`idle`、`attack_light`…），Tag 名与动画名一致。
2. 导入时 PPU = 32、Pivot = Bottom Center、Filter = Point。
3. 一个文件一个角色，图层：`body` / `weapon` / `fx`。
4. 导出帧不要裁切到内容尺寸（会破坏 pivot 对齐），保持统一 cell。

---

## 13. 战斗可读性（像素动作游戏的隐形规格）

这几条不是"美术风格"，是**功能性要求**，评审时按这个看：

1. **前摇必须三段式**：起手 → 蓄力 → 挥出。21 帧的前摇里至少要有 3 个可辨识姿势，否则玩家反应不过来。
2. **攻击预警配色**：高攻欲敌人用暖色描边/闪光，低攻欲用冷色——玩家不看 UI 也知道"这个要打我了"。
3. **完美防守四重反馈**：环闪 + 顿帧 + 慢放 + 音效，缺一个都会让"完美"感觉不到。
4. **受击与无敌要分得开**：受击闪白 2 帧，无敌期间角色半透明（或闪烁），两者不能长得一样。
5. **地面线要清晰**：横版最容易犯的错是地面和背景同色，跳跃落点读不出来。

---

## 14. 待确认

| # | 项 | 影响 |
|---|---|---|
| 1 | PPU = 32、480×270 是否确认？ | 定了才能出美术规格表 |
| 2 | 480×270 还是 320×180？ | 画量差 2.25 倍 |
| 3 | 角色 32×48 是否符合你的预期比例？ | 影响所有角色重绘 |
| 4 | 是否要 2D 光照（→ 切 URP 2D）？ | 切了所有材质要重做 |
| 5 | 调色板由谁定？32 色还是有现成色板？ | 影响所有像素画 |
