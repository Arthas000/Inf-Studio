# InFalsus Studio 0.4 设计契约

继承用户已验证的0.3.1。**新代码尚未Unity/C#实机编译运行**。以下是源码与数据契约，不是原游戏内核解密报告。

## 冻结基线

CameraCalibration、StageSpace、StageRenderer、StudioProfile、FlickGeometry、CalibratedProfile、Demo Scene、三份Shader、LightsOut样本均保持原字节。NoteRenderer只修改指针黄色导针的绘制起点；新的X由独立求值器注入。现有RMB即时菜单/悬停子菜单、左Ctrl门控、网格/滚轮、宽地键、Sky缓动把手不重写。

## 新模块

| 模块 | 职责 |
|---|---|
| Core/ProjectJson | 有边界的独立JSON读写；不修改导入来源元数据 |
| Core/IcpJsonImport | 解码JSON到明文工作SPC；字段和导入枚举集中映射 |
| Core/SongFolderProject | 只读Prepare、四难度、主音频严格验证、相对安全路径、workspace manifest |
| Core/AutoplayCursor | 给定时间直接求视觉指针X；不积累delta、不产生判定 |
| Core/ScoreEvidence | 实测数据与hash失效；严禁记录数代替连击 |
| Core/SongProjectSelfTests | 真C#测试入口，交付时未执行 |
| StudioBootstrap.SongProject | Editor文件夹选择/音乐缓存/工程事务/切难度/界面 |
| StudioBootstrap.SongHud | 固定Rect HUD；提供的五张素材、曲绘、九位数字 |

## 工程写入流程

`Prepare(folder)`验证并构造四难度，不写文件 → 明确处理多音频冲突 → Editor解码选定主音频 → 保存/确认旧文档 → `CommitInitialWorkspace`只新建自己的工作谱和manifest → 原子切换当前文档/音乐。

音频严格只枚举顶层MP3/OGG。出现多份默认错误；仅当来源元数据显式区分主曲与全部其余supplementary且用户确认时例外。不得根据“_bga”文字、最长时长、文件排序或文件大小默选。现有manifest只承认记录过的例外；后来新增不明音乐依然失败。

提供的Enigma副音频41秒，主音频163.2594375秒；偏移不得由时长或PreviewStartSeconds推断。预览起止时间用于歌曲选择试听，不是谱面audio offset。默认offset0，用户可显式校准。

`.ifstudio/Charts`四个工作谱为编辑源。Save不能覆盖输入的JSON/二进制/音乐/曲绘，Save As不能覆盖歌曲目录已有文件。共享音频/offset，四套独立EditHistory。切换前保存失败就不切换，不用Recovery当“保存成功”。保存用原有临时文件/File.Replace/.bak和回读校验。

工作路径在读取manifest时与固定四个路径核对；拒绝../、绝对路径、跨根路径、目录链接。来源路径`Songs/014_enigma/...`在选择歌曲根目录时去除导出前缀，可移动整个工程但不遍历目录外资源。缓存音乐以内容SHA256命名，不拷贝到本包Assets/InFalsusStudio内。

## ICP1导入：证据与未确定项

用户的四个SPC为`ICP1`二进制，不通过旧plaintext parser，不开发解密/二进制导出。配套JSON顶层含bpm/beats_per_bar/notes/events。note_count是记录数组长度，四难度分别315/540/1243/1492，并非最终combo。

元数据Difficulty是bitmask **1,2,4,8**，UI索引则是**0,1,2,3**。勿直接用Difficulty作为数组下标。各难度保留172 BPM/4拍与等级4/7/11/13。只有新建空难度默认100 BPM/4拍。

JSON映射：type1 Tap、2 Hold、4 Flick、5 Sky；side1中央地键、2左侧、3右侧、4空中。中央地键start_x.value×4是左侧起始lane，width.value×4是轨宽，不是空域中心逻辑。空域center/width分别为有理数，各端取公共分母，不强制所有分母24或100。Flick flags1024/4096映射文本4/16。

Sky one-hot字段：left=flags&28，right=(flags>>3)&28，各取4/8/16。**本版映射4→Linear、8→SineOut、16→SineIn是根据全部样本字段结构建立的明确导入profile，还没有配对明文或游戏实现确认缓动名称。** 必须对照游戏画面验证，不能把“所有标志位都匹配”当作语义已经证实。未知组合原样封装，不降级为Linear。source JSON和binary保留，导入工作谱写注释说明profile。

type0事件为track(time,value)。type3包含side/enabled，但side不是地面lane索引；保留为`icp_event(...)`不模拟轨道开关。extra_flags/packed_flags/relay不支持的note作为inert记录保留并提示；原始JSON仍是无损来源。不能为了开头看起来有轨道而把side事件参数冒充已知。

## 指针

每次文档变化Rebuild；每帧Evaluate(chartTime)，复杂度O(空中记录数)，无需按帧累计。当前Sky严格取两边界平均，非“起止中心同一个ease”。Flick短促位移默认100ms/归一化.055，空档140ms接近下一目标。下一事件截断前划动。多个同时空中中心采用确定性优先级并暴露冲突，不平均、不假装全击中。点击编辑用真实鼠标射线，自动指针不劫持。

## HUD/IMGUI

素材载入Resources/InFalsusStudio/Hud。ScoreFrame直接用提供底图；Difficulty0..3按索引映射。曲绘在难度底图前景，底图右部承载缩写与标级，旁边为Scroll speed；不是Playback倍速。

普通HUD绘制使用GUI固定Rect，资源/文字结构不改变GUILayout计数。Song面板列表和当前difficulty在Layout快照；结构按钮用现有QueueGui下一轮Layout执行。模态目录/冲突对话框不在GUILayout组内运行。UI占位不会改相机矩阵，工具栏下移；HUD与面板区域必须拦截场地点击/右键。

Score仅实现外观和实测回放。未知ticks不计算中间分数；totalCombo未知时Max未知。用户声明上限100000000+combo只用于已有实测totalCombo。旧Rust total_notes并非tick模型。source editor object counts永远不传给ScoreEvidence.TotalCombo。改工作谱hash自动失效，无隐式沿用旧物量。

## 已参考的官方接口

- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EditorUtility.OpenFolderPanel.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioImporter.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioImporterSampleSettings-preloadAudioData.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility.ExitGUI.html

本版使用AudioImporterSampleSettings.preloadAudioData，不使用旧AudioImporter.preloadAudioData。Editor解码不等于Player支持：独立播放器仍需另一个合法音频后端。未附带字体文件，标题文字尝试使用系统字体；数字自行画线，不是原游戏字体。
