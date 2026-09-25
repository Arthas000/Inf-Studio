# InFalsus Studio 0.3 — 交互状态、坐标与吸附设计契约

本版基于用户成功运行并认可的0.2。当前新增C#、Unity键鼠与原生左Ctrl查询尚未在交付环境编译/运行；本手册描述代码与设计，不声称实机通过。几何详细推导留在 `DESIGN_MANUAL_0.2_HISTORY.md`；交互以此文和 `EDITING_GUIDE_0.3.md` 为准。

## 1. 不改变的几何基线

X横向、Y向上、+Z远方。中央四轨x∈[-2,2]，y0；左右side内侧与±2共用接缝，run0.8252873/rise0.7716634。相机(0,3.086448,-3.219452)、俯角23.65212°、垂直FOV50°；空域高度1.156494。同一时刻地面/空域的Z相同。Flick固定参考高度0.40，宽度不参与高度缩放，顶点仍在水平空域层。

本次锁定比对CameraCalibration、StudioProfile、StageSpace、StageRenderer、FlickGeometry、CalibratedProfile、演示Scene、三份Shader和LightsOut样本，保持与0.2字节一致；所有既有meta保留。更新UI使用覆盖层，不改变相机或场地父节点。

## 2. 文件与职责

| 文件 | 责任 |
|---|---|
| Core/AuthoringGrid.cs | 唯一拍点集合、单次时间取整、X刻度、生成数字格式/十进制边界修改 |
| Core/RadialMenuState.cs | 纯右键状态机、长按/悬停门限、动作合法性 |
| Core/PointPlacement.cs | 四类音符的纯分步草稿，完成前不碰文档 |
| Core/AuthoringSelfTests.cs | 提供但未在此环境执行的真实C#回归 |
| LeftControlGate.cs | 只识别左Ctrl的编辑门控，不改变全局快捷键 |
| StudioBootstrap.Radial.cs | 右键捕获、径向排版/命中/释放路由、独立类型默认值 |
| StudioBootstrap.PointPlacement.cs | 落点/横向标尺、已锁端点预览、最终单次提交 |
| StudioBootstrap.Editing.cs | 门控原主体/把手，分步放置接入，键盘/工具栏 |
| StudioBootstrap.Timeline.cs | 同一网格的单调时间视图、左Ctrl拖动、version3会话 |
| Rendering/NoteRenderer.cs | 用统一AuthoringGrid取代硬编码整拍/b%4绘制 |
| Core/EditOperations.cs | 生成字段格式、十进制宽度、组移动逐头尾吸附 |
| Core/SpcDocument.cs | 原始源行替换、原文/ID/换行/撤销保护 |

## 3. 右键协议

`Idle -> PressedHidden -> Visible -> [Point子菜单] -> Release`。长按阈值0.28s，可在纯状态类改；子菜单0.5s按用户明确要求。Home与Point是持续模式，不是必须一次按压连开的两层。

Home在EnterPoint上右键松开：只设InPointMode，释放热控件，菜单关闭。下一次RMB重新打开Point。Point里直接Release Tap/Hold/Sky/Flick选择已有默认。维持RMB悬停Tap/Hold/Flick0.5s，子菜单以该命中按钮Rect.center为中心替换；没有重新按键，也不提前执行工具。子菜单按住→移动→右键松开执行。Sky不设子菜单。

所有可执行项由Allowed白名单控制。Blank Release为无动作；不影响模式、默认值、工具、网格、未完成草稿及播放状态。NoCreate停用工具但保留Point；Exit停用并回Home。工具持久，完成一枚后只是重置草稿。

View使用GUI.Box而非GUI.Button，避免默认左键可触发；仅 `rawType==MouseUp && button==1` 调用Release。GUIUtility.hotControl捕获RMB，输入在工具栏/场地逻辑前处理；持有期间禁止鼠标/滚轮/键盘穿透GUI和谱面，Esc取消。首层靠屏幕边缘可内移以容纳后续菜单，但子菜单仍严格以原按钮为中心。小于建议720px高的窗口需另做可用性测试。

## 4. 左Ctrl门控

Event.control不能区分左右Ctrl。明确监听KeyCode.LeftControl；Windows Editor/Player通过user32 GetAsyncKeyState(VK_LCONTROL=0xA2)取得物理左键状态，以适应新InputSystem-only项目而不引入额外包。失焦归零；其他平台只有明确左右键事件后备，不宣称已验证。

BuildHandles、BeginNoteDrag、TimelineMove均需LeftCtrlHeld。普通点击/Shift选择/框选仍可用。正在Move/Resize/TimelineMove时松开左Ctrl，立即提交最后有效预览一次并释放捕获；Esc/失焦取消。需要保持Ctrl快捷键与Shift选区语义，不把右Ctrl误作解锁。

