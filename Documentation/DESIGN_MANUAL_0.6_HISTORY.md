# InFalsus Studio 0.6 — 设计与实现契约

以本文件和EDITING_GUIDE_0.6为准，0.5.1历史手册保留。此次新增实现未在交付环境执行Unity/C#，不得把Python数学验证写成编译或播放通过。

## 1. 不动的部分

相机、四轨地面/斜侧轨、StageSpace、StageRenderer、Profile、FlickGeometry、左右Ctrl门控、右键菜单状态机、原始LightsOut谱和三份Shader保持0.5.1字节。Renderer只新增小节线，不改变时间→Z、空域高度和Flick宽度无关高度。

ManualSongSession、SongMetadataEdit、StudioBootstrap.ManualSave、ProvisionalComboTimeline、TickCountResearch保持字节。Enter/Apply仍只改RAM；Ctrl+S/Save是唯一工程落盘入口。没有定时恢复/退出保存，也不替换原source JSON/binary。

## 2. 模块边界

| 文件 | 新责任 |
|---|---|
| Core/AutoplayCursor | 当前空域退出后按最新空中动作排序，不能恢复更旧的Flick尾位置 |
| Core/AuthoringGrid.BarsBetween | 独立小节枚举器，与snap网格使用同一Make/段边界/绝对格号取整 |
| Core/PreviewScore | 明确标注AUTO EXACT估计，纯当前量/总量函数 |
| Core/NoteProperties | 单note字段快照、百分比映射、原文版本检查、Apply-only token修改 |
| Core/HitSoundPlan | 原谱事件→攻击列表与两个独立持续区间并集 |
| Core/Release06SelfTests | 真C#回归，挂到CoreSelfTests末尾 |
| StudioBootstrap.Panels / NotePanel | 左侧收起工作区、类型专用表单、Timing独立面板 |
| StudioBootstrap.SongHud | 中央combo、分数模型接线、SCORE锚点修正 |
| StudioKeySounds | Resources样本、DSP前瞻排程、voice池、epoch/loop失效 |
| StudioTransport | 暴露DspTimeAtChart/ChartTimeAtDsp、Revision和音乐音量；原音乐调度不重写 |
| Editor/StudioSetup | 新音频资产校验菜单 |

## 3. 指针bug的具体根因

旧Evaluate以半开区间判断活动Sky。在tail时刻活动Sky消失后，prev仍是“最近完成的Flick簇”，即使它发生在若干Sky之前。else分支于是重用prev.Route末端。LightsOut73846～77077每段都在中央，旧Flick位于左侧，使bug表现为每段尾部闪回左端。

新LatestSkyExit按EndMs挑选最近完成Sky，与prev.End比较动作先后。SkyExitRange在精确尾端求同时有效的可达范围，不用固定时间epsilon推端点。刚退出Sky的默认位置是该尾区间中心；若尾时刻仍处于Flick后的ReturnMs阶段，则保持那个时刻实际约束后的返回位置。下一Flick/Sky的approach沿用已有逻辑。

不是只对中心=.5特判。非对称Sky保留其尾中心，新的Flick也能正常抢占更早Sky。持续活动Sky、左右同时间Flick扫动、不可达冲突报告和可视消失时间规则不变。自动指针从time确定，不依赖上一帧。

## 4. 分数与combo证据层

给定估计已判定数C和估计总量N，score=floor(100000000*C/N)+C，C限制[0,N]；N=0输出0。用decimal直接求商，不重复累加每tick取整分数。MAX=100000000+N。

这个公式是用户要求的编辑器预览模型，不是从原游戏已经识别的计分内核。候选tick总数和时间、起点BPM假设均没有增加新的认证。原ScoreEvidence记录可以优先展示RECORDED。GUI明确显示AUTO EXACT/EST，而不是按音乐长度百分比伪造。

中央大COMBO依赖ProvisionalComboTimeline.At(time).Total，显示的不是记录数。默认零隐藏。它是固定相机视口相对HUD，不参与鼠标命中。SCORE字母参考2048截图x≈35,y≈31；1920设计坐标HR(33,20,78,20)，数字框HR(138,40,328,54)。所有主数字保持9位预算，辅助信息放下面。

## 5. 工作区与表单

LayoutWorkspace在固定camera.pixelRect中布置覆盖式UI，不修改相机aspect或投影。SCORE下方左侧竖栏宽88设计单位，展开工具宽448；note面板在竖栏右侧，宽352，按类型内容给高度上限并提供滚动。Tools和note空间互斥；Timing位于右侧且默认关闭。

