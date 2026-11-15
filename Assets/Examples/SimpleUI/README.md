# 主工程 UI 演示

打开 SimpleUIPlayground.unity 并 Play。场景中的 UIManager、三个层级、六个面板和按钮引用均已保存，可在编辑模式检查和修改。

菜单 → 设置 → 确认弹窗；开始游戏 → 暂停 → 恢复或结算 → 返回菜单。

使用 Packages/com.modulefactory.simple-ui 的运行时代码，不复制核心脚本。示例业务控制器仅负责按钮和暂停。确认重置为流程演示。EventSystem 使用旧输入模块，项目需启用 Input Manager 或 Both。

若场景尚未生成，等待 Unity 编译，或使用 Tools > Module Factory > Simple UI > Create Playground。生成器不会覆盖已有场景或保存当前打开的业务场景。

## 黑屏转场

打开 `Transitions/FadeDemoA.unity` 并 Play，演示普通及暂停黑屏、反向、取消恢复和双场景加载。两个场景已加入本工程 Build Settings，共享组件为 `UIScreenFade` 和 `SceneFlowLoader`。
