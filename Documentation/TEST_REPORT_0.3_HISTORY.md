# InFalsus Studio 0.3 — 测试与交付记录

## 结论边界

**没有在交付环境执行Unity Editor、C#编译、Shader编译、Play Mode键鼠测试或Windows构建。** 环境没有可用Unity、dotnet、csc或mcs；不把静态结构检查称为“编译通过”。用户之前实际运行的是0.2，这不是0.3运行证明。本包没有0.3运行截图；原有PNG是历史数学投影叠图。

本次实际执行：

| 检查 | 结果 | 证明什么／不证明什么 |
|---|---:|---|
| Tools/verify_reference.py | 363项通过 | 独立Python几何/数学与资产元数据；不运行C# |
| Tools/verify_editing_contracts.py，基线0.2 | 100项通过 | 旧编辑数学/静态契约及基线对照；不执行新手势 |
| Tools/verify_authoring_contracts.py，基线0.2 | 152项通过 | 分数/十进制独立期望、接线静态检查、GUID/不变资源比对 |
| Tools/check_source_layout.py | 28份源码通过 | 有限括号、预处理、常见声明检查；不是C#或Shader编译 |
| 0.2 + 更新Assets → 0.3 | 字节一致 | 实际临时目录覆盖合并后逐文件比较；不是Unity导入 |

这些数量包括资源结构与元数据检查，不等于同等数量的端到端交互测试；不重复相加制造覆盖率。

## 数学与结构覆盖

130BPM四细分的直接求值、0/115/231ms序列、一百万格115384615ms；不同BPM/细分的长时间有理数期望；渲染整数候选自吸附；小节4拍/3拍与细分独立；局部BPM起点；0..100网格5%及不整除100的右端刻度；保留对边/split的十进制变宽；组移动各头尾重新吸实际网格；生成坐标干净格式与原文精度两条路径。

静态检查了右键MouseUp/button1提交、无左键GUI.Button、悬停0.5、子菜单中心接线、左Ctrl门控主体/把手/Timeline、源语句TextArea实际Apply、同一AuthoringGrid供绘制和吸附，以及新测试接入CoreSelfTests。**这些结构检查不能保证热控件/焦点时序在Unity中一定正确。**

## 补丁与基线

基线完整ZIP：`InFalsusStudio_Unity_Prototype_v0.2.zip`。

基线Assets含meta共56份文件；目标共70份。更新包有30份Assets文件（含对应meta）；删除数0。按原始0.2复制到实际临时目录，再覆盖更新文件，文件集合和字节都与完整0.3相同。

锁定不变：CameraCalibration、StudioProfile、StageSpace、StageRenderer、FlickGeometry、CalibratedProfile、Scene、LightsOut样本及3个Shader。新GUID互不重复，既有meta未变。不覆盖Packages/ProjectSettings或用户素材/音乐目录。

机器可读记录：Reference/python_verification.json、editing_contracts_check.json、authoring_03_checks.json、source_layout_check.json、patch_03_verification.json。根目录SHA256SUMS.txt列出文件摘要。

## 已提供、但这里没有执行的真正C#检查

Core/AuthoringSelfTests.cs已接入CoreSelfTests.Run；Tests/CoreChecks.csproj使用通配包含所有纯Core文件。其断言覆盖：实际AuthoringGrid.Next/Nearest/Between、4096次前进和后退不漂移、正BPM段/拍数继承、亚毫秒格合并；decimal边界与原文不动；实际History多预览一撤销；PointPlacement四步/侧宽/取消；RadialMenuState的长按、直接松开、0.5悬停、空白无效、Flick只有左右、Sky无子菜单、NoCreate与Exit。

用户本机执行：`Tools → InFalsus Studio → Run CSharp Core Checks`。在有.NET8 SDK环境可执行 `dotnet run --project Tests/CoreChecks.csproj`。这里未执行这两个入口，故不编造C#通过项数。

进入Unity Play再执行 `Check Running Camera Landmarks`。这个菜单验证实际相机/Flick锚点，但不能代替下面的人机交互验收。

## 需要本机验证的重点

- Unity6000.3.24f1实际导入与C#编译；首条Console错误、GUI的Layout/Repaint捕获、系统字体回退。
- RMB长按开Home，松在点立得进入模式，下一次右键开Point；保持右键悬停Tap/Hold/Flick0.5s，子菜单中心不跳；右键释放执行，空白/左键释放无操作。
- 左Ctrl vs右Ctrl、先松Ctrl/先松左鼠标、Esc与失焦、编辑器焦点在Game外时状态重置，键盘快捷键与输入框。
- Sky四步/滚轮锁端点/取消不写行；Hold两次点击和独立侧轨宽；重复放置不丢工具；一次提交一次Undo。
- 六轨和空域时间高亮同一落点；网格ON时Alt不绕过；改拍数/细分后可见线一致；多选跨不同格长/变速避免230ms偏格。
- Inspector整句与单参数Apply保留任意小数；仅改宽度不改time/split；真实Save/Reload和备份恢复。
- 小窗口、DPI、菜单靠边、Inspector遮挡、长曲密集网格、停/倒流多时间候选、音频操作仍需性能和交互测试。

## 已知限制

径向菜单外观为程序绘制，不是Arcade美术复刻；推荐Game至少1280×720、16:9。Windows物理左Ctrl读取和跨平台后备尚未实测。极端亚毫秒网格/大量停止段有安全数量上限，不保证全量可视化；BPM段重置网格是编辑策略，特殊本体事件仍保留而非声称全部理解。原始文字可主动写出非法音符，保存/显示诊断不等于本体接受。没有新增游戏素材、判定计分或生产级稳定性承诺。
