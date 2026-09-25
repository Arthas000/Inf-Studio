# Arcade-plus 与 InFalsus Studio 0.9：源码对照、缺口与优化路线

## 0. 先划清这次比较的边界

对照对象固定为 `yojohanshinwataikei/Arcade-plus` 提交 **d157e35cbc3af869094775333a89cf0918aac166**，而不是浮动master；该提交说明为0.6.11，ProjectVersion记录 **Unity 6000.0.58f2**。[版本文件][unity]

用户之前示范视频/截图显示的是 **Arcade-Alpha 1.12.1**。这两个分支不能视为同一个功能清单。下文只把实际读到的源码功能列为“已确认”，没有根据截图反推Arcade-plus一定有Arc切分、复杂轨迹生成或多轨波形。我们也没有在本环境启动Arcade或编译两边Unity工程。

InFalsus基线是用户实际使用的0.8与本轮0.9增量源码。0.9原生窗口/背景/新导航尚待运行验收。“实现存在”与“本机验收通过”分开。

本次深入阅读18个关键文件的完整内容或明确范围，覆盖timing、grid、progress、commands、operations、copy/cut、property、project、input、fault、OBS、skin；并查看相关目录。每个已读范围见 `Reference/arcade_source_index_09.json`。没有把文件名存在当成完整实现已验证，也没有复制对方源码、字体或插件进本工程。

## 1. 最重要结论：借编辑架构，不换Infalsus语义

### 1.1 timing的名字相同，功能不相同

Arcade的`ArcTimingManager.CalculatePositionByTimingAndStart`累加：

`(segmentEnd-segmentStart) * timing.Bpm / BaseBpm * Velocity`

也就是BPM本身参加视觉位移，且不同timinggroup可有独立列表。[源码][timing-math]

InFalsus Studio的视觉位置是 `S(t)=∫trackSpeed(t)dt`，Z与S(noteTime)-S(playhead)有关；BPM用于音乐节拍、每小节分组和候选长条计数。**不能为了“像Arcade”把BPM乘回滚速。** Enigma慢速段、LightsOut22154渐速、Coldsea meter99都是现成回归。

### 1.2 Sky group不是AFF timinggroup

Arcade timinggroup包含它自己的Timing列表；增删组时处理组内Tap/Hold/Arc等。[源码][timing-editor]

Infalsus SkyArea group目前决定同组首次头框/头音与浏览分组；不是一份独立音乐/速度时间轴。0.9“当前组”只导航Sky，不能在里面克隆一套BPM/track再给所有音符加group属性。

### 1.3 保留本项目的硬约束

六轨中央宽键、空域固定高度/双边界缓动、左右Flick编码/固定参考高度、[0,1]可达范围、有理split、按原始时间求计数、用户指定的右键取消、Ctrl复选/Alt把手、**Enter/blur只改RAM与Ctrl+S唯一落盘**，这些不是有待用Arcade替换的临时代码。

## 2. 已有相似功能，不应重复造一遍

| 主题 | Arcade-plus已读实现 | InFalsus 0.9现状 | 正确的对比目标 |
|---|---|---|---|
| 时间数值与进度条 | AdeTimingSlider在拖动时写AudioTiming、ResetJudge，非拖动时跟随播放器 | 右上进度、输入结束定位、DSP音频锚点、循环/书签已有 | 比交互捕获与定位准确度，不把进度条误称完整多轨时间线 |
| 网格 | AdeGridManager分别缓存细分/整拍/小节并池化LineRenderer | AuthoringGrid统一绘制/高亮/吸附、强制网格、常驻小节 | 保留绝对格号、先取整一次规则；优化缓存和可见范围 |
| 点立得 | 操作类+异步SelectTiming/Position（目录和复制流程共同使用选择器） | 已有四步Sky、两步Flick、连续放置与RMB取消 | 比取消/失焦/重新进入状态，而不是替换用户已验证操作 |
| 复制/剪贴 | Prepare批量命令、实时更新克隆、Cancel反向命令、Commit入栈 | Cut不提前删除，只隐藏源；ghost可浏览且放下一次undo | 保留更保守的原数据策略；抽统一交互协调器 |
| 多选面板 | 同字段取交集，混合值用“-”，输入事件刷新 | 同型批量字段；混型按用户要求不弹；Enter/blur自动RAM提交 | 不把对方“混型仍显示公共字段”直接当本项目缺陷 |
| 撤销 | 命名ICommand/Do/Undo/Prepare/Commit/Cancel，可调栈容量 | 源行稳定ID、完整快照、原子批量与预览事务、200项栈 | 提高可追溯性/内存效率，而非取消事务 |
| 工程目录 | 五难度AFF、Project.arcade、base音频、封面 | 四难度SPC、manifest、JSON导入、音乐-only初始化、元数据无损patch | 差异是格式与保存策略，不是少一个难度 |
| 显示与窗口 | resolution/fullscreen/framecap偏好、UI四边、camera切换 | 显示预设+无边框窗口+背景picker已补；全屏UI隐藏已有 | 比DPI/缩放适配，禁止改标定相机来腾UI空间 |
| 声音/视觉预览 | 对方是Arcaea游戏模型和皮肤槽 | 六类key音、双持续床、group首音、自动指针、轻量VFX | 声道/性能和资源接口可借鉴，判定规则不得拿来替换 |

