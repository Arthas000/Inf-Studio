# InFalsus Studio 0.7 — 测试与验收记录

## 实际执行

环境：Linux工作容器，能读取源码/图片/音视频并运行Python。未找到Unity Editor、dotnet、Mono或C#编译器。未执行Windows运行程序、C#编译、Shader编译、Unity Play、实际听音或Audio Profiler。

### 独立参考及有限静态检查

运行 `python Tools/verify_v07.py`：**121项通过**，细分为：数学32，音频预算参考10，几何参考2，源码连接34，冻结文件哈希28，用户贴图4，资源总体4，合成用例7。

这不是121个Unity或C#测试。数值由独立Fraction/Python参考计算；源码连接是有限文本契约，不保证类型、API和控制流完全正确。

运行 `python Tools/check_source_layout.py`：**67份源文件通过有限词法检查**（括号、预处理配对、常见非法var多声明等）。该检查不是C#解析器，不确认API或程序集可编译。

抽样检查了本轮约51秒的Arcade复制/剪贴/段选录屏的多个时刻，及用户UI/打击效果截图；没有逐帧自动重放键鼠事件。未把原图叠加或外部截图伪装为本版运行截图。

### 资源与不变项

四个1024×1024 RGBA难度侧框原字节复制，来源、索引和SHA256见Reference/v07_user_assets.json。新文件都有.meta，旧GUID保持，全集没有GUID冲突，不附字体、不修改Packages/ProjectSettings。相机/场地/Flick几何/手动保存/计数/原音效等28个冻结对象逐项哈希相等。

最终补丁从v0.6差异生成并覆盖到干净副本，须与完整版Assets逐路径/哈希一致；具体数量和哈希见根目录PATCH_VALIDATION_0.7.json（由打包脚本实际计算），不是手写估计。

## 已提供但未执行的真实C#回归

Release07SelfTests已经挂到CoreSelfTests结尾：批量只改字段/原文保留/原子拒绝/undo、同型和混型选区、范围END规则、复制相对时间与1msregrid、cut取消无源删除/正式drop/冲突、长条首尾对齐、选区径向状态、任意N与未来timing、几何连接的head、两条持续总线、音频预算。

真实入口：Tools/InFalsus Studio/Run CSharp Core Checks；或在.NET8环境运行 `dotnet run --project Tests/CoreChecks.csproj`。不能把“已写这些断言”说成“已经执行通过”。

Unity新增菜单Check v0.7 Runtime and Difficulty Assets会读取运行时music priority/isVirtual/real voice预算并检查4张贴图；旧Camera、KeySound和clipping菜单保留。这些也尚未在交付环境执行。

## 音乐中断诊断的证据程度

确认代码risk：v0.6 FX priority80且pool96，music默认priority更低。在最大真实声道用尽后Unity会virtualize并mute低优先级源（官方AudioSource.priority说明）。本次补丁提高音乐、限制FX预算、连段只打首段、coalesce同样本同时间。**没有实录证明这就是用户所有断音的唯一根因，也没有已听音确认彻底修好。** 密集用例和诊断菜单供实机追踪。

## 合成用例

| 用例 | 记录数 | 验证 |
|---|---:|---|
| 07_Selection_Copy_Range | 6 | 区间1000..2000选4枚，long按尾 |
| 07_Copy_OneMs_Alignment | 2 | 复制231/347→显式对齐231/346 |
| 07_Batch_Properties | 10 | 四类同型参数、两侧宽度不变 |
| 07_Grid_Arbitrary_Subdivision | 2 | N7/11/12和非整数phase timing |
| 07_Connected_Sky_Heads | 5 | 接段/独立段/1ms间隙/并行hold |
| 07_Audio_Dense_Disconnected | 331 | 连续音乐加密集短Sky/Flick |
| 07_Hit_Feedback | 7 | Tap三色、Flick两色、Hold/Sky叠加 |

它们是新编的编辑器测试谱，不是官方认证游戏谱，不证明原版二进制加载兼容。

## 必须在用户Unity继续确认

优先：完整编译；普通参数live与Save字节闭环；剪贴/段选/分步放置RMB取消；Ctrl/Alt状态和失焦；密集音乐不virtual/无dropout；首框首音一致；VFX强弱；小窗口布局；播放隐藏；四难度切换与selected前层；退出不保存。

## 最终打包实际核对

更新Assets合并到干净v0.6后，全部 **182 个文件** 与完整v0.7逐路径/哈希相同。实际新增或改动 **40 个资产文件**；补丁连同配套meta共含 **66 个Assets文件**。全部 **85 个历史meta** 保持原字节。没有要求删除旧资产。
