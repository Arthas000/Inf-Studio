# Codex 接手 — InFalsus Studio 0.8

目标平台仍是用户现有 Unity 6000.3.24f1 / URP / Windows。用户验收过0.7；**0.8在本次交付环境没有C#编译、Unity运行、Windows native runtime或exe构建结果。** 先读README、EDITING_GUIDE_0.8、DESIGN_MANUAL、BUILD_WINDOWS和TEST_REPORT，再接手。不重建场地、不换输入系统。

## 本轮变化

1. `StudioBootstrap.InputFields` 管理Text/Live/Dirty和提交Ticket。Enter在TextField消费按键之前检测，失焦/点击外面/Tab提交，Esc取消；没有idle防抖。只有选择项即时提交，输入字符串本身不影响文档。QueueGui在下一个Layout开始执行。每个控件名字稳定，Note字段包含选区ID；不能在没完成输入时重写成旧值。
2. 所有常用TextField/TextArea都集中经过上述入口。SOURCE也按Enter/blur提交，精度不强制吸附。Ctrl+S先提交有效输入再Save；普通输入提交只改RAM。多个字段的blur/button队列不能互相视作过期：只替换对应key，真正换文档/选区仍需检查。
3. `SkyGroups.Heads`按非负group选择TimeMs、SourceId排序的第一条。之后同group的段无论有无时间空隙都没有head。缺省/-1视作独立旧对象。`SkyConnections.Starts`现在只是调用这个函数，旧名称不代表还使用几何区域规则。
4. `EditHistory.BeforeCommit`在Execute克隆和Preview内调用`SkyGroups.NormalizeAuthoredChanges(before,after)`。修改或新建geometry时，首尾同时间且范围相接/重叠继承前组；改变前一段也要传播到后续，断开不拆组。load和纯group手改不做整谱重新分组；隐式组号变化必须在同一个Undo事务里。切难度恢复History时也重新接hook。
5. `AirAuthoringGrid.Division`为1/N(1..4096)，设置和sidecar v5支持；旧percentage sidecar仍能读取。`RationalAirCoordinates`用共同整数分母写入循环小数，不将1/3永久截成33.333%。修改原有端点时，若旧split下值是有限小数则保留split；只有需要的那个端点重编码。失败超过分母安全上限则拒绝，不静默漂移。原文输入和未动字段不重写。
6. RMB取消条件必须覆盖`pointPlacement.Pending`，不是仅WorkflowActive；取消吃掉按下事件，不继续唤菜单。完成后仍是Point模式、原tool；有选区但正处于放置工具时，右键不能错误选择Selection/Home层。
7. `NoteRenderer`新增独立ground projection批次。同一X/Z、Y=.004、中央[-2,2]裁剪，不进入pick/计数/音效；ghost不额外投影。相机/Flick高度不改。
8. UI克隆GUISkin并还原，字号/行高/内边距同时缩小；面板固定内容宽、长文字换行。右侧侧签上移缩短，active仍绘制在最前并有优先hit。Play只画图标贴合SCORE原底图的小梯形，Playback按钮循环100/75/50/25；Scroll输入/滑条都取一位小数。

## 新Windows独立运行路径

- `StudioFileDialogs`：EditorUtility与Windows Unicode Win32文件/目录对话框分支。Windows dialog只在STA线程处理native工作，UnityAPI在主线程。保存询问默认Cancel。
- `StudioBootstrap.PlayerAudio`：只有`INFALSUS_USE_UNITY_WEBREQUEST_AUDIO`编译符号下使用本地file URI的MP3/OGG解码。普通Editor源码导入缺可选模块也不报Networking类不存在。
- `SongProject`：Editor沿用AudioImporter；Player通过协程DecodePlayerAudio，成功后才切换工程。旧文档Save/Cancel/Discard、主音频文件hash核对和写入限制不省略。
- `Editor/StudioBuild08`：检查Windows build支持和两个内置模块，跑全部CoreSelfTests，BuildPlayer成功且exe真实存在才报告成功。临时窗口/Mono设置finally还原；extraScriptingDefines只用于该次构建。
- `Prepare Windows Audio Modules`显式确认后才启用Unity内置模块，不自动升级其他Packages。脚本重载打断时重跑检查。
- `Tools/Build-Windows.ps1`用于实际本机批处理，不能把脚本本身叫成已经生成exe。

## 实机验收顺序

1. Unity重新导入，修第一个真正编译错误。Run CSharp Core Checks必须包含所有历史回归和Release08SelfTests，不跳过失败去构建。
2. 当前TimeGrid4改12：输入中不变，Enter后改变；再改6点击空白后改变。Tab换字段、Esc取消、Save时有输入、多字段先后blur、切歌失焦都测。Field草稿不能写到另一首歌。
3. 宽度按钮即时改RAM，SOURCE输入Enter/blur立即预览，磁盘hash直到Save一直不变。两个难度修改后Save整体保存；停止Play未保存就丢弃。
4. group7两段有间隔只第一段head/hit；不同组即使源文件相连LOAD也保留原组。新建/后编辑重叠inherit、断开retain、Undo/Redo、先编辑前段带动后段都测。连续音按时间区间，不越过同组空隙发声。
5. 三步未完成Sky/Hold右键取消，不出菜单；完成后继续放同type；下一RMB是Point，不是Home。NoCreate立即停止；复制/range仍沿用同一取消优先级。
6. Air N3/N7/N12：可见刻度和高亮一致；写回x/split和宽度比例对；反复保存不漂；首尾不同split原未改端保留。
7. 1280×720、1600×900、1920×1080与Windows150%缩放：输入数字不切底，设置/歌曲/时序无横向内容丢失，FBD最高放大也不盖轨道。不要通过改相机解决UI。
8. Check v0.8 Runtime/Input/Projection；投影X边缘与centralfloor吻合，不可被作为第二枚音符pick；开启关闭只改视图。
9. 真实Build菜单生成Windows目录，记录Unity版本、BuildReport与CoreChecks。双击exe后另测打开MP3/OGG/中文路径、四难度、SFX、选文件/保存、取消、退出、窗口缩放。编译成功不等于听音和native窗口已测。

## 兼容与测试声明

原生源码reference空域坐标统计：五份SPC的13284个center/width/split token均为整数；这不等于归一化坐标只有零位小数，也不代表原生格式禁止手写小数。导入器注释`// source_note_id`没有语义事件，不再产生诊断噪声，但原字节保留；真正unknown/非法事件仍诊断并默认折叠。

因为用户新确认改变了group策略，历史关于“无组几何连接自动头合并”的两处音效断言、以及新建Sky无group的一个断言被更新。没有删除测试/跳过错误。理由写在测试报告。

本轮Python和词法结果见TEST_REPORT；不复用旧121/67数字，不把源结构检查叫C#编译。所有未实测部分要继续明确标注。