依据：[进度条][progress]、[网格][grid]、[复制剪贴][paste]、[属性面板][properties]、[命令][commands]、[项目][project]、[主UI][transport-ui]。

## 3. 优先改进：Timing编辑器与时间轴

### 3.1 typed、全量、稳定排序的时序表（P0/P1）

Arcade的AdeTimingEditor按当前时序组列出对象，用独立AdeTimingItem处理`time,bpm,beats`，增删改都是可撤销命令。[编辑器][timing-editor] [行提交][timing-item]

我们现在的 `StudioBootstrap.GuiFrame.cs` 仍然只取离播放头最近的12个Chart/BPM/Track，按距离排序；这有利于“附近”，不适合作为整曲的主时序表。输入时列表随着播放头改变也容易让用户误解顺序。现有原句输入支持未知字段保留，但阅读成本高。

建议单独实现TimingDocumentView：默认按时间+SourceId稳定排序；筛选BPM/拍号、Track、保留事件；显示time、BPM、拍数、速度、原行号；支持跳转上一/下一事件、按时间搜索、批量平移、复制、删除、来源字段提示。可选“跟随播放头”只滚动视图，不改变表的顺序。不把未知事件藏到一大段英文提示里，也不未经解析自动改成已知对象。

必须继续走 `History.Execute` 和字词级改动；第四保留参数及未知尾参数不丢。Chart头不能删除。输入校验失败要显示原因；不要照抄AdeTimingItem的空catch（该源码会吞掉解析异常）。

验收：Coldsea的99拍可以输入/显示，137931/140000/140690各自是新小节；未来BPM线提前显示；300个timing滚动不改变当前编辑行；修改BPM不改变track积分；Undo恢复原文精度。

### 3.2 时间轴需要的是概览与索引，不是另做进度条（P1）

AdeTimingSlider只是Unity Slider接到AudioTiming，不证明这个版本有波形或多轨时间轴。我们已经有 `StudioBootstrap.Timeline.cs`：音频幅值波形、六地轨+空中行、缩放/平移、点击与选区，能力上不能写成“缺失”。[实际slider][progress]

真正还可补：固定全曲概览+可拖可见窗口、书签列表、按小节编号定位、独立playhead与查看区域、显示/移动长条头尾、密集note与timing的空间索引、可复现性能profiling。Add group浏览是本轮自己的功能，不是从对方独立时序组照搬。

### 3.3 网格缓存和分类（P1）

Arcade的网格维护多个时间数组，按变更重建、用池隐藏不用的线。好处是时间列表不必在普通帧重新创建；但其更新仍遍历列表，AttachScroll/AttachTiming仍有分配与线性搜索，不能笼统宣称是最优O(log n)。[网格计算/渲染/吸附][grid]

我们的 `AuthoringGrid` 已解决时序段原点、单次毫秒取整、1/N、整数公共拍位、负track逆映射候选和独立小节。下一步按BPM/Track版本、Division、viewport拆缓存；用二分/区间索引筛选。逆向track下可视Z非单调，不能拿普通二分按Time裁掉本该可见的对象。

