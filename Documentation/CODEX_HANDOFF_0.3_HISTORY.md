# Codex接手：InFalsus Studio 0.3 点立得

## 当前状态与首要任务

用户在Unity6000.3.24f1运行0.2并认可几何；本版据用户逐项确认增加0.3右键点立得、左Ctrl门控、统一网格、精度修复。交付环境无Unity或C#编译器，不能声称代码已编译。请先在用户现有工程完成导入编译和真实交互检查，不重做场地。

操作定义见EDITING_GUIDE_0.3；核心设计见DESIGN_MANUAL；AGENTS有不得回退规则。历史目录只供推导参考。

## 首轮按顺序做

1. 记录Unity版本和URP版本；保留首条C#/Shader错误。只做实际必要修复，不替换Packages/Input System/ProjectSettings。
2. Tools / InFalsus Studio / Run CSharp Core Checks。CoreSelfTests已调用AuthoringSelfTests，它测试真正的C#AuthoringGrid、decimal编辑、菜单纯状态、放置草稿与撤销。
3. 进入Play跑Check Running Camera Landmarks，复验Empty/1826/3610/5500/22400和Flick宽度；这些资源哈希对比0.2未变。
4. 真实RMB录屏：Home进入Point后下一次按压，直接Tap释放，Tap悬停0.5子菜单，Hold独立宽度，Flick左右，Sky无子菜单，空白释放/左键释放都不执行，不创建vs退出，沿用工具连续放。
5. 真实左Ctrl：普通鼠标不移动主体/把手，右Ctrl不解锁，左Ctrl编辑并显示网格，松Ctrl结束最后有效预览一次，Esc/失焦取消。底部Timeline同样门控；Ctrl快捷键和输入框不能穿透。
6. 四步Sky过程中滚轮、取消、换工具、grid开关；提交只有一个Undo。Hold保留lane，边轨永远单宽。
7. 实测网格ON/Alt、拍数4→3、细分4→8、130BPM不累积误差；复制选区原文记录跨网格取整时各头尾落点。
8. 手工源文本小数时间Apply保留；仅改边界不改time/split；Save→Reload字节/字段对照。相邻端点不做未经请求的自动联动。

## 代码切入

Core/AuthoringGrid为唯一时间/横向刻度和生成数字格式；Core/PointPlacement是纯草稿，不直接写SPC；Core/RadialMenuState只处理右键协议。Unity partials StudioBootstrap.Radial与.PointPlacement负责按键/投影/UI；Editing/Timeline负责旧交互门控。LeftControlGate在Windows用VK_LCONTROL，不依赖额外Unity输入包。

菜单GUI使用Box而非Button，避免左键确认。RMB热控件在普通GUI前捕获；子菜单中心切换只能发生一次。注意GUI的Layout/Repaint重复调用以及窗口焦点、DPI。不能在每次OnGUI重复执行文档动作。选项提交仅在MouseUp button1，音符点击放置仅在MouseDown button0，分开两条通道。

AuthoringGrid按局部BPM段原点计算；显示rounded整数候选，Nearest同候选；Inverse仍支持滚速多值。不要从上一帧时间或已roundedstep叠加。PointerMove按原始目标对每个改变的头/尾调用grid.Nearest；直接低层Move用于不吸附命令，不要互换。Raw修改不经过AuthoredNumber。

## 可复现命令

```sh
python Tools/check_source_layout.py
python Tools/verify_reference.py
python Tools/verify_editing_contracts.py --baseline-zip /path/to/InFalsusStudio_Unity_Prototype_v0.2.zip
python Tools/verify_authoring_contracts.py --baseline-zip /path/to/InFalsusStudio_Unity_Prototype_v0.2.zip
# 有.NET8 SDK时才执行：
dotnet run --project Tests/CoreChecks.csproj
```

Unity批处理入口仍为InFalsusStudio.Editor.StudioSetup.VerifyBatch，用用户本机真实Unity路径、项目路径和-logFile，保存退出码与Temp/InFalsusCoreChecks.txt。批处理纯Core不代表GUI和相机实际渲染验收。

## 保留边界

没有新增游戏材质、判定/计分；未确认beam/lane/特殊BPM/group真实本体行为；没有无缝循环/保调伸缩和万级对象性能实测。Windows左CtrlAPI和IMGUI手势必须实机跑；非Windows只是后备。小窗口、多DPI、复杂停流密集网格和透明覆盖是下一轮重点。

后续补丁只改被证据定位的文件。发布时报告真实测试结果，附操作序列而非只说“应该可以”。不把没有发生的Unity运行截图或C#执行写入报告。
