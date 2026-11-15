# Simple Events

用于场景内玩法通知的轻量事件总线，命名空间为 `ModuleFactory.SimpleEvents`。消息按 C# 类型路由，在调用 `Publish` 的线程同步派发。在 Unity 中，请在主线程调用。

## 接入场景

1. 给场景根物体添加 `SceneEventBus`。通过 Inspector 将同一个组件绑定给发布者和监听器。
2. 在玩法程序集定义消息，例如 `readonly struct ScoreChanged`，携带更新后的分数。计分组件先更新自己的分数，再发布消息。
3. 监听组件在 `OnEnable` 注册，在 `OnDisable` 释放订阅。重新启用时，从业务组件读取当前状态，再用事件处理后续变化。

```csharp
// 发布方，已通过 Inspector 绑定 sceneEvents。
CurrentScore++;
sceneEvents.Bus.Publish(new ScoreChanged(CurrentScore));

// 监听方，OnEnable 中。
subscription = sceneEvents.Bus.Subscribe<ScoreChanged>(OnScoreChanged);
RenderScore(score.CurrentScore);

// 监听方，OnDisable 中。
subscription?.Dispose();
subscription = null;
```

纯 C# 场景下可以直接创建 `new EventBus()`，由调用方管理实例和订阅生命周期。

## 派发规则

- 每个总线实例独立。每次订阅都有独立句柄，即使注册的是同一个回调。句柄可以重复释放。
- 按注册顺序派发。派发途中新增的监听器从下一次发布起参与。已经释放且尚未执行的监听器立即跳过。
- 嵌套发布会读取最新的订阅，并在外层派发继续前完成。
- 监听器抛出的异常会结束本轮派发并传给发布方。业务状态由发布方管理。
- 通知只交给本次派发的有效监听器。初始化界面时从业务组件读取当前值。

在 Unity 中打开 [SimpleEventsPlayground](../../../Examples/SimpleEvents/SimpleEventsPlayground.unity) 并进入 Play Mode。点击 **Add score**，观察 HUD 和事件记录。取消 **Show HUD** 后继续加分，再勾选它，HUD 会立即显示最新分数。EditMode 测试位于 `Assets/Tests/SimpleEvents`。
