# Keyboard Input

用 Unity Input System 将键盘按键映射为玩法和菜单动作。工程使用 `Active Input Handling = Both`，以便已有的旧 Input Manager 示例也能运行。首次从旧版输入设置切到 Both 后，重启 Unity 以启用新的键盘后端。

## 动作与默认键位

| 动作表 | 动作 | 默认键位 |
| --- | --- | --- |
| Gameplay | Move / Interact / Pause | WASD / E / Esc |
| UI | Navigate / Submit / Cancel | 方向键 / Enter / Esc |

在 Inspector 打开 `KeyboardActions.inputactions` 编辑默认键位。需要项目专用的默认配置时，可以复制这份预设。保留两张动作表及调用方使用的动作名。

## 接入

在场景脚本中绑定一个 `InputActionAsset`，启用时创建 `KeyboardActionSet`，禁用时释放。实例持有动作资产的独立副本，起始启用 Gameplay。通过 `GetGameplayAction` 和 `GetMenuAction` 取得动作。用 `ReadValue<T>()` 读取移动向量，用 `SubscribePerformed` 订阅动作。订阅的生命周期随动作集结束，也可以调用订阅时返回的委托提前取消。

```csharp
using ModuleFactory.KeyboardInput;
using UnityEngine.InputSystem;

var keyboard = new KeyboardActionSet(preset);
var move = keyboard.GetGameplayAction("Move");
var interact = keyboard.GetGameplayAction("Interact");
interact.SubscribePerformed(() => player.TryInteract());

// 暂停或恢复时由游戏流程调用，动作表由 KeyboardActionSet 管理。
keyboard.SetMenuOpen(true);
keyboard.SetMenuOpen(false);

// 在组件的 OnDisable 或退出输入上下文时调用。
keyboard.Dispose();
```

用 `TryBeginRebind(action, bindingIndex, callback)` 改一个 Gameplay 键位。对于移动组合绑定，传入要改的方向对应的子绑定索引。改键期间两张动作表暂停派发。Esc 或 `CancelRebind()` 可取消。同一 Gameplay 动作表内的键位冲突保留原绑定。UI 表和 Gameplay 表互斥，因此可以复用键位。用 `RestoreGameplayDefaults()` 清除本次运行的 Gameplay 改键。

打开 [键盘示例](../../../Examples/KeyboardInput/KeyboardInputPlayground.unity) 进入 Play Mode。WASD 移动，E 切换信标，Esc 打开暂停与改键菜单。选中 Interact，按 Enter 后按 F，再按 Esc 返回玩法，即可用 F 交互。
