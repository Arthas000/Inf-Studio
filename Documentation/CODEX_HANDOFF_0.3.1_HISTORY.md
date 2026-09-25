# Codex接手：InFalsus Studio 0.3.1

先读README、EDITING_GUIDE_0.3.1、DESIGN_MANUAL、TEST_REPORT。本版基于用户实际运行的0.3，但新补丁未在交付环境编译C#/运行Unity。请在用户Unity6000.3.24f1工程实测，不换引擎，不重建相机，不升级输入系统。

## 第一轮必须验证

先编译最早一条错误 → Tools/InFalsus Studio/Run CSharp Core Checks → Play时Check Running Camera Landmarks。
接着重点测试Inspector打开播放、附近事件集合每帧变化、选择Sky/Tap/Timing、Raw Apply把类型改变、增删Undo/Redo、切面板、文件/音频对话框。确认没有新增GUILayout control-count错误，保留原始日志。不能凭CoreChecks通过声称真实GUILayout正确。

在Fixtures/Grid_130_4_Verification.spc从0各滚/4 /6 /8档，核对Fixtures/Grid_031_Expected.json。若物理鼠标单位不同，Calibration可选1/3并显示原始delta；不要改回半拍timesdelta。
在1123非全局网格的Timing样本验证局部phase重置；track不会重置BPM。
按左Ctrl试L~/R~，只允许改arg7或8，原time/width/split/group/另一边界不变。严格一次手势一条undo。
测试宽2点击4->start3，宽3点击3/4->start2，宽4任何中央->start1，side0/5仍width1。

## 禁止回归

- 相机、场地、Flick固定高度、Shaders和原始谱面基线保持原样；新的像素网格ribbon不是修改地面。
- RMB初始显示零延迟，Tap/Hold/Flick子菜单0.5s；释放在按钮才执行，空白释放不改状态，子菜单中心是原类型按钮。
- 普通左键不能拖主体；只有LeftCtrl解锁，RightCtrl不解锁。
- 网格显示与吸附同AuthoringGrid。生成绝对索引位置后最后取整，不反复加整数格距。
- 音乐连续播放不允许每帧Seek到格子；仅暂停编辑锁网格。
- stage、hover、播放头用同时间/Z和BeatGuideGeometry；节拍线六轨同步，侧轨斜线是正确投影。
- 已有语句原文保护。新建宽键归位规则不应用到导入、未编辑字段或整组移动。
- `BeginGuiFrame`只在Layout更新快照，结构性按钮只入QueueGui。不要用实时CurrentMs/selection重新生成同轮GUILayout控件列表。

## 后续优先事项

真实键鼠回归与GUI日志、Input.delta设备刻度确认、密集网格与透明层性能；再处理材质。复杂停/倒流保留候选，极密/特殊非正BPM仍未完整验证；不能假造游戏语义。

交付报告区分：真实Unity版本、C#编译、CoreChecks、视觉/鼠标操作、保存回读、未完成项。Python与静态测试数量不是Unity执行证据。
