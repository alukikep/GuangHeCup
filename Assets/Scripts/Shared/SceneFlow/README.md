# Scene Flow

`SceneFlowLoader` 用于带屏幕渐变的单场景异步切换，例如从主菜单进入关卡，再从关卡返回菜单。模块依赖共享的 `ModuleFactory.SimpleUI`。

## 接入正式场景

1. 在每个需要发起切换的正式场景里建一个独立根物体，添加 `SceneFlowLoader`。Unity 会自动添加 `UIScreenFade`。根物体只放转场相关组件及黑幕，加载时它会临时跨场景保留。
2. 在 Inspector 设置 `Fade Seconds` 和黑幕的颜色、排序及指针遮挡。把目的场景加入并启用在 Build Settings。建议使用完整的 Assets 相对路径以区分同名场景。
3. 在 Button 的 On Click 绑定 `SceneFlowLoader.LoadScene(string)`，填入自己的目的场景路径。业务代码可调用 `TryLoad` 判断请求是否被接受。

```csharp
using ModuleFactory.SceneFlow;

// loader 由当前场景的组件引用提供。
bool accepted = loader.TryLoad("Assets/Scenes/Level01.unity");
```

## 切换规则

- 渐黑、异步加载、等待目标场景一帧、淡入。淡入结束时移除旧场景的转场根物体。渐变使用不受 `Time.timeScale` 影响的时间。
- 所有加载器共用 `SceneFlowLoader.IsBusy`。等待当前切换结束后可再次发起请求。无效目标返回 `false`，并恢复当前黑幕和由它阻挡的 uGUI 指针。
- 渐黑期间禁用加载器会解除遮挡并取消尚未发出的加载。已发出的 Unity 加载会继续，忙碌状态至少保留到加载完成。
- `TryLoad` 的返回值代表是否接受请求。项目可用 `IsBusy` 限制玩法输入。资源预加载、目标场景的业务就绪条件和玩法输入由项目处理。

双向加载见 [转场示例](../../../Examples/SimpleUI/Transitions/README.md)。正式场景使用自己的目标路径和资产。