Arcade的自定义X/Y网格字符串能表达范围和额外点；我们的1/N对当前用途更简洁。将来可做“常用N收藏+非均匀刻度”，但继续用有理/decimal生成，**不抄对方`for(float i=start; i<=end; i+=interval)`**，它有误差积累并需要额外防止零步长。

## 4. 当前真正缺失或不完整的能力

| 优先级 | 能力 | Arcade证据 | InFalsus具体缺口与实施建议 |
|---|---|---|---|
| P0 | 系统化运行回归 | 命令/操作有清晰入口，但也不等于对方全部有自动测试 | 新增Unity PlayMode交互测试、Windows打包烟测、音频诊断/截图金样；先让每次更新有编译证据 |
| P1 | 全谱问题检查面板 | AdeFaultDetector按多个Fault分类，导出ChartFault.txt | 现有分散诊断升级可筛选、可点击定位、可选中问题对象；区分错误/警告/未知，不批量删除“重复” |
| P1 | 可配置快捷键 | AdeInputManager交互重绑定、modifier、保存override、文本焦点屏蔽 | 当前热键写死；做ActionRegistry与冲突提示，保留默认Ctrl/Alt/RMB规则，先不切输入框架 |
| P1 | 命名命令与历史查看 | ICommand.Name、onCommandExecuted与可调undo容量 | 现在大多是全谱快照；加入动作名称、涉及ID/字段、耗时、估算内存；再逐步做增量token事务 |
| P1 | 统一交互协调器 | AdeOperationManager一次一个operation、CancellationToken | partial类中gesture/point/floating/range/radial状态越来越多；用一个显式互斥/取消优先级层统筹，保留现有纯数据状态机 |
| P1 | 完整时序表/下一事件导航 | AdeTimingEditor+Item | 如上，不再依赖12条附近原句 |
| P1/P2 | 可见对象和group索引 | Grid及Timing有持久列表/池，ValueEditor按事件刷新 | 本版group成员/计数仍会扫描，部分网格批次每帧重建；加版本化缓存后量化GC和帧耗时 |
| P2 | 外部文件变化提示 | ProjectManager FileSystemWatcher→mainthread reload | 保存时已有字节冲突检查，但没有持续通知；只通知或用户明确Reload，绝不能覆写未保存草稿 |
| P2 | 完整皮肤/资源映射配置 | AdeSkinHost有Default/SkinDatas、typed resource槽和label来源 | 当前Resources写死+背景picker；建skin.json白名单、UV/透明度/回退、导入验证；不允许外部包执行脚本 |
| P2 | 录屏工作流 | AdeObsManager连接OBS WebSocket、主线程事件队列 | 目前靠人工录屏；可选连接现有OBS，联动预卷/播放/停止，不强制安装、不称离线视频编码 |
| P2 | UI布局/焦点通用层 | UGUI InputField.onEndEdit、四边RectTransforms | 当前IMGUI经历多轮布局补丁；先统一样式/焦点/遮挡矩形，再逐个面板迁移；保持相机有效视口和命中数学 |
| 待证据 | 无缝保调音频/原版判定 | 已读文件不足以证明Arcade有保调或可直接用于INF的规则 | 不能列成“直接移植即可”；音乐变速仍会变调，计分/物量是本项目候选研究 |

依据：[FaultDetector][faults]、[输入][input]、[命令][commands]、[Operation][operations]、[项目][project]、[皮肤槽][skin]、[OBS][obs]。

## 5. Fault检查器可以如何适配，而不是照抄规则

Arcade检查了短Hold、相同位置零时长Arc、Tap重叠、ArcTap重叠、Tap/Hold重叠、Hold重叠、非静止Arc跨Timing，并把原谱时间与音频时间写报告。[源码][faults]

Infalsus建议检查：非法lane/宽度/持续时间；Sky分母非正；未知ease与Flick方向；连续最小宽度负值；越过0..1；缺组/-1/组首重复头；首尾接触可合组但当前不一致；疑似重复Tap/Flick；同时间不可达Flick/互不相交Sky；计数跨未知BPM；未解明ICP事件；字体/资源引用丢失。

宽地键的重叠是区间关系，不能用`Track==Track`。Sky跨BPM在当前实现是合法的原始时间曲线，不应复制Arcade的CrossTimingFault直接判错误。重复Sky或同组重叠可能是源谱设计，默认警告、可审查，不自动删。

