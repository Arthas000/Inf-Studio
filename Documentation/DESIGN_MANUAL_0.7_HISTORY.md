# InFalsus Studio 0.7 — 设计契约

覆盖0.6中的左Ctrl把手、单对象Apply、每段Sky默认hit、96FX声道和底部进度条。保留手动保存、计数候选及既有几何。不使用历史测试数字说明新版实际运行。

## 冻结与改动

CameraCalibration、StageSpace、StageRenderer、StudioProfile、FlickGeometry、AutoplayCursor、ManualSongSession、SongMetadataEdit、ProvisionalComboTimeline、TickCountResearch、PreviewScore、3 Shader、profile、场景、6个音效均保持0.6字节。完整baseline列表见Reference/v07_frozen_baseline.json。

StudioTransport只加音乐优先级0/诊断（原DSP锚点算法保留）。NoteRenderer增加ghost/忽略cut源/连接头框/针长/细分参考色；不改音符坐标契约。基准BPM通过runtime profile.unitsPerSecondAtSpeedOne归一化视图，不写chart头：canonicalUnits*(reference/initialChartBpm)。初次默认reference=initial，保持旧观感。

## 文件职责

| 文件 | 职责 |
|---|---|
| Core/SelectionAuthoring | 同型字段修改、段落END选取、首尾分别对齐；只改传入RAM文档 |
| Core/FloatingNotes（同上文件） | 复制剪贴源快照、小预览文档、精确时间偏移、正式提交 |
| Core/SkyConnections | 首尾时间+.001ms容差和横向足迹连接，exact duplicate去重 |
| Core/AudioVoiceBudget（同上文件） | real voices到FX预算、priority契约 |
| Core/Release07SelfTests | 实际C#回归入口，需用户执行 |
| Runtime/EditModifierGate | Alt物理状态，Windows VK_MENU；旧LeftControlGate保留但不再接线 |
| StudioBootstrap.SelectionWorkflow | 浮动复制剪贴、段选状态、共享最近屏幕网格求值 |
| StudioBootstrap.NotePanel | 同型批量live表单、0.28s数字防抖、source过期检测、原文Ctrl+Enter |
| StudioBootstrap.Chrome | 四周UI、图标、本地化、基准BPM、进度条、侧签、退出确认 |
| Rendering/HitFeedbackRenderer | 有预算的stateless程序化VFX |

## 内存与事务

NoteProperties.Apply名称只是已有纯数据方法，不是保存文件。批量一次History.Execute：每个源对象Capture最新表单，只写操作key。第一对象有效第二无效也要整体rollback，不留下半次修改。源SourceId/OriginalSource/History/selection快照同时验证，防止过期表单覆盖新对象。

普通选项队列下一Layout改RAM；数字输入防抖防止不完整token，改另一选项前合并有效待提交数值。不同参数只改本次字段。Save先接收合法待输入值，再沿ManualSongSession保存全部dirty difficulties和metadata。不在OnDestroy/timer切难度写谱；旧SOURCE Apply逻辑升级为Ctrl+Enter显式原文校验。

## 选区和浮动状态

Idle → RangeStart → RangeEnd → Selection；Idle/Selection → FloatingCopy/Cut → Drop/Cancel。

Range按闭区间，Hold/Sky使用EndMs，其他TimeMs。完成得到普通selection集合，不另建特殊对象类型。Ctrl切换成员，Alt门控拖动，混合类型仍有上下文命令但没有参数面板。

Copy/Cut开始保存selection和primary，复制源行进小预览文档；Preview只改小文档。Cut不执行Delete，仅NoteRenderer/Timeline忽略原ID；取消放弃对象并恢复selection，无History记录。Drop一次Execute，Copy调用NoteClipboard.Paste，Cut验证原行没变后EditOperations.Move原ID。

复制锚是最早头时间，其他相对时间精确保留，不自动regrid。显式Align逐头尾Nearest，然后求duration，先全体验证再写。与原生未知参数/group策略共存，不用Mesh反向生成SPC。右键按下先处理草稿取消并Use事件，不能取消后继续唤出径向菜单。

PointerAuthoringTime先射线→真实六轨面→候选时间，再比较Nearest及其相邻线在鼠标处的屏幕距离，返回与DrawTimeGuide一致的原网格时间。停流多分支沿用上下文，Time layout辅助不修改track。ScrubProgress只seek不CancelGesture，滚轮也保留草稿；播放/换谱是明确放弃草稿边界。

## UI/GUI安全

依旧是IMGUI覆盖，不引入第二输入系统/新包，不为面板移动相机。Layout冻结guiShowUi/selectionform类型/列表和workflow状态。播放时guiShowUi=false；HUD/进度/Play固定Rect保留。不同种panel字段只在Layout切换，不能在Repaint按live选择重建控件树。GUI修改磁盘/模态对话框仍排到Layout开头。

基准1920×1080：SCORE(33,31)，top strip(525,7,795,54)，Play(12,102,84,43)，progress(1338,129,565,19)。Note/Tools在左侧。SideDifficulty0..3源对应easy/medium/hard/xtreme；未选83×134，选中112×167，绘制与hit顺序都在上层。

语言通过L(zh,en)主流程，OS字体动态加载；不附字体文件。小图标用GUI线条，不依赖Unicode emoji和外部iconfont。高级诊断部分英文未全量翻译。

## 音频与Sky连接

Unity virtualizes低优先级音源时完全静音而保持播放状态，与用户现象相符，但此环境未做Audio Profiler实录。代码明确修补risk：music0，hold48，shot160；shotBudget=min(20,max(0,real-7))，holdBudget=4（real>=7）否则max(0,real-2)。音乐不放进效果池，不被StopAll触碰。预排voice用BusyUntil，不以isPlaying=false错误复用尚未起播的源。

同样本同原时刻只用一攻击声道，声道满时丢额外FX并计数，不改变谱/物量/score。真实配置每秒读取，不调用AudioSettings.Reset、不改ProjectSettings。若用户额外AudioSource优先级也为0，仍可能有外部竞争，要看诊断，不保证任意工程无音频问题。

连接要求前段End与后段Start约同毫秒且当前足迹接触/重叠；不是全按group，不把时间相同但位置分离的Sky合并。Starts同时控制攻击音和头框。两类hold beds各自时间并集，因此同种重叠不倍增volume、floor与sky可同时发声。这是编辑器试听规则，不宣称解明官方SkyTap类型。

## VFX与针

VFX从chart time直接求age/持续active，不累计ParticleSystem时钟。Tap使用26ms快亮、余314ms平方衰减，Hold波动扇形短带，Flick在输入方向侧高端闪光，Sky current L/R范围漂浮三角。Flick用VisualContactTime只对齐可视扫动，不让其100ms延迟进入音频和计分。FX上限128对象，1个批次，不成百新建GameObjects。

Needle的Y仍固定于sky平面，沿相机屏幕y=.75 ray-plane求终点，世界Z有有限保护。头括框在真正source head随纵深透视，边缘贴墙时向内画；不让几何发光越界。ghost透明度约.28，使用独立透明批次，无cursor/grid/playfeedback，且不进入实际selection hit集合。

## 证据与未验证

Reference/v07_verification.json：121项独立Python参考/源码契约/资源检查（不是C#测试）；source_layout_check：67份有限词法检查。C#提供Release07SelfTests，原有全部Core检查继续接入。真实Unity键鼠、声音、GPU与Windows打包未执行。

官方接口参考（不是本项目测试结果）：
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource-priority.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioConfiguration-numRealVoices.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource-isVirtual.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility.ExitGUI.html
