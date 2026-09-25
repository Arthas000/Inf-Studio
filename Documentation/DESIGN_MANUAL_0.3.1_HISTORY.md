# InFalsus Studio 0.3.1 设计契约

与0.3冲突的交互/网格细节，以本文件和EDITING_GUIDE_0.3.1为准。旧0.3手册留在DESIGN_MANUAL_0.3_HISTORY.md，不能再依其0.28秒门限和宽键拒绝规则修改代码。

## 固定基线

CameraCalibration、StudioProfile、StageSpace、StageRenderer、FlickGeometry、CalibratedProfile、Demo场景、三份Shader、原LightsOut样本、StudioTransport以及LeftControlGate均与0.3保持字节一致。
相机不为修UI或节拍线而移动。编辑网格的屏幕宽度通过新的guide网格完成，不改变轨道面。

## 修改文件责任

| 文件 | 责任 |
|---|---|
| Core/NavigationAndHandles.cs | GroundPlacement新建宽键归位、WheelStepAccumulator输入单位累积、SkyEaseHandle三挡选择 |
| Core/AuthoringGrid.cs | 原有按timing局部网格，新增Step严格邻居导航；同毫秒冲突采用确定的优先级 |
| Core/PointPlacement.cs | 草稿开始时调用同一GroundPlacement；不改已锁端点 |
| Core/RadialMenuState.cs | Press立即Visible；子菜单仍0.5秒，release白名单不变 |
| Core/NavigationSelfTests.cs | 实际C#回归入口，由CoreSelfTests调用 |
| StudioBootstrap.Navigation.cs | 唯一滚轮浏览路径；暂停网格锁；输入刻度设置 |
| StudioBootstrap.GuiFrame.cs | Layout快照、结构修改命令队列；不在Repaint新建控件树 |
| StudioBootstrap.EaseHandles.cs | L~/R~原始时间中点把手，仅通过History修改一个缓动字段 |
| Rendering/BeatGuideGeometry.cs | 基础格线与高亮共用的六轨中心线；抬升与原判定线相同 |
| Rendering/MeshBatch.cs | ScreenStrip像素宽度ribbon，ScreenToWorld反投影，有近面保护 |
| Rendering/NoteRenderer.cs | 时间候选只生成一次；六轨同Z，固定像素线宽、深度重合去重 |

## 时间与输入契约

timeExact=origin+(index/division)*60000/BPM；timeAuthored=roundAwayFromZero(timeExact)。禁止逐次加已取整period。每条正BPM事件重置局部网格和小节起点，meter省略继承；track只影响time->Z。
Wheel向上对应负IMGUI Y，变成正的网格步数。默认unitsPerStep=3，可设1，分数输入留余量，合并事件按多档处理。不能把e.delta.y直接当成拍数。该映射需在实际鼠标上核对，并非Unity对所有设备一律保证delta3。
暂停且gridON时播放头自动Nearest；播放时不能量化音乐时钟。滚轮调用严格Adjacent而不是先Nearest再Adjacent，避免从半拍位置漏掉前方第一条线。
源文档小数和未知字段保持不变；原始SOURCE Apply继续绕过吸附。修改播放头、网格或细分不修改任何已有音符。

## 绘制契约

原细分线worldWidth=.004、alpha=.20，在示例相机下Z=5的1152p投影不到0.2像素，Z=15不到0.05像素；这是看起来缺线的具体原因之一，不是全部拍点没生成。
ScreenStrip以投影中心线为基础，在屏幕垂直方向扩展固定半宽，再反投影，适度朝相机偏移防止落入地面；它是编辑辅助，不是新的世界轨道几何。
宽度为小节3.5px、整拍2.5px、细分1.65px；同时间六条lane横截面共用计算。高亮复用BeatGuideGeometry，避免显示线与指针线使用不同抬升量。
停流/倒流可以多个时间同Z，绘制合并但数据/逆映射候选不合并。正常透视下远端密集重叠是正常的；不删每隔N条线来伪造低密度。

## IMGUI契约

每轮EventType.Layout开头先处理待执行命令，再冻结控件树：面板状态、主对象与字段数组、时间窗附近音符列表、timing列表、诊断文本和音频槽位。整个输入/Repaint过程复用它们。
可变文本长度可以改变下一轮布局高度，但不能改变当前轮GetRect次数。显式数值修改捕获行ID/values；场景选择变化不能把输入误写到另一个对象。
不能在catch GUILayout异常之后继续Draw别的控件来“消除红字”。ExitGUIException必须正确向外传播。Modal Editor对话框不在已打开GUILayout组里执行。
这里修的是源码中观察到的风险路径；没有Unity实机日志重放，不得把静态检查写成GUI错误已复现并完全消除。

## Sky中点与宽地键

L~/R~在原始时间段的50%处，跟随原生边界求值，仅选择0/1/2；保持原始端点和持续时间。Out/In不是任意数值曲率。如果端点坐标相同，改变ease也不会弯曲。
三挡rail的中心在手势开始时固定，依据初始枚举偏移，避免一抓就变成Linear。所有预览从原始History快照计算，一次拖动一次提交/撤销。
GroundPlacement限定新建：side0/5 width1，中央startLane=min(hitLane,5-width)。仅自动挪起始轨，不裁宽；不推广到导入纠错或整组选区移动。

## 官方API参考与证据边界

Unity关于Layout、Repaint和控件ID序列的说明及所举异常：
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUIUtility.ExitGUI.html
滚轮delta接口说明：
https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Event-delta.html

源码与用户实机截图是这次诊断的依据；上述文档不是对本补丁的执行验证。

### 滑条边界补充

滑条仅在真实用户输入改变时Seek，不在Layout/Repaint时把超出旧谱面末端的播放头钳回非网格时间。滚轮可浏览到末尾之后，方便继续制谱；暂停锁格后Go输入框同步显示实际落点，未参与修改的原文不变。