## 5. 点立得放置的时序

草稿只保存Kind/Phase/start/end/X/lane/width。Tap: StartTime→Ready；Hold: StartTime→EndTime→Ready；Flick: StartTime→StartX→Ready；Sky: StartTime→StartX→EndTime→EndX→Ready。

选时间统一投影到实际六轨地面，取相应Z的时间候选，再调用唯一网格。空域时间同时绘制相同Z上的高亮横线；选择X时固定该时间对应的Sky横线，横向指针映射0..100并量化，鼠标Y不参与修改。滚轮只移动浏览播放头，不修改草稿已锁时间。

端时间必须严格晚于首时间，拒绝非法点击但不污染草稿。最终Ready才一次History.Execute创建SPC；之前既无源行也无撤销项。换工具/关网格/换细分/应用默认值/失焦取消草稿；空白右键释放无影响。

新Tap/Hold用独立1..4默认值，但0/5侧轨强制1；中央超界拒绝。新Sky为10/100宽、split100、左右Linear；新Flick暂为25/100宽且左右菜单不含宽度。所有现有音符保留自身split。

## 6. 时间必须从绝对格号求解

对正BPM段j：`T_exact = T_j + (n / N) * (60000 / BPM_j)`；`T_authored = round_away_from_zero(T_exact)`，n为整数格号。小节另用n乘beatsPerBar求候选（允许与细分不重合的非整数拍数）。相同毫秒合并，优先小节线。

AuthoringGrid.Nearest、Between、Adjacent共享Make与InSegment。Renderer、底部Timeline、时间高亮、放置/拖动用同一实例与Division。禁止逐步累加已四舍五入的period，也不能显示连续双精度线却把创建时间另外取整。

每个正BPM段从事件时间启动局部网格/小节。Meter缺省继承前一正值；每拍细分与小节拍数独立。BeatTimeline对特殊非正BPM依旧是旧版本保留/警告路径，不声称完全等价游戏语义。

当网格ON，Alt不能绕过。鼠标时间操作从手势起始文档计算，每个被移动头/尾独立吸入新位置的网格；因此跨取整间隔可能出现1ms差别，而不是产生230ms这类不在线上的位置。宽度操作不吸附原始时间；相邻固定端点不被连锁移动。网格OFF仍对主动改动时间一次取整，原文没有参与本次操作的字段不动。

为了避免极端输入造成无限循环，Between有输出/工作量上限；未对几百倍BPM、高密度零速和大量分段做性能保证。普通停/倒流通过ScrollTimeline可见段计算候选时间窗口；多值时间用现有上下文消歧，单调TimeLayout是可靠辅助。

## 7. X网格、可视化与原文精度

归一化0..1展示0..100，完全独立于每事件split。StepPercent默认5，0.1..100可设。AirAuthoringGrid用decimal刻度计算和中点规则；100始终是显式端点。节拍线关闭时X网格仍独立吸附，手动源文本可绕过。

绘制在固定Sky Y、当前操作时间Z上的一维尺，只有短竖刻度不是高度网格；活动X绿色、高亮时间黄橙色。地面线跨全部六轨。投影线做屏幕裁剪，不因一端出屏就丢掉整条线。预览不覆盖相机投影。

AirEdge在原始split单位使用decimal：保留对边，改指定边，再反算center/width；AuthoredNumber只对新生成字段以最多10位小数、去尾零形式输出。禁止全谱ToString/重新量化。手输SetToken与ReplaceSourceLine路径不经过此格式器；相同数值的Set为no-op以保留导入词法。

原始语句TextArea返回值必须接收；用户点Apply SOURCE text时只改一行，保留ID/EOL/BOM/其他语句。允许手工小数时间与不在网格的X。不能以“显示更干净”为由删未知字段、统一split或自动改时间。

## 8. 测试与后续

参考回归、静态检查和基线哈希已实际运行；C#与Unity未执行，见TEST_REPORT。不使用旧126/355测试数字假称新增右键交互已运行。后续先跑UnityCoreChecks和CameraLandmarks，再做真实左/右键、松Ctrl、空白释放、四步取消、Undo/Save/Load与窗口缩放检查。

### 官方API参考（实现所依赖的接口，不是运行验证）

- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Event-keyCode.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility-hotControl.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Font.CreateDynamicFontFromOSFont.html

径向菜单只借鉴用户指定的操作流程，不含Arcade代码/图片/字体。中文字体运行时使用系统字体，不附带字体文件。其他软件/原素材许可和本体未知SPC语义不在此版本被推定解决。
