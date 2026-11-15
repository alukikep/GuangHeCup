# Quick Start

1. 打开 `AudioPlayground.unity`，进入 Play 模式即可测试。
2. 场景已包含 AudioRoot、独立 Settings / Mixer、相机和 AudioListener。
3. 自建场景时执行 `Tools > Audio > Quick Setup`，给空物体添加 `AudioPlayground` 即可。
4. 左上角可测试音效、跟随循环、音乐交叉切换、暂停和音量保存。
5. 打开 `Tools > Audio > Diagnostics` 观察池和轨道。

0.2.0 新增 1,000 次 UI 请求按钮、世界音效暂停、音乐请求状态与预算拒绝计数。
示例配置启用分类配额、UI / Voice 保留名额和 20ms 抢占尾音。
测试音频由运行时生成，因此已在内存中；文件后台加载、失败与超时另有自动化测试，不能用该示例宣称已验证磁盘加载性能。

示例合成低音量正弦波，仅用于功能验证。实际游戏使用 AudioCue 资产与项目自己的素材。
示例脚本使用 IMGUI，不依赖 uGUI；核心音频模块没有 IMGUI 依赖。
