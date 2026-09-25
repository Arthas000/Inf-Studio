# Codex 接手 — InFalsus Studio 0.7

继续用户Unity6000.3.24f1/URP工程，不从头重建。先读EDITING_GUIDE_0.7、DESIGN_MANUAL、TEST_REPORT与本文件。0.6经用户实际运行，新版0.7未在交付环境C#/Unity编译/运行；不能把独立121检查和67词法检查当成程序集通过。

## 不可退回

1. Ctrl复选、Alt主体/把手，普通点选不移动；播放隐藏编辑UI/不接受误编辑。
2. 同型批量live字段只改RAM，无普通Apply。Global Save/Ctrl+S才写所有difficulty+metadata；切难度/退出/计时器不写盘；Save As仅导出。不要把Apply方法名误读为文件保存。
3. Copy/Cut预览可滚轮/拖进度条；Cut仅隐藏源对象、不提前Delete。RMB按下/ESC取消回原选择和位置，不能同时唤出菜单；drop一次undo。
4. 段选按Hold/Sky的EndMs，其余TimeMs，闭区间；之后Ctrl可继续加减。混合选择不弹表单。
5. 照旧0..100空域，四轨宽/侧轨，独立ease、原split、时间原文、unknown不损坏。镜像交换左右缓动与Flick方向。
6. copied times保留精确offset；Align是单独显式命令，网格ON才有，分别对齐long head/tail，原子拒绝零长。
7. 任意整数N1..1024，按绝对格号计算；1/12有quarter/half参考色；grid/bar/time高亮同源；BPM≠track。referenceBPM仅view。
8. music优先级0不进FX池，floor/Sky持续床独立；FX按numRealVoices预算并保留音乐。不要改成无限PlayOneShot或用isPlaying回收尚未起播声音。
9. SkyConnections同时驱动头框/头音；相接只第一段，group不替代几何连接。原声音文件/Shader/字体授权不扩展。
10. VFX只是示意预演，不改score/物量。原评分候选、AutoplayCursor修复、相机、左右轨、Flick高度保持。

## 文件定位

SelectionAuthoring/FloatingNotes纯Core；SkyConnections+AudioVoiceBudget纯Core；Release07SelfTests为新回归。Runtime EditModifierGate，StudioBootstrap.SelectionWorkflow、NotePanel、Chrome及Panels/GuiFrame是交互接线。HitFeedbackRenderer是受控程序化效果。StudioTransport只增加priority和diagnostic；StudioKeySounds是受限排程。

## 实机必须执行

- 完整导入/编译，修首条真实C#／Shader错误，不猜包缺失、不整体重装输入系统。
- Tools/InFalsus Studio/Run CSharp Core Checks（保留所有历史测试）。可选dotnet run --project Tests/CoreChecks.csproj仅纯Core。
- Play后Check Running Camera Landmarks、Check v0.7 Runtime and Difficulty Assets、Check v0.6 Key Sound Assets和v0.5 clipping历史入口。
- Ctrl复选同型/混型，Alt拖不同面，按键释放/窗口失焦；0.28s数值输入、快点另一个字段、按Save、Undo、原语句精度；绝不能把一组pending字段写到下一选区。
- 批量三对象第一个有效第二无效，应整体不改；查看source bytes和undo数。
- Copy/Cut→滚轮→进度条→回场地预览→RMB取消，原selection/source/undo不变；drop一步undo；CtrlV再次复制；copy再Align修1ms。
- range头已定后滚轮/进度条，RMB取消；range结束后Ctrl减一个。fixture明确长条END规则。
- 一首持续音乐配07_Audio_Dense_Disconnected；记录music isVirtual、priority、maxrealvoices、FX capacitydrop。若仍断，抓Audio Profiler，不说静态修补已证明听音正常。
- 头相接/1ms缝隙/同时间不同X/重复源；头框与head sound同源。
- 1280x720/1920x1080/窗口缩放，顶栏与HUD无碰撞，左侧长栏/详细波形不挤压，selected侧签前层点击一致。
- Playing只保留游戏HUD/COMBO/progress/Play，暂停恢复原面板。双击退出一秒失效，不暗中保存。
- 两难度编辑和metadata：mtime/bytes不动直到Save，退出Discard重开旧内容。现有ManualSongSession失败冲突回滚测试继续通过。

## 后续不能隐瞒的边界

这版没有真实原版判定/分数内核、保调拉伸、无缝hold循环或独立exe MP3/OGG后端。大量选区ghost重建与SkyConnections O(n²)仍需profile；数学正确不能替代响应速度。中文主要UI/Help覆盖，高级部分英文仍在。VFX是原创程序化近似，不是用户图中原粒子系统的完整复制。

交付下一个版本要附实际Unity版本/日志/测试截图/音频诊断/未通过项。不要复用历史测试数字当新版实机证据。
