# InFalsus Studio 0.2 测试与验收记录

## 结论边界

已从用户提供的截图和录屏看到0.1.1在Unity6000.3.24f1运行，用户认可场地并指出Flick宽高耦合。这是前版的现场反馈，不是0.2编译或交互验收。

本次交付环境无Unity Editor、C#编译器或.NET运行环境。**没有执行本版C#、Shader编译、Unity PlayMode、Windows构建或真实输入交互测试。** 下列“通过”必须按各自测试范围理解。

## 实际执行

| 执行项 | 结果 | 能证明什么 / 不能证明什么 |
|---|---:|---|
| `python Tools/verify_reference.py` | 355项通过 | 独立Python数学模型、实际参考谱面SHA/统计、相机投影、固定Flick高度/窄端帽、资源GUID。不是运行C#。 |
| `python Tools/verify_editing_contracts.py --baseline-zip <0.1.1.zip>` | 88项通过 | 独立镜像/连续最小宽度/时间分支数学、源码结构条件、GUID/文件对照。不是模拟鼠标已通过。 |
| `python Tools/check_source_layout.py` | 21份源文件通过 | C#/Shader定界符、条件编译数量、常见声明的有限词法检查。不是编译器，也不验证API。 |

参考输出分别是 `Reference/python_verification.json`、`Reference/editing_contracts_check.json`、`Reference/source_layout_check.json`。

对照0.1.1，StageSpace.cs、StageRenderer.cs、CameraCalibration.cs、Demo场景、三份Shader以及用户LightsOut参考谱面均逐字节不变。所有已存在的.meta也逐字节不变，新增脚本获得独立GUID。Profile资产新增固定高度字段，不改相机/轨道字段。

参考谱面SHA-256仍为：
`166a8abf158c8e2ccdebac0b2ee897b371f569b0ae4cdbcc7bbd6709ea41aa7e`

## 新增但未在此处执行的C#回归

CoreSelfTests.Run调用EditingSelfTests.Run，覆盖：稳定ID与删除/追加；BOM/混合换行/数值无操作保存；预览不累积；单次拖动单次撤销；失败预览不污染；取消；整组选区越界原子拒绝；Hold首尾保留另一端；地键宽度；Sky异split/边界与额外参数；九种ease镜像；Flick方向镜像；连续最小宽度及中间交叉；复制相对时间/group映射；显式续段；新建Tap/Hold参数顺序；BPM拍点；倒流/停流消歧；PCM8/16/24/32和float32 WAV、立体声、坏文件拒绝。

本地实际执行入口：Tools / InFalsus Studio / Run CSharp Core Checks，或 `.NET8` 的 `dotnet run --project Tests/CoreChecks.csproj`。该入口失败时保留真实错误，不先修改默认相机。

Play状态中的 Check Running Camera Landmarks 检查实际Unity相机锚点、侧轨接缝及多宽度/多深度的平面Flick高度；不是仅检查Python投影。这个检查也尚未在交付环境执行。

## 需要用户/Codex完成的运行验收清单

- Unity导入无C#错误；打开正确Demo而非Untitled空场。
- 多宽度Flick同距离等高、远处有透视缩小；相机/判定线不位移。
- 放置四种音符，Hold/Sky首尾拖放；Esc取消不新增。
- 点选、Shift/Ctrl、多选主体拖动、空白框选、Ground/Air过滤、Tab重叠循环。
- 拖把手仅改变声明的字段；鼠标在UI上不穿透；窗口失焦取消。
- 一个拖动撤销一次；非法整组移动不留下部分修改；删除后选中身份不串位。
- 新建、复制、剪切、镜像、续段；未知行与额外参数在保存回读仍存在。
- SaveAs到新路径、连续Save、.bak、重新加载；源文件受保护。60秒恢复路径可见。
- 本地WAV、Editor OGG/MP3/AudioClip槽、波形、offset、A/B、书签/sidecar。
- 停流/倒流提示、Time layout、底部时间横向拖动；不改变SPC track。

## 已知限制

尚未实机做上述交互回归、性能压测、编码平台适配、设备延迟与音高保持；A/B不是无缝循环；长谱大量选中仍可能有文档克隆与扫描成本；未完善近裁面三角形框选、窗口中途缩放或GUI重布局的全部边界。没有本体判定/计分、原材质、完整beam/lane/特殊BPM解释、自动无损任意曲线切分或端点链联动。

这是功能更完整的源码迭代，不是“全部测试通过的正式版”。修复后需重新运行真实C#与Unity检查，而非继承本报告数字。
