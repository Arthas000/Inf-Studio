# Codex 接手：InFalsus Studio 0.4

沿用用户Unity 6000.3.24f1。不要从头重建、不更换渲染管线、不改已经认可的相机/左右侧轨/Flick高度。先读 README、EDITING_GUIDE_0.4、DESIGN_MANUAL、ENIGMA_IMPORT_AUDIT、SCORE_VERIFICATION_PLAN、TEST_REPORT。

## 状态

用户已验收0.3.1；0.4实现工程/HUD/可视指针，但交付环境未运行C#/Unity编译。Python记录逐条检查不是C#执行，不得声称用户已经跑过0.4。

## 第一步：真正的Unity验收

导入补丁后先修首个真实编译错误，再跑 Tools / InFalsus Studio / Run CSharp Core Checks（现在包括SongProjectSelfTests）及 Check Running Camera Landmarks。编辑内Open folder实际导入用户Enigma压缩包的解压目录，记录对话框、音频加载状态、每个difficulty对象数/标题评级、实际截图和Console。

本地装有.NET8可执行 `dotnet run --project Tests/CoreChecks.csproj`，只是纯Core，不验证Unity音频/GUI。如果没有编译器，不借静态检查装作完成。

## 必测流程

音乐-only→四个100 BPM空槽→修改一个难度→切其他难度→切回→undo/redo→保存关闭→重开。0音频与2个无metadata音乐必须拒绝；Enigma的两个OGG必须先报冲突并明确确认主音频，不能由长度/文件名猜。取消不创建manifest。

原曲JSON/binary/song.json/audio/jacket在操作前后计算SHA256不变。编辑文件仅在.ifstudio/Charts，manifest独立。文件夹整体移动后仍可读取相对路径。导入源后来变化不能自动覆盖编辑工作谱。SaveAs不能覆盖来源二进制或另一个难度。

打开Song面板/Inspector播放，快速切难度，确保没有旧GUILayout异常。HUD覆盖区不能穿透成放置。AudioImporter发生同步导入时允许短暂等待，但报错后不得用旧歌配新谱悄悄继续。

## 源码重点

新Core/SongFolderProject、IcpJsonImport、ProjectJson、AutoplayCursor、ScoreEvidence。Runtime partial SongProject/SongHud已接入；不要额外挂第二个Bootstrap。音频缓存以内容hash命名在InFalsusStudioUser，不覆盖用户音频。工程模式SaveWorkingCopy走SaveSongDifficulty，Save As导出另存；旧Loose模式保留。

Right-top曲绘+难度底图叠层来自用户提供贴图，数字自行绘制无外部字体。HUD用固定Rect；Layout新控件列表通过CaptureSongGuiFrame冻结，按钮QueueGui。

## 下一步明确未知

1. **ICP1 extra_flags缓动枚举名称**仅为导入profile。拿Enigma同难度同时间画面对照或配对plaintext确认4/8/16含义，先改唯一映射函数，不改Sky几何弯曲来掩盖解码问题。
2. **combo ticks/中途score算法未知。** 上游total_notes仅是记录计数，不可用。按实验计划采样原游戏：头尾±1ms、phase、BPM变化、group切段、重叠。拟合规则后还必须用另一谱面/难度做保留集验证；计分舍入也单独验证。
3. 自动指针目前为视觉预演，不是合法输入规划器。多Sky/冲突Flick在Song显示冲突，不擅自声明perfect。
4. Editor的OpenFolderPanel和AudioImporter不是Player接口。Windows独立exe仍需原生目录picker/MP3OGG解码后端，不能用#UNITY_EDITOR路径称“打包也能打开”。
5. 长谱perf：Evaluate暂时O(空中记录)，导入同步；实测profiler后再索引/异步，不牺牲相机、原始文档和GUI状态一致性。

## 不做

不在未知tick时让Score按时间/对象数假增长；不复刻“遭遇得分”；不上传未授权游戏素材/音乐到公共仓库；不捆绑字体；不读写游戏安装目录。任何新增兼容性声明必须带实际输入、版本、日志和验证范围。
