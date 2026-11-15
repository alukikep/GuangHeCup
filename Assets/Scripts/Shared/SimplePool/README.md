# Simple Pool：GameJam 轻量对象池

Unity 2022.3+。核心只有 4 个脚本，不依赖音频包、复杂对象池包、第三方库或物理模块。

## 接入

1. 模块随工程打开即可使用。
2. 场景创建一个空物体，添加 `SimplePool` 组件，保持启用。
3. 业务脚本通过 Inspector 引用这个组件和自己的 Prefab。

```csharp
using ModuleFactory.SimplePooling;
using UnityEngine;

public class FireExample : MonoBehaviour
{
    [SerializeField] private SimplePool pool;
    [SerializeField] private GameObject bulletPrefab;

    private void Start() => pool.Configure(bulletPrefab, capacity: 32, prewarm: 8);

    public void Fire()
    {
        var bullet = pool.Spawn(bulletPrefab, transform.position, transform.rotation);
        if (bullet == null) return; // 已到容量上限
        pool.DespawnAfter(bullet, 3f);
    }
}
```

## 基本接口

| 接口 | 行为 |
|---|---|
| Configure(prefab, capacity, prewarm) | 配置该 Prefab 的总容量，并同步预热；不能缩到已有数量以下 |
| Prewarm(prefab, count) | 使总实例数至少达到 count，不超过容量 |
| Spawn(prefab, position, rotation, parent, initialize) | 取出并激活；容量满、无效 Prefab、组件禁用或回调失败时返回 null |
| Despawn(instance) | 归还并禁用；重复归还、错误池、非池对象返回 false |
| DespawnAfter(instance, seconds, unscaledTime) | 延时归还；重复设置覆盖旧计时器，归还/复用时取消旧计时器 |
| DespawnAll() | 归还本管理器的所有在用实例，保留缓存 |
| Clear(prefab, includeActive) | 销毁该 Prefab 的缓存；默认包含在用对象 |
| ClearAll(includeActive) | 清理所有池；传 false 只销毁空闲缓存 |
| CountAll / CountActive | 总实例数 / 在用实例数 |

没有 Configure 的 Prefab 在首次使用时自动建池，默认容量 64，可在 SimplePool Inspector 中修改。
容量统计包括在用、空闲和已请求销毁但尚未真正销毁的实例。Clear 后本帧再次 Spawn 可能返回 null，下一帧可恢复。
预热是同步的，GameJam 建议在开始时预热少量对象；本包不实现分帧调度和请求预算。

## 状态重置与激活前赋值

对象自己的组件按需实现 IPoolable：

```csharp
public class BulletState : MonoBehaviour, IPoolable
{
    public int Damage;
    public void OnSpawn() { Damage = 0; }
    public void OnDespawn() { Damage = 0; }
}
```

OnSpawn / OnDespawn 都在对象未激活时执行。调用顺序为：设置位置与父节点 → OnSpawn 重置 → initialize 填本次数据 → 激活。

```csharp
var bullet = pool.Spawn(bulletPrefab, position, rotation,
    initialize: go => go.GetComponent<BulletState>().Damage = 10);
```

Awake 在首次激活前可能尚未执行；重置方法应自己获取组件或延迟初始化，不依赖 Awake。不要在 Awake 再覆盖刚填好的业务数据。
核心只恢复根对象位置、旋转和 Prefab 根缩放。速度、命中记录、子物体状态、事件订阅、Tween、粒子等由业务回调处理。
回调异常时记录错误并销毁该实例，避免脏数据再次复用；回调里不支持重入同一个池管理器的修改方法。

## 场景、暂停与生命周期

- 默认随 SimplePool 所在场景销毁；销毁管理器会清理它拥有的实例，包括挂在外部父节点下的实例。
- 不指定 parent 时，生成对象挂在管理器下；移动/缩放管理器会影响其子对象，建议池管理器保持原点及单位缩放。
- 指定未激活父节点时，实例 activeSelf 为 true，但仍不可见；CountActive 表示“在用”，不是画面上可见的数量。
- 关闭 SimplePool 组件时归还全部在用对象；重新启用可继续使用缓存。
- 可勾选 Persist Across Scenes，管理器必须位于层级根节点；不提供自动单例去重。
- 默认计时使用 Time.deltaTime；unscaledTime=true 使用 Time.unscaledDeltaTime，不随 timeScale=0 暂停。
- 手动 SetActive(false) 不等于归还，请调用 Despawn。
- 外部 Destroy 的实例会在 Update 或下次取用时清理记录。

## 边界

这是独立于 `com.modulefactory.pooling` 的简单版，不要求安装复杂版。
没有租约 API、ScriptableObject 配置、分帧预热、全局多池预算、物理/粒子适配器、Addressables、编辑器控制面板。
池内部计时器不会误回收下一次复用，但业务自己保存的旧 GameObject 引用无法区分前后两次使用；外部协程和异步任务应在归还时取消。
不应手动激活空闲缓存、删除内部 PooledObject、移动 Pool Storage，或在回调中自行激活/销毁管理器。

## 示例与测试

打开 ../../../Examples/SimplePool 下的 SimplePoolPlayground.unity，进入 Play 模式。
示例使用运行时创建的方块、默认 32 容量、2 秒回收，提供单发、突发和清理按钮；示例材质使用 Built-in 渲染管线的 Unlit/Color。
核心不受渲染管线限制。

测试需安装 Unity Test Framework，并在项目 manifest 顶层添加 `"testables": ["com.modulefactory.simple-pool"]`。
在 PlayMode 中验证复用、激活前初始化、容量、重复归还、错误池、旧计时器、暂停、外部销毁和清理。
