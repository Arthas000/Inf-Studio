# InFalsus Studio 0.5 设计契约

以本文件和EDITING_GUIDE_0.5为准。0.4已由用户运行验收，0.5在交付环境未执行C#/Unity。

## 1. 冻结与改动

CameraCalibration、StudioProfile、StudioTransport、StageSpace、StageRenderer、FlickGeometry、LeftControlGate、Profile asset、Demo Scene和三份Shader均与0.4字节一致。MeshBatch和NoteRenderer只增加空中可达裁剪/相应拾取及可视接触保留；不重定义Flick高度或坐标系。

| 新模块 | 责任 |
|---|---|
| Core/AirReachBounds | 归一化0..1、范围诊断、中心容纳、共同位移约束 |
| Core/AutoplayCursor（替换） | 时刻→Sky约束＋同时间Flick簇轨迹，可视接触时刻 |
| Core/ScoreHudLayout | 9个数字+2分隔符+发光内边距的纯布局计算 |
| Core/LosslessJsonPatch | JSON标量token定位/替换，拒绝整体改写原文件 |
| Core/SongMetadataEdit | 明确授权的元数据双文件写入、外部冲突检查、备份和失败恢复 |
| Core/TickCountResearch | 明确标注临时的计数候选、参考语义签名匹配，不产出score |
| Core/Release05SelfTests | 新C#断言与五谱计数回归入口 |

## 2. 指针规划

Rebuild按严格相等的时间戳分组，不把邻近时间合并。全16码按中心X降序，全4码按升序。目标优先中心，中心不可达但足迹与Sky交叠时选择交叠中可达位置。活动Sky的交集与硬范围相交；重复同区域段不产生假冲突。交集为空或Flick足迹不可达会显式提示。

路线按累计横向弧长参数化，以smoothstep时间函数走完有限SwipeMs，下一组会截断前一组。单边缘Flick允许从其可达足迹内侧起步向墙划，以免中心已经在边界时完全不动。混向按近邻＋对应方向短划可视化，不认证同时击打可行性。

每帧按当前时刻重新求Sky范围再裁指针，因此不能只按簇开始那一帧夹住。无Flick时用区间中心，接近/划动/回中有显式阶段。VisualContactTime只供预览隐藏；未接触的同时间Flick短暂停在Z0，扫到后消失。不修改SPC time，不将这100ms当游戏窗口，不给分。

## 3. Reach与几何裁剪

左测试例范围[0,1/6]；镜像右界1。统一worldX=[-centralHalfWidth,+centralHalfWidth]。简单clamp每个顶点会把越界曲面挤成怪形，因此MeshBatch对每个三角形进行两次半空间裁剪，插值交点及Color，再三角化。Strip的扩展发光同样经过裁剪，不能只裁主体后还留下越界光条。

NoteRenderer的空气拾取三角形从裁剪后的air batch取得，原未裁PickQuad不进入空气命中。地键/侧轨/grid的普通绘制不使用这个ClipX；每帧及每个note后显式关闭，以免污染下一种对象。

AirReachBounds只检查达到性：原生0/1/2每条边界单调，端点极值足够保证0..1约束；边界相互交叉是另外的SkyMinimumWidth检查。不要把越界和负宽度混为一类。

编辑事务从原文快照开始；只改本次主动参与的字段。更改宽度不自动改时间。原文编辑绕过鼠标限制但不绕过诊断；不擅自把整个导入谱面量化/缩窄。

## 4. GUI与元数据

左键场地Playing分支先返回，不Pause，不开始History手势。DrawHandle/hover同样在Playing禁用。显式目录/难度切换仍可Pause。

HUD保持固定Rect，不使用GUILayout。数字只在Repaint画矩形，没有Repaint专属BeginGroup（会改变后续控件ID）；ScoreHudLayout提供足够内边距防止越框。文本输入在所有事件中存在，控制名以field开头阻止快捷键穿透。切换字段/提交/面板结构经QueueGui下一次Layout执行，原来的GuiFrame快照继续生效。

元数据改动捕获SongFolderProject实例和difficulty；对象已换则不写入错误歌曲。通过LosslessJsonPatch先扫描原文标量span，不用ProjectJson.Write完整源树；后者内部double不能保存全部Int64。source song.json中的普通标量被替换，未知值及格式原字节保持。

编辑title/artist：仅default及等于旧default的已有翻译。编辑数字：掩码1<<index，更新charts与song_info.ChartInfos的Rating/LevelSectionIndicator。未识别结构不猜字段。自己的manifest仍可用结构化serializer，因为它不带原始引擎Int64引用。

先准备new source/new manifest与before/after恢复快照，检查外部字节无变化，逐文件原子替换，读回，全部成功后更新内存。正常失败尝试只恢复仍等于本次写入内容的文件，不能抹掉外部并发编辑。不是进程崩溃/断电下的跨文件事务；MetadataHistory保留人工恢复路径。

## 5. 计数证据

资源ComboReferences记录五谱实测分类总数及已知note/BPM语义签名（中心/宽度归一化后保留时间、方向、缓动、group等）。正文行序/注释/合理分母比例变化不影响参考匹配。track/来源opaque side事件不进计数签名；不能宣称该签名验证了这些事件全部本体语义。

TickCountResearch的候选规则见COUNT_FINDINGS。它不会生成tick事件或分数。输出分为已匹配测量值和非样本临时值，未知/非法记录或非正BPM拒绝宣称完整可靠。真BPM变化明确警告“开始BPM还是区间BPM未知”。录屏表明显示score会继续插值，单帧不等于内部刚加完的分数。

ICP easing旧待验证项：从新增Notes.zip中的Enigma Minimal/Forbidden明文与014_enigma的JSON逐条配对，1807条note对应、565段Sky的1130边界码全符合同一映射；4/32Linear，8/64SineOut，16/128SineIn。只更新证据文字，不重导入覆盖用户工作谱，也不改映射。

## 6. 实际测试边界

运行2408项独立Python资料/数学/静态检查及48文件有限词法检查。它们不执行Unity组件、C#表达式、材质或IMGUI。新的实际C#测试经CoreSelfTests链接；新Editor菜单调用实际MeshBatch，但本环境未运行。

官方IMGUI参考：
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility.ExitGUI.html
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/EventType.MouseDown.html

不加入新包/字体/输入框架，不更改游戏安装目录，不扩大原素材授权范围。历史测试数字不充当新版本运行证据。
