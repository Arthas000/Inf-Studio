# InFalsus Studio 0.3.1 测试与验收记录

日期：2026-09-22。用户运行环境为 Unity 6000.3.24f1 / Windows / URP；本交付环境没有Unity Editor、dotnet、csc、mcs或mono。因此 **本次没有执行C#编译、Shader编译、真实IMGUI循环、鼠标设备或音频测试**，没有新的Unity截图/EXE。以下数字均明确按其真实执行范围记录。

## 实际执行

| 检查 | 结果 | 范围 |
|---|---|---|
| `verify_v031.py --baseline ...` | 3364通过、0失败 | 独立有理数/十进制数学、C#候选算法的Python翻译对照、输入/曲线模型、投影数学及源码/资产结构；不是执行C#。其中大多数断言逐项检查3种细分各1024个绝对拍点，不代表数千种实机操作。 |
| `check_source_layout.py` | 34份源文件通过 | C#/Shader有限括号、预处理平衡和常见var声明检查，**不是编译器**。 |
| `verify_reference.py` | 363通过 | 原始样本、几何数学与资源布局的独立Python检查。 |
| `verify_authoring_contracts.py --baseline-zip v0.2.zip` | 158通过 | 旧点立得/原文保护规则的数学与源码检查；TextArea检查更新为Layout快照路径。 |
| `verify_editing_contracts.py --baseline-zip v0.2.zip` | 106通过 | 保留的镜像/连续宽度/资源基线检查。 |
| 补丁覆盖重建 | 见PATCH_VALIDATION.json | 对重建出的0.3 Assets逐文件应用补丁，结果应与完整0.3.1 Assets完全一致。 |

这些脚本不是五套独立运行引擎，部分验证范围有重叠，不能相加包装成Unity测试数量。

## 独立数值结果

130 BPM，4拍小节，每拍N细分，从0到1846ms（含末端）/4有17条、/6有25条、/8有33条。/4落点0、115、231、346、462；第1000000个细分为115384615ms，不是115000000ms。
BPM事件1123ms开始150BPM/3拍时，新/4网格为1123、1223、1323……；新小节为1123、2323……，不是全局0ms的旧相位。回滚能跨timing事件回到前段正确邻居。

在参考相机1152p中，旧世界宽度.004的地面细分线在Z=5约0.198px、Z=15约0.047px，加上旧alpha=.20容易近乎不可见。新辅助ribbon投影宽度固定1.65/2.5/3.5px；独立反投影数学验证了六轨、多距离和三种宽度。**这不是Unity渲染截图或GPU验证**。

## C#测试已提供但未在本环境执行

`NavigationSelfTests`已接入`CoreSelfTests.Run`。包括真实代码的Step/Adjacent、timing重置、跨段、轮滚累积、宽键24种组合（Tap/Hold草稿）、菜单立即出现/.5s子菜单、缓动三挡选择，以及只改arg7/8的一次事务/原文回滚。
旧`AuthoringSelfTests`的“短右键不生效”和“中央放不下直接拒绝”断言已按新要求改为立即生效/左移起始轨；不是简单删除测试。

## GUI问题的证据边界

从0.3实际源码定位到实时CurrentMs列表、选中类型/字段/诊断行在Layout与Repaint之间变化，以及按钮在当前GUILayout组中执行文档更改的风险。本补丁通过Layout快照和延迟命令处理这些路径。用户只提供了简短Console错误，没有完整调用栈；本次没有Unity实机复现，不声称已证明所有触发路径都消除。
Unity官方文档明确说明Layout-Repaint需要一致的控件调用顺序，并列举同类Getting control异常：
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility.ExitGUI.html

## 固定资产

锁定的相机、StudioProfile、StageSpace、StageRenderer、FlickGeometry、场景、CalibratedProfile、Shader、LightsOut文本、LeftControlGate、StudioTransport和ChartMath与0.3字节一致。既有.meta没有换GUID。具体SHA见`Reference/v031_checks.json`。

## 必须在用户机器完成的验收

Unity导入/编译 → Run CSharp Core Checks → Play/Camera Landmarks → Inspector打开播放跨事件窗口/增删/类型切换/Undo/Redo无GUI错误 → 真实鼠标单档和Calibration原始delta一致 → /4 /6 /8六轨线同相位 → timing边界跨越 → L~/R~仅改缓动且一次Undo → 宽2/3/4在末端点击归位 → 保存/回读原文。
播放时不能量化时钟；暂停网格ON才把播放头锁到格线。精确预设1826需要关网格；这不是相机漂移。

## 保留限制

输入delta单位随设备/后端需核实（默认3可设1）。极密网格、退流透明叠加、多显示器DPI/窗口尺寸变化和大型选区仍需性能/交互实测。非正BPM、beam/lane特殊本体规则仍未解明。中点缓动选择不提供任意曲率；端点相同不会凭空弯曲。所有原有素材和音频政策不变。