所有会改变GUILayout树的开关、对象选择和Apply通过QueueGui到下一Layout。CaptureWorkspaceFrame固定guiNoteForm类型/对象和高级区开关。字段输入可以改Values，但不能在同轮Repaint改控件数量。右Timing继续用guiTimingEvents快照，不能使用随playhead每帧变化的live列表。

NoteProperties保存SourceId、OriginalSource、各字段初值。Apply要求源行仍等于快照，不允许旧表单覆盖新拖动或换难度。只写有变化的key；center/width显示百分比，写回各端原有split。开始/结束字段都是绝对时间，修改其一保持另一端不变。新duration可变化，没动的原time token不重新格式化。

友好表单拒绝非法duration/width/越界；在History.Execute的克隆文档上应用，失败不提交。编辑明确的Sky缓动调用原EditOperations.SetEase，保留单边wall与端点方向规则。Advanced原文是独立手输通道，不受百分比UI自动规整。

## 6. 小节线的时间语义

BarsBetween对每一正BPM段从p.Time+n*p.Meter*60000/p.Bpm生成。每个位置最后取整一次，使用同一个InSegment，上一段不跨过新timing。相同毫秒的新段优先。未来timing已经在列表中，Renderer遍历可见scroll时间区间时直接枚举，不以CurrentBpm重画整场。

当snapGrid关且persistentBars开，生成淡灰1.15px的六轨ScreenStrip。snapGrid开则仍显示原小节/整拍/细分颜色，不叠灰线。画灰线不强制snap；关细分网格后自由编辑规则保留。track只改变投影纵深，不改变bar的音乐时间。

## 7. 音效与排程

读取6个用户提供OGG，原字节与对应关系见Reference/key_sound_manifest_06.json。全为48000Hz stereo，最长SkyHit约3.564s，两个Hold样本约6s。文件放Resources/InFalsusStudio/KeySounds；AudioImporter设置预载和DecompressOnLoad，禁止运行时依赖外部网络或临时ffmpeg。

HitSoundPlan攻击基于原事件time，非combo候选ticks，也非VisualContactTime。Tap按lane区分floor/side，Flick每个事件一声，Sky默认每段头一声。没有已识别的独立SkyTap，不虚构语句。可选RegionStart只改变Sky攻击策略，不合并谱面，不影响计数。

持续床：将地面Hold时间区间求并集；SkyArea单独求并集。两组同时激活对应两个AudioSource，而不是切换同一个clip。相邻同种床不重启，但不消除每段独立攻击（默认策略）。两份持续床均循环整份样本；没有已知原版loop marker，不声明无缝。

StudioTransport的baseMs/anchorDsp/Rate是音乐与音效共同锚点。DspTimeAtChart=anchor+(chart-base)/(1000*Rate)，ChartTimeAtDsp为其逆。Revision在明确transport变化时增加。排程前瞻200ms，音乐原预留80ms不变。voice单独记录BusyUntil，不能用尚未起播时isPlaying=false误判为空闲。

Pause/seek/rate/loop/difficulty/重建plan停止所有已排定source，重建索引；继续播放到hold内部从样本相位恢复。重建plan但没有新transport epoch时从当前time开始，不能从旧PlaybackStart重放已经发生的攻击。未及时排程超过120ms的攻击丢弃并统计，不追打一串过去音。

循环B是排程上限，攻击start必须<B，持续床和攻击尾音最迟在B停；实际Seek回A生成新epoch。没有宣称循环无缝。最多96voice，容量不足计数可见，避免无界创建GameObject。音量音乐/总效果/攻击/持续分开；暂停/disable销毁时停止source，不写任何工程文件。

## 8. 官方参考与未知

- Unity AudioSource.PlayScheduled：https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource.PlayScheduled.html
- SetScheduledEndTime：https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource.SetScheduledEndTime.html
- AudioSource.loop：https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioSource-loop.html
- GUI布局顺序：https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility.ExitGUI.html
- 上游语法：https://github.com/yuhao7370/In_Falsus_Editor/blob/d61cd73395ebe6959358f2f5669ba9cf259eadeb/src/chart/codec/types.rs

这些资料不是本版执行验证。原版真实tick与score舍入、sky攻击的group规则、输入窗口、音频设备延迟仍待实测。手册只说明这版已写入代码的策略，不把音效名字当成独立音符类型存在的证明。
