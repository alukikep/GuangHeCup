# Module Factory Audio / 通用音频模块 0.2.0

Unity **2022.3+**，当前在 2022.3.62f2c1 验证。无第三方运行时依赖。

## 快速开始

1. 模块随工程打开即可使用。
2. 运行 `Tools > Audio > Quick Setup`，创建项目配置、Mixer 和当前场景的 AudioRoot。保存场景。
3. 相机保留一个启用的 AudioListener。
4. 通过 `Assets > Create > Module Factory > Audio > Cue` 创建音频配置，向 Clips 拖入 AudioClip。
5. 添加 AudioEmitter 并指定 Cue，使用 Play On Enable，或将其 Play 方法绑定到 Button.onClick 等 UnityEvent。

代码调用：

```csharp
using ModuleFactory.Audio;
using UnityEngine;

public sealed class WeaponAudio : MonoBehaviour
{
    [SerializeField] private AudioCue fire;
    [SerializeField] private AudioCue engine;
    private AudioHandle engineHandle;

    public void Fire() => GameAudio.PlayAt(fire, transform.position);
    public void StartEngine() => engineHandle = GameAudio.PlayAttached(engine, transform);
    public void StopEngine() => engineHandle.Stop(0.3f);
    private void OnDisable() => engineHandle.Stop();
}
```

只需要播放音效的调用方可以持有 `IAudioPlayback`。需要音乐、音量或偏好管理时使用 `IAudioService`。接口均在主线程调用。
用 `AudioEmitter.SetPlayback` 可以在首次播放前提供播放服务，传入 `null` 恢复使用 `GameAudio`。
自定义播放服务可用自己的 `IAudioVoiceControl` 构造 `AudioHandle`，并忽略过期的槽位与版本组合。

## 示例

打开 ../../../Examples/Audio 下的 AudioPlayground.unity 后进入 Play。
示例在运行时合成测试音，不分发外部音乐素材。

## 常用控制

```csharp
var audio = GameAudio.Service; // 在 AudioRoot 初始化之后获取
audio.PlayMusic(menuMusic, 1f);
audio.PauseMusic();
audio.ResumeMusic();
audio.SetVolume(AudioBus.Music, 0.6f);
audio.SetMuted(AudioBus.SFX, true);
audio.SetWorldPaused(true); // 暂停世界音效；UI 和音乐不受影响
audio.SavePreferences();   // 明确保存，不会每次拖动滑条都写盘
audio.StopBus(AudioBus.Master, 0.2f);
```

`Time.timeScale = 0` 不自动暂停音频；项目在暂停菜单中调用 SetWorldPaused。
音乐单独暂停。用户 Pause 和世界暂停分别记录，恢复世界不会解除用户暂停。
没有 AudioRoot 时门面播放返回无效句柄，不创建隐藏管理器。

## 配置规则

- Play 始终是 2D；PlayAt / PlayAttached 使用 Cue 的 Spatial Blend。
- Cue Volume 与 Pitch 是随机范围；音量倍率范围 0–1，最终 Pitch 限制 0.01–3。
- Priority 数值越大越重要；池满时只抢占优先级不高于新请求的声音，先最低优先级，再最早播放。
- Max Instances 与 Cooldown 按整个服务内的 Cue 统计；音乐播放器不使用音效的并发、冷却策略。
- 每个 AudioEmitter 同时拥有一次播放；再次 Play 会替换原播放。需要重叠音效时直接调用服务。
- 默认音效归当前活动场景；跟随音效归目标所在场景。场景归属在播放时固定，不随目标后续迁移变化。
- 增量加载场景中的定点音效，请在 AudioPlayOptions.OwnerScene 指定实际归属场景。
- Global 声音跨场景保留；跟随目标销毁默认停止，可选 ContinueAtLastPosition。
- 音乐属于全局，在独立的两个音源上播放；循环由 Cue 的 Loop 控制。
- 连续切歌最多保留两路。第三首到来时替换当前更安静的一路，不保证三首同时平滑叠加。
- 重复请求当前音乐不会从头重播；主动 StopMusic 后可重新播放。

## Mixer

Quick Setup 提供 Master > Music / SFX / UI / Voice，暴露对应的 `MasterVolume` 等 dB 参数。
Settings 中可换成项目自己的 MixerGroup 和参数名。分类组必须处于配置的 Master 组之下；暴露参数只交给音频模块控制，避免同时由 Snapshot 或其他脚本写同一参数。

缺少 Mixer 或暴露参数时使用 AudioSource 音量回退；无 Mixer 也可播放。最终静音使用 -80 dB。
Mixer 参数从 Update 应用，不在 Awake 中调用 SetFloat。

## 调试与验证

运行时打开 `Tools > Audio > Diagnostics` 查看活跃音源、淡出尾音源、分类占用、音乐准备状态、错误、预算拒绝次数和最近拒绝原因。

Test Runner 的 EditMode 包含初始化及 Mixer 测试，PlayMode 包含句柄、并发、暂停、场景清理、自然回收等测试。

## 当前边界

- 直接引用 AudioClip；不会自动卸载资源，不包含 Addressables 适配。
- 不包含节拍同步、DSP 段落衔接、混响区域、对白队列或 Ducking。
- AudioRoot 使用项目现有的 AudioListener，不自动添加。
- 不强制修改项目的音频导入设置。长音乐可按平台选择 Streaming，短音效按预算选择解压策略。
- 当前版本为 0.2.0，接口在生产使用前应先按目标平台进行试听与性能验证。

设计与维护说明见 `../../../../Documentation/AudioDesign.md`。

## 资源加载与安全切歌

