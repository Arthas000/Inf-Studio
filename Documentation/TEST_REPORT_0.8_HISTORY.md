# InFalsus Studio 0.8 — 测试与交付边界

日期：2026-09-25。基线：用户已使用0.7；当前代码输出版本0.8。

## 已执行的检查

1. `python Tools/verify_v08.py`：**346项独立参考数学、附件数据审计与有限静态连接检查通过**。测试包括1/N刻度、group头规则示例、几何连接条件、投影坐标、单次流速取整、输入提交/状态机调用路径及build成功检查条件。**该脚本不执行C#或Unity。**
2. `python Tools/check_source_layout.py`：**74份C#/Shader源文件通过有限词法检查**（括号/预处理/少量声明），不是编译，也不解析UnityAPI类型。
3. 原始Notes.zip五份谱的**13,284个Sky/Flick center/width/split token**全量统计：均为整数。每份source hash和小数位直方图见`Reference/v08_verification.json`，不是对所有游戏版本的格式限制。
4. 实际将增量Assets复制覆盖到干净0.7副本；**196个最终Assets文件与完整版逐路径、逐SHA256一致**。改动旧文件22个，新增文件14个（含7份新C#、7份新meta）；160个文件原字节不变。旧meta修改数0，GUID无重复，所有新资源具meta。
5. 33份冻结基线逐哈希核对，包括相机/场地/Flick几何、音乐时钟、计数/分数/手动保存核心、原有shader、素材和场景。
6. 交付包不含字体、不含假exe、不含需要用户运行的外部解密器。

原始报告：`Reference/v08_verification.json`、`source_layout_check.json`、`v08_package_validation.json`、`v08_frozen_baseline.json`。

## 未执行，不能声称通过

本环境Linux，未找到Unity、unity-editor、dotnet、csc、mcs或mono。本次没有安装并运行Unity，**没有C#编译结果、Unity运行画面、Windows打包结果或可执行exe**。

新增的Windows Win32对话框、UnityWebRequest MP3/OGG解码、模块安装菜单、BuildPlayer入口、PowerShell脚本及第一次真实GUI操作均**未在Windows验证**。文档里的输出目录只是构建成功后的预期结构，不是当前已有binary。

不能用上面的独立检查数量当作0.8可以编译或已经处理所有窗口/输入设备的保证。

## 实际C#测试已编写、尚待执行

`CoreSelfTests.Run`继续运行所有历史测试，然后调用`Release08SelfTests.Run`。新增覆盖：

- 同group有空隙仍仅一个head、不同group源输入不被load改写；未分组legacy独立；按时间不是源文件顺序找首段。
- 新建max+1、连接inherit、改变前段传播后段、断开保留、同事务Undo/Redo与取消。
- 1/N各刻度幂等、1/3+10%共同分母、保留未动尾split和高精度time、反复编辑无累积。
- 原始注释无诊断但字节不变，真正unknown仍诊断；放置取消/完成后Point模式。

历史三处断言因用户明确修订语义而更新：新建Sky输出现在带group0；无group几何相接不再自动去掉head；显式group首条而不是geometric区域决定head集合。**没有为了躲失败移除测试或跳过异常。**

真实入口：Unity菜单`Run CSharp Core Checks`，或有.NET8时`dotnet run --project Tests/CoreChecks.csproj`。前者仍需单独Play模式GUI/音频验收。

## 建议本机检查顺序

先导入编译 → CoreChecks → 1/N输入Enter/blur/Tab/Esc → 有未提交字段时Save/切选区 → group时间空隙/重叠编辑/撤销 → Point未完成RMB → 地面投影与边界 → 多分辨率UI → 本地Windowsbuild → exe中文路径/MP3/OGG/音频/保存。

构建菜单再次跑全部Core测试，仅BuildResultSucceeded且exe存在才报告成功。生成CoreChecks.txt、BuildReport.txt、READ_ME；文件夹必须整体运行。详见`BUILD_WINDOWS.md`。
