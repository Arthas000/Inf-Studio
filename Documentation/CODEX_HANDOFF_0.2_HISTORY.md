# Codex接手：Unity 0.2，不重做已经对齐的场地

## 已验证的范围

用户已经在Unity 6000.3.24f1运行0.1.1，认可相机、两侧轨、空域和主要音符。随后指出Flick的屏幕高度不该与音符宽度成比例。0.2落实该修正并增加交互，但**交付环境没有Unity/C#编译器，新版还未实机验收**。不能把旧运行截图、355项Python数学检查或词法检查说成新版C#编译通过。

先阅读 README、EDITING_GUIDE_0.2、DESIGN_MANUAL、TEST_REPORT、AGENTS。

## 文件职责

- Runtime/StudioBootstrap.cs：启动、主UI、原始字段Inspector、文件/音频入口。
- Runtime/StudioBootstrap.Editing.cs：选区、工具、鼠标捕获、把手/放置、编辑键盘命令；与主类为partial，不要额外挂第二个组件。
- Runtime/StudioBootstrap.Timeline.cs：单调时间视图、波形、会话侧车文件、循环/书签、工作副本保存。
- Core/SpcDocument.cs：稳定行ID、原文/换行/BOM、原子替换、事务撤销。
- Core/EditOperations.cs：不依赖Unity的编辑数学、群组移动、宽度/端点、镜像、剪贴板。
- Core/ChartMath.cs：原有双边界/时间轴，新增连续最小宽度求解。
- Core/WaveReader.cs：无需Networking的本地WAV解码。
- Core/EditingSelfTests.cs：新的实际C#回归入口，由CoreSelfTests调用。
- Rendering/FlickGeometry.cs：固定参考跨度的透视高度；不再读旧flickScreenHeightRatio。
- Rendering/NoteRenderer.cs：多选高亮、三角形拾取和框选；场地、相机不由该文件重建。

## 先完成真实验证

沿用用户工程，不升级Unity、不改Packages、不切换输入系统。导入后修第一条真正的C#错误；跑Tools/InFalsus Studio/Run CSharp Core Checks。进入Play跑Check Running Camera Landmarks，其中有多宽度/多距离Flick检查。批处理入口仍为InFalsusStudio.Editor.StudioSetup.VerifyBatch；Tests/CoreChecks.csproj可在.NET8运行。

逐项实际操作：Tap/Hold/Sky/Flick放置；点选/框选/混合地空选区；拖动/取消/失焦；一个拖动一步undo；边界拖动保持另一侧；变速/停流/退流的Time layout与底部时间拖动；剪切只删被复制音符；镜像交换Sky左右ease和Flick方向；SaveAs→再保存→.bak→重载；WAV和Editor OGG/MP3导入；波形；循环/书签/sidecar。

Fixtures/Flick_height_w2/w4/w8/w12/w24分别在3610 ms观察同一深度，应该高度一致、宽度变化。Fixtures/Editing_Sandbox是合成样本，含未知custom_test行，保存后必须保留；不是游戏兼容样本。

## 不可被重构破坏

X横向、Y高度、+Z远方；地面Y0；侧轨内缘与x±2接缝连续。相机(0,3.086448,-3.219452)、俯角23.65212°、垂直FOV50°；AirY1.156494。不要为UI面板改相机，当前UI为覆盖层。

Flick16左黄、4右绿，高端帽在输入方向侧；网格固定Y。高度=同深度1单位参考横段投影宽度×0.40，而不是eventWidth×0.20，也不是固定像素高度。极窄端帽仅限制横向偏斜，不能偷偷把高度又缩小。

Sky每端自己的split，左右边界独立缓动，u由原始时间算，开头1%→100%→1%保留。判定时间与装饰Z不同。tap(t,width,lane) != hold(t,lane,width,duration)。滚动积分只用track，读完全文后构建。

SourceId是会话内稳定ID，删除不重新编号；不是当前行号。要显示行号用SourceIndex。ID不写入SPC。Preview从transactionStart克隆，每帧不基于上一帧重复累加。所有可能拒绝的多对象编辑通过History事务，原子拒绝。未知参数、行序、换行不丢失。

## 仍需继续完成

1. 真实Unity键鼠回归与错误处理，特别是GUILayout控制捕获/失焦/窗口缩放/平台输入差异。不以源码检查替代它。
2. 长谱大量选中时性能：当前按可见重建网格，Preview克隆文档、频繁扫描，尚未做索引与增量缓存。先profile，不牺牲几何/无损保存。
3. 更完整撤销UI、便捷时间范围选取、2D尾部调整、可关闭的端点链联动。当前链只是显式续段，不自动传播连接点。任意曲线切段不等于复制ease。
4. 无缝循环、保调变速、实际音频延迟校准。当前循环会重新调度、变速会变调。
5. 材质贴图通过不改几何的渲染接口接入；用户还未提供，不虚构原纹理。防止更新覆盖Assets/InFalsusStudioUser/Audio。
6. lane/beam/特殊BPM/group本体语义仍需独立验证；不能从编辑器字段推断完整判定。

交付下一版要记录真实Unity版本、编译结果、C#测试输出、实际键鼠操作/保存回读结果、未通过项，不能沿用本次静态测试数字作为后续版本证据。
