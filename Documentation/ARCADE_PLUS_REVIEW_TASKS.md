# 给Codex的后续对照优化工单

## 入口任务（可直接交给Codex）

继续当前InFalsus Studio 0.9。只修改用户的InfalsusStudio工程，References/Arcade-plus只读。先读取Documentation/ARCADE_PLUS_COMPARISON.md、DESIGN_MANUAL、CODEX_HANDOFF以及AGENTS。Arcade参考固定d157e35cbc3af869094775333a89cf0918aac166；不是Arcade-Alpha1.12.1。不要凭同名timing直接搬BPM位移，不把SkyGroup变为timinggroup。

先建立可复现基线，再逐个工单实施。每个任务给出改动路径、现有行为、参考方法、INF特有约束、运行测试结果与未通过项。保留所有手动保存与字词无损回归。不能用Python统计“很多测试通过”代替Unity真实编译。

## P0-00 建基线，别先重构

记录Unity/URP/Windows/GPU/音频realvoices/屏幕DPI。运行CoreSelfTests、CameraLandmarks、0.9Runtime诊断。用实际PlayMode截图空场、1826、3610、5500、EnigmaFBD24070、LightsOut74077尾后、Coldsea137931/140000/140690。保存音频25%+key1x、group7前后、背景2:1 cover、原生窗口四种模式录像。

比较前后源文件SHA：没有Save时原metadata/四工作谱不变；Save后只目标token变；失败保留dirty、原先字节可恢复。必须验证Ctrl复选、Alt拖、RMB取消、预复制滚动、Cut取消、后台窗口失焦。

## P1-01 时序表

参考AdeTimingEditor/Item；目标新增TimingTableModel（纯Core）+独立视图，不挪相机。可编辑time/BPM/meter/track speed，未知尾参数保留；排序time+stableID，筛选/搜索/上一下一/跳转。全部更改History一次事务，Enter/blur生效、Save唯一落盘。

验收：meter99、meter缺省继承、两个同时间timing稳定顺序；500条timing滚动；播放时列表不跳行；删除Chart头拒绝；未知ICP显示time/type/raw来源，不猜解码。任何正BPM更新后未来网格立刻正确且track位移不变。

## P1-02 交互协调器

参考AdeOperationManager与Prepare/Commit/Cancel思想，不强制使用UniTask。统一枚举Idle/PointPlacement/Range/FloatingCopy/FloatingCut/HandleDrag/TextInput/Modal；禁止互相吞事件。RMB在未完成操作只取消且不弹轮盘，连续创建后仍在点立得层。选区restore与History rollback一起定义。

验收：每种状态下Esc/RMB/Alt释放/失焦/换难度/Play/Save，给状态转移表；复制/剪贴取消文件和undo数零变化。混类型选区仍按用户要求不弹属性表。

## P1-03 索引与缓存

测量再做。目标：按Time排序的event索引、group索引、timing/track版本、可见区间缓存、geometry dirty范围。普通播放帧不重复克隆文档，不循环new大量List/GUIStyle。避免负track非单调造成错误裁剪。

使用实际输入和10k/100k合成谱，报告CPU ms、GC bytes/frame、内存、首次加载与修改单note耗时。对照同帧顶点/投影误差、拾取结果和SPC字节；不能为了性能降成固定20段Sky或丢未知记录。

## P1-04 命名历史与验证器

命令显示动作名/对象数/时间；优先保留现有快照API，再替换局部token delta。新增全谱Problems窗口，错误/警告/保留类型分层；点击定位并选对象，报告可导出。宽键使用覆盖区间，Sky交叉检查连续极值；不把所有重复/跨BPM段当非法。

验收：任一批量对象非法时全部不改；undo/redo完整原文、group、尾参数、换行、BOM；Coldsea的合法bpm不再标成未知；误导入缺失payload不能静默“修复”。

## P1-05 快捷键配置

参考InputManager的action注册/焦点屏蔽/override持久化；不一定替换InputSystem。添加冲突提示和恢复默认，按用户默认Ctrl复选Alt把手RMB菜单，文本焦点全局快捷键不穿透（Save的明确接收规则例外）。配置只显式保存，不悄悄改变同事电脑项目设置。

## P2清单（完成P1后再选）

皮肤manifest/资源槽与回退；外部文件变化通知但不自动重载dirty；全曲概览/小节定位/书签列表；可选OBS录制流程；逐面板UI重排与局部保留式UI迁移。功能开关默认不阻碍制谱，不添加强依赖下载步骤。

## 禁止回归

四难度、中央宽键、Sky双边界/固定Y、Flick16左4右/高度不依width、group首头/首音、全局Save-only、键音pitch1、原始时间计数、非整数源时间导航、相机/轨道校准、unknown字节保留。BPM/track/参考BPM三者不得合并。

所有研究/移植保留源commit和许可清单，不复制字体文件，不将用户未授权游戏资源推到公共仓库。当前音乐保调、真实判定tick/score、ICP二进制导出不是靠抄AFF代码可补齐的任务。