## 6. 不该导入的行为 / 暂不作为目标

- 对方每30秒自动保存和文件变化自动Reload，与用户“没有保存退出修改无效”冲突；不要恢复自动落盘。备份仅在显式保存时，已实现。
- 对方五难度不要变成INF五难度，保持MIN/EVO/ULT/FBD四档。
- AFF Arc拥有X/Y、ArcTap父子关系、isVoid、颜色/Designant、timinggroup等，不为借架构而发明INF新语法。
- 不用Arcade的BPM位移积分、世界宽度8.5、编辑/播放camera切换覆盖当前标定。
- 不把对方assets、DOTween、UniTask、InputSystem包、SFB或OBJImport整目录搬入现有项目。先抽取需求，能独立实现的保持低依赖。
- 用户截图中的Arc切分、长条密度、Arcade-Alpha增强菜单，在本次所读Arcade-plus源码中没有足够确认；列为另行核对，不宣传仓库已经包含。

## 7. 源码不是“抄了就无Bug”

读到的几处应反向吸取教训：AdeTimingItem空catch吞解析错误；Grid自定义刻度用float反复累加且未见有效零步长校验；部分吸附每次创建List再找Min；CommandManager.Undo发布`onCommandExecuted(preparing,true)`，在当前分支preparing通常为空而不是刚撤销的cmd。这些是静态观察到的风险/明显参数疑点，不宣称已在对方运行复现。项目保存采用FileMode.Create写目标，不可照抄以削弱我们的备份/冲突预检查/失败回滚。

本项目也有待改善：全谱快照大、频繁扫描、某些局部缓存边界没有性能数据、IMGUI代码在多个partial文件中互相调用、原生Windows与声音缺实际自动化；比较应有测量，不能只给对方或本项目贴“完善”标签。

## 8. 许可与工程组织

仓库大部分代码MIT，复用实际代码要保留其版权/许可。README列出Packages、Plugins、DOTween、SFB、OBJImport、图标、字体与纹理的不同许可；**MIT不等于整仓库每个文件都MIT**。本轮没有复制Arcade源文件/纹理/插件/字体，只提供引用和分析。[许可证][license] [许可例外][readme]

给Codex的目录建议：

```text
Workspace/
  InfalsusStudio/              # 用户实际Unity项目，唯一修改目标
  References/Arcade-plus/      # 单独clone固定commit，仅作参考
  ReferenceRuns/              # 两边独立程序的录屏/日志，不混进Assets
```

不要把Arcade嵌进Infalsus的Assets，不共用Library/Packages/ProjectSettings；对方Unity6000.0.58f2与用户6000.3.24f1分开。用户若提供Arcade-Alpha源码，应作为另一个显式命名的参考版本，不覆盖本报告证据。

实际执行顺序/验收条款详见 `ARCADE_PLUS_REVIEW_TASKS.md`。本轮只是实现用户明确要求的0.9功能并整理差距，未提前自动迁移上述P1/P2项目。

[timing-editor]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Editing/Editors/AdeTimingEditor.cs
[timing-item]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Editing/Editors/AdeTimingItem.cs
[timing-math]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Gameplay/Managers/ArcTimingManager.cs
[grid]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Editing/Cursor/AdeGridManager.cs
[transport-ui]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/ArcadeComposeManager.cs
[progress]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/UI/Info/AdeTimingSlider.cs
[commands]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Command/AdeCommandManager.cs
[operations]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Editing/AdeOperationManager.cs
[paste]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Editing/Operations/AdeCopyOrCutPasteOperation.cs
[properties]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Editing/Editors/AdeValueEditor.cs
[project]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Project/AdeProjectManager.cs
[input]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Input/AdeInputManager.cs
[faults]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Feature/AdeFaultDetector.cs
[obs]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/Feature/AdeObsManager.cs
[skin]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/Assets/Scripts/Compose/AdeSkinHost.cs
[readme]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/README.md
[license]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/LICENSE
[unity]: https://github.com/yojohanshinwataikei/Arcade-plus/blob/d157e35cbc3af869094775333a89cf0918aac166/ProjectSettings/ProjectVersion.txt
