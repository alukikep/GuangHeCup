# Simple UI 0.2.0

轻量场景 uGUI 面板管理，命名空间 `ModuleFactory.SimpleUI`。仅依赖 uGUI。

## 接入

1. 在 Canvas 下创建三个铺满屏幕的同级 RectTransform：Normal、Popup、Overlay，保持激活。
2. 添加 UIManager 并拖入三个根节点。管理器放在持续激活的物体上。
3. 每个面板添加 UIPanel，作为对应根节点的直接子物体；将所有面板拖入管理器 Panels。
4. 配置 Layer、InitiallyOpen、CanGoBack、Modal；模态只用于 Popup。不要在面板内部添加 Canvas。
5. 通过 `ui.Open(panel)`、`ui.Close(panel)`、`ui.Back()`、`ui.CloseAll()` 调用。显示/隐藏时在 Shown/Hidden 中绑定业务。

通过代码创建面板时，在管理器初始化之前调用 `UIPanel.Configure`。

运行时搭建应先填引用再调用 Initialize；Inspector 配置默认在 Start 初始化。配置在初始化后不支持修改。打开已打开面板只提升同层顺序，不重复通知。Open 返回调用结束时是否打开，Close 返回是否实际关闭。

Back 按视觉顺序关闭一个可返回面板；不可返回模态会截断查找。共享遮罩随最上层模态移动。Overlay 的 CanvasGroup 禁止交互和射线，子级不要设置 ignoreParentGroups。遮罩只阻挡同 Canvas 的 uGUI 指针射线，不负责键盘焦点、世界输入或其他 Canvas。

通过 Open 管理显示，不直接 SetActive(true)。外部禁用/销毁会立即清理状态与遮罩；外部禁用的 Hidden 通知在 LateUpdate 发出，已销毁面板不再通知。管理器禁用时关闭所有面板，重新启用不自动恢复。CloseAll 的隐藏回调不能重新打开面板。避免事件形成无限递归开关。

暂停、设置保存、确认逻辑、数据刷新和音效由业务实现。面板管理器不自动处理输入、资源加载、面板动画或跨场景存活；屏幕渐变由下面的可选组件提供。

## 示例

打开 ../../../Examples/SimpleUI 下的 SimpleUIPlayground.unity 后进入 Play。场景包含菜单、设置、确认、暂停和结算按钮；需要项目启用旧 Input Manager 或 Both，示例使用 StandaloneInputModule。确认按钮仅演示关闭弹窗，不执行真实设置重置。

## 验证

在 Unity Test Runner 的 PlayMode 运行模块测试。测试覆盖返回、模态射线、重复开关、外部销毁、回调重入及暂停。实际鼠标流程、分辨率和目标设备仍需项目验证。


## 可选黑屏转场 UIScreenFade

在独立根物体上添加 `UIScreenFade`。组件自动配置 Screen Space Overlay Canvas、CanvasGroup、GraphicRaycaster 和全屏 Image，不需要 UIManager。不要放到面板 OverlayRoot 下；保持根物体激活，排序默认 1000，应高于要遮挡的其他 Canvas。

```csharp
using System.Collections;
using ModuleFactory.SimpleUI;

IEnumerator Transition(UIScreenFade fade)
{
    var request = fade.FadeOut(0.3f); // 画面变黑
    yield return request;
    if (request.Status != FadeStatus.Completed) yield break;
    // 这里执行传送、刷新画面或加载业务。
    yield return fade.FadeIn(0.3f);   // 恢复画面
}
```

- `FadeTo(alpha, seconds)` 指定透明度；`SetImmediate(alpha)` 立即设置；alpha 限制在 0～1。
- 使用不受暂停影响的时间。时长 <= 0 或目标已达到时立即完成。NaN/Infinity 抛出参数异常，保持原请求。
- 新请求取消旧请求，从当前透明度接续。`FadeRequest` 可 yield，状态为 Pending/Completed/Cancelled；等待结束不代表成功，需要检查状态。
- `Cancel()` 终结等待并保留当前透明度。需要恢复画面时调用 FadeIn 或 SetImmediate(0)。变黑后默认保持，不自动恢复。
- `Alpha`、`IsFading` 查询状态；`Color` 配置 RGB（颜色 alpha 忽略，由渐变统一控制）；`SortingOrder` 配置排序。
- `BlockInput` 默认开启：渐变中或 alpha > 0 时挡住 uGUI 指针射线，透明空闲时释放。关闭此选项仅显示渐变。
- 禁用/销毁时取消请求；禁用时清零并释放射线。再次启用不恢复旧请求。未激活时发起请求直接返回 Cancelled。
- 仅保证该 Canvas 能遮挡排序低于自己的 uGUI 指针操作，不接管键盘/手柄焦点、世界输入或其他高排序 Canvas。不对外部直接修改/销毁内部组件提供状态保障。

## 转场与加载示例

在 ../../../Examples/SimpleUI/Transitions 下打开 `FadeDemoA.unity`。两个场景已经搭好，可在编辑模式修改按钮、黑幕、颜色和组件引用。示例使用旧输入模块，项目需启用 Input Manager 或 Both。

按钮演示黑屏保持再恢复、暂停时转场、反向替换、取消并恢复、切换 A/B 场景。使用菜单 `Tools > Module Factory > Simple UI > Add Transition Scenes To Build Settings` 加入加载场景；保留已有条目，不在编译时自动修改 Build Settings。当前工程已加入这两个场景。缺失时按钮给出警告并恢复画面。

示例的场景加载由共享的 [Scene Flow](../SceneFlow/README.md) 负责。正式场景接入时，给独立的黑幕根物体挂上 `SceneFlowLoader`，并使用项目自己的目标场景路径。

生成器不会覆盖已有示例场景。若需要重新生成，请在已保存的场景中使用 Create Transition Scenes 菜单；存在未保存场景时生成器会跳过。
