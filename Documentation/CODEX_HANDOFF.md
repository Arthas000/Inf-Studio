# Codex 接手 — InFalsus Studio 0.9

用户已有可用Unity6000.3.24f1/URP工程，0.8已经使用。继续这套工程，不搬Rust/不重做场地。0.9源码尚未在交付环境编译，必须先Unity完整编译和CoreSelfTests，再运行测试、最后BuildPlayer。不能引用历史静态检查数字声称编译通过。

先读EDITING_GUIDE_0.9、DESIGN_MANUAL、COLDSEA_UNKNOWN_EVENTS、TEST_REPORT、BUILD_WINDOWS及ARCADE_PLUS_COMPARISON。ARCADE_PLUS_REVIEW_TASKS是后续对比工单，不是让你现在一次移植所有功能。

## 本轮重点

- 当前组浏览在流速和referenceBpm之间。选择Sky跟随其组；手输组不改note；严格前后按source time，不循环。offgrid源起点精确导航，滚轮后才重新吸附。
- key samples.pitch=1，攻击样本end=startDSP+clip.length；床结束仍按chartTransport. 音乐Rate保持。切倍率仍用revision撤旧排程，不把key样本拉长/变调。
- 当前背景是远端camera-child quad、Queue Background。附件2048x1024不是16:9，默认cover裁边，stretch可选。不改相机矩阵、不挡note、不做拾取。
- Windowed/BorderlessWindow/Exclusive/FullScreenWindow区分；edge resize原生hook只在本PID UnityWndClass，恢复WndProc时不覆盖后装hook。F11恢复普通窗口。此路径未Windows实测。
- Coldsea三合法bpm在源1477..1479，而2954..2956为转换工作行推断。匿名JSON载荷不能猜；配对明文补入严格校验，no-op repeated，unknown保留，一步undo、Save之前不写。

## 不可退回

Enter/blur仅RAM；Ctrl+S才保存所有dirty difficulty和metadata，不自动写Recovery、不退出保存。Alt把手、Ctrl复选，RMB草稿取消优先于菜单，复制剪贴有预览可滚动且取消不动原文。Sky group首条有head/hit、后续gap不新头；编辑相接重叠自动继承但分开不拆。BPM只决定节拍/计数，track决定视觉积分。Sky group绝不等同Arcade timinggroup。

CameraCalibration/StageSpace/StageRenderer/FlickGeometry/Profile/Shader基线不动。未知token、split有理表达、手输非网格time、.meta GUID均保留。评分仍明确AUTO EXACT估计，不从Arcade拿Arcaea计分公式代替研究。

## 本轮实际回归

运行全部旧CoreSelfTests+Release09SelfTests；Tests/CoreChecks.csproj可在.NET8纯Core运行（不证明Unity）。用09_GroupNavigation检查小数时刻；手输99空组；重选Sky与手输组的优先级；切难度/undo后following状态。

音频25/50/75/100%：所有攻击与持续source.pitch=1，触发仍DSP跟music。开背景再反复进入/退出Play，检查Owned Texture/Mesh/Material无泄漏；PNG/JPG失效保持旧背景。窗口普通/无边框/全屏多次切换、负坐标副屏、DPI、F11、拖四边；检查UI命中与pixelRect一致。

Coldsea未改配对补入3行，undo原字节恢复；相同BPMidempotent、不同BPM冲突、改过音符、错误曲名同filename全部拒绝。关grid看未来137931/140000/140690小节。修时序只改RAM且保留opaque。

背景/显示偏好只有成功全局Save写本机presentation.v09.json。该偏好不属于跨文件谱面事务，写失败不可把谱面保存结果混成失败。外部背景路径迁移要提示重选。

## 后续比较

Arcade-plus固定d157e35cbc3af869094775333a89cf0918aac166、版本0.6.11、Unity6000.0.58f2；不是用户之前截图Arcade-Alpha1.12.1。只借模块模式，不合并Assets/Plugins/ProjectSettings，不改用户引擎。优先typed timing表、操作状态协调、索引/缓存、检查器、快捷键可配；详见比较文档。素材/字体授权分别核对，不打包仓库字体。
