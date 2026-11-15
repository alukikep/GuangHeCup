# Screen Transitions

打开 FadeDemoA.unity 并 Play。五个按钮分别展示普通黑屏、暂停时黑屏、反向替换、取消恢复及双场景切换。

通过 Tools > Module Factory > Simple UI > Add Transition Scenes To Build Settings 启用两个演示场景。示例使用旧 Input Manager；核心组件不依赖输入系统。

用 **Load FadeDemoB** 切到 B，再用 **Load FadeDemoA** 返回。示例按钮引用各自场景的 `SceneFlowLoader`，由共享模块完成渐黑、异步加载和淡入。

`Tests` 下的 PlayMode 流程测试覆盖示例按钮、双向切换、重复请求、无效目标和输入恢复。正式场景接入见 [Scene Flow 模块说明](../../../Scripts/Shared/SceneFlow/README.md)。