PlayMusic 现在是一个请求：准备数据 → 静音启动新轨道 → 确认 isPlaying → 新旧轨道交叉淡化。
在准备期间，旧音乐继续播放；失败或超时保留仍在播放的旧轨道，错误可通过 LastMusicError 获取。
仅保证保留现存轨道；非循环旧音乐如果自然播完，不会被强制循环续播。

```csharp
audio.PlayMusic(nextMusic, 1f);
// 可在加载 UI / 调试界面观察；无需每帧再次请求同一首。
var status = audio.MusicStatus;
var pending = audio.PendingMusicCue;
var error = audio.LastMusicError;
```

- 新请求替代旧的待准备请求；StopMusic 取消待准备请求。过期加载完成不会重新开始播放。
- 重复请求正在准备的同一个 Cue 不重置加载或超时。
- Music Load Timeout 默认 10 秒，按未暂停的 unscaledDeltaTime 统计。
- 暂停音乐期间不启动新轨道，也不推进准备超时；恢复后继续。
- MusicStatus 是最近请求的状态，不是“当前是否有声音”的判断。Failed 时旧音乐仍可能播放，Cancelled 时停止淡出也可能仍在进行。
- 已经交叉切换中的第三次切歌仍受两路上限约束，必要时清理较安静的一路。

Unity 在 Load In Background 关闭时调用 LoadAudioData 可能同步加载。模块默认拒绝对未就绪且不支持后台加载的素材发起这种加载。
请在音频导入设置启用后台加载，或在进入游戏前完成预加载。Allow Synchronous Loads 是明确的兼容开关，打开可能阻塞主线程，不应作为消除卡顿的方案。

加载关卡时提前准备 Cue 中的所有片段：

```csharp
// 此方法返回数据状态，不创建音源，也不改变随机播放历史。
var state = audio.Preload(explosionCue);
// Loading 时可在后续帧再次查询；Loaded 后再允许依赖此素材的游戏流程。
// Failed 时检查素材导入设置和资源是否有效。
// 仅在允许同步加载的加载阶段：audio.Preload(explosionCue, allowSynchronous: true);
```

即时音效只播放数据已就绪的片段。数据未就绪时返回无效句柄，并记录拒绝原因；不会为了补播而排队，从而避免过时脚步/枪声延迟响起。
上层如有异步资源系统，可实现 IAudioClipLoader 并传给 AudioService 构造函数；此接口仅准备已引用 AudioClip 的音频数据，不是 Addressables 资源提供器。

**本版不宣称绝对无卡顿。** 资源对象的加载、平台解码和流式缓存仍需要真机验证。取消请求只放弃本模块的等待，不中断 Unity 的共享后台加载，也不自动 UnloadAudioData，避免影响其他正在使用同一片段的系统。

## 突发播放、分类配额和抢占尾音

Settings 新增这些配置：

| 字段 | 默认值 / 含义 |
|---|---|
| Max Requests Per Frame | 64；所有播放入口共用的每帧请求预算，超出后快速拒绝 |
| Max Steal Tails | 4；预创建的短淡出尾音源数量，最多不超过主池容量 |
| Steal Fade Seconds | 0.02；被抢占的声音短暂淡出；0 表示直接停止 |
| Bus Policies | 分类上限、保留名额、分类每帧请求预算；留空则仅使用全局规则 |

新建 Quick Setup 配置提供：SFX 最多 24 路 / 每帧 48 次；UI 最多 4 路 / 保留 2 路 / 每帧 8 次；Voice 同 UI。
保留名额从全局主池容量内划分，其他分类不能占用空闲保留名额，也不能抢走处于保留数量内的已播放声音。
分类达到上限时，可在该分类内按优先级替换；不会为了突破分类上限而抢占其他分类。
保留数量之和超过主池容量、重复分类或保留数超过分类上限时，初始化会明确报错。

分类请求预算与声音名额不同。全局每帧请求预算是共享的，保留音源名额不保证在全局请求预算耗尽后仍接受 UI 请求；按游戏事件量合理设置分类预算。
预算根据 Time.frameCount 重置，单帧多次 Tick 不会重置预算。超过预算后不继续遍历音源、随机选片或创建运行状态。

抢占时把正在播放的 AudioSource 转移到尾音槽，主槽改用备用源，不重新播放或跳转旧声音。
尾音容量不足时拒绝新请求，保留原声音，不额外分配音源。暂停或已经结束的声音可直接替换。
主槽立即失效旧句柄，尾音由系统控制，支持分类停止、暂停和场景清理。
Max Steal Tails 设为 0 时禁用尾音保护，抢占直接停止旧声。

实际音源总上限为：**Max Voices + Max Steal Tails + 两路音乐**。
尾音不计入主池并发 / Cue 并发 / 分类配额，因此过渡的短时间内总可听数量会比主槽略多。

这些机制限制播放数量和调度工作，不是响度限制器。大量音频仍需合理的素材电平与 Mixer 增益；本版没有自动增益、Ducking 或砖墙限幅。

## 从 0.1.0 升级

- 保留已有 Settings / Cue / Mixer 的 .meta 和资产引用；不需要重新创建它们。
- 旧 Settings 的新增参数使用默认值，Bus Policies 留空；Quick Setup 不覆盖旧配置。
- 根据实际项目，在已有 Settings 中手工添加分类策略；修改容量或配额后重新启动 AudioRoot。
- 新增资源就绪检查后，原先依赖首次播放同步加载的素材可能被拒绝，请先配置后台加载和预加载流程。
- 同时更新了 IAudioService；项目如果有自己的接口实现或 Mock，需实现新增的音乐状态属性和 Preload 方法。
