# InFalsus Studio 0.9 — 实际检查与验收边界

## 已实际执行

- `python Tools/verify_v09.py --baseline <0.8原包目录>`：**534项独立检查通过**。分类为 `{"group reference": 51, "attachment data": 7, "grid reference": 4, "audio reference": 60, "static connection": 31, "background reference": 21, "layout reference": 20, "resource audit": 221, "baseline audit": 119}`。其中资源/基线检查占有较大比例，不能把总数说成534项功能单元测试。
- `python Tools/check_source_layout.py`：**84份源码的有限词法检查通过**（C#与Shader括号/指令和常见声明检查）。这不是编译器、不检查UnityAPI运行。
- Coldsea附件36490字节、1479行、1475note+3bpm+header；实际末尾3行与说明一致。工作行2954..2956是基于导入格式的推断，未取得用户工作文件；`working_file_observed=false`保留在审计JSON。
- 背景附件2048×1024、原字节SHA与资源副本一致；cover UV按Fraction参考核对，不把数学结果冒充Unity截图。
- 更新仅新增/覆盖：**37个Assets文件**；不删除基线文件。旧meta均与0.8一致，所有meta GUID唯一、每个资产有meta、没有字体文件。
- 更新包实际解压覆盖干净0.8副本，再与完整0.9全部**219个Assets文件**逐路径逐哈希比对。具体清单`Reference/update_manifest_09.json`，覆盖结论`Reference/package_verification_09.json`。

## 没有执行（不可声称通过）

C#编译、CoreSelfTests实际执行、Unity导入/Shader编译、PlayMode、Windows BuildPlayer、原生WndProc/屏幕DPI/双屏负坐标/全屏切换/实际键鼠、音频听音或Profiler、Unity运行截图。交付环境没有Unity、dotnet、csc、mcs。源码包不含伪造exe。

## 提供给本机的真实测试

`Tools/InFalsus Studio/Run CSharp Core Checks`会串联所有历史套件与Release09SelfTests；可选.NET8 `dotnet run --project Tests/CoreChecks.csproj`只测pure Core，不测Unity。

新增C#测试包括：组内严格前后/空组/非网格时间/无写盘，音效pitch常量与正常样本长度/恢复相位，背景cover/无效图像预检，配对时序匹配/冲突拒绝/一次Undo/未知保留/幂等，Coldsea的99拍及新timing原点，显式命名JSON字段与匿名payload保护。

进入Play运行 `Check v0.9 Runtime / Input / Projection` 可输出当前组数量、实际窗口、背景、固定key pitch与路径。`Check Running Camera Landmarks`等旧运行入口保持。Build Windows Editor 0.9会先跑CoreChecks，再调用实际BuildPlayer；真实编译成功后才能得到exe。

## 用户最小验收

1. 输入组7，Next/Prev；选择另一组Sky，组号改变；输入99提示空，不修改note group。小数原time跳转不被grid拉走；滚轮恢复格线浏览。
2. 25/50/75/100%播放：歌曲/谱面时间变速，攻击音与持续样本音高/速度不变；两类持续床同时播放；暂停/seek不串旧声音。
3. 点击SCORE左下梯形空白边缘也播放，图标尺寸不变，场地误点击仍不暂停。
4. 背景示例等比cover无图像留白；Stretch完整图有预期变形；清除/文件损坏保留正确状态，轨道、note、拾取不变。
5. 独立Windows在720p/1080p/1440p/4K、两种窗口/两种全屏之间切换；普通/无边框窗口四边四角缩放；负坐标副屏；F11恢复。Editor内这些选项不修改Unity主窗口。
6. Coldsea配对导入：3BPM在RAM出现，unknown仍保留；undo字节还原；修改过note或冲突BPM拒绝。未Save退出工作文件不变。
7. 外观偏好Save后重开恢复、未Save退出不持久化；谱面保存失败不能误清dirty。外观偏好失败不谎称谱面也回滚。

## 对照研究

18份Arcade-plus关键文件按实际读范围索引，固定commit；没有启动Arcade程序。`ARCADE_PLUS_COMPARISON.md`区分已有、部分、缺失、刻意不做与未确认，不把Arcade-Alpha截图当Arcade-plus功能证明。
