# InFalsus Studio 0.9 — 实现契约

以本文件和EDITING_GUIDE_0.9为准；历史几何/保存规则继续。新增代码未在交付环境执行Unity或C#编译，不能把Python/静态检查称为程序集通过。

## 变更定位

| 文件 | 本轮职责 |
|---|---|
| Core/ReviewNavigation.cs | SkyGroupNavigation、KeySoundClock、BackgroundFit、PNG/JPEG尺寸预检 |
| StudioBootstrap.Groups.cs | 当前组字段、选择跟随、严格前后跳转、精确浏览锁 |
| StudioKeySounds.cs | 样本pitch固定1、攻击长度与倍率解耦、持续恢复相位 |
| Rendering/StudioBackground.cs + Background.shader | 屏幕覆盖的far quad、居中cover UV、无拾取、资源所有权 |
| StudioBootstrap.Presentation.cs | 背景/窗口UI、输入结束提交、显式保存本机偏好 |
| StudioWindow.cs | 分辨率/模式请求、延迟原生样式应用、F11恢复 |
| NativeBorderlessWindow.cs | Windows自身窗口的非客户区及边缘命中，安全恢复WndProc |
| Core/TimingReferenceMerge.cs | 配对明文验证及缺少BPM的原子补入、opaque摘要 |
| StudioBootstrap.TimingReference.cs | 导入确认、一条History事务、RAM-only |
| Core/IcpJsonImport.cs | 仅新增显式bpm/beats_per_bar命名字段的导入；不猜匿名载荷 |
| Core/Release09SelfTests.cs | 新实际C#回归入口；由CoreSelfTests调用 |
| Editor/StudioBuild08.cs | 类名兼容旧批处理，菜单/输出更新为0.9，仍本机真实构建 |

## 当前组不是时序组

group字段仍是SkyArea条带组；不创建独立BPM地图。按源TimeMs+SourceId排序，无负号组隐式合并。Prev/Next严格按当前time比较，终点不自动循环，同时间多个对象确定地取一个再跨到更晚时间。选择天空主对象时切group；手输group后不会被旧选择每帧抢回，直到重新选另一条Sky或明确选择对象。

导航是非编辑操作。目标原time可不在拍点网格上，exactGroupJump锁防止EnsureBrowseGridLock把它立刻吸到邻线；一旦主动滚轮/定位/播放改变time即解除。所有源文件不动，History不新增。

## 时间与key音

启动音效时刻、结束持续床和音乐依旧共用StudioTransport的DSP锚点。只将音效source.pitch设置为1。攻击end=startDSP+clip.length；不能沿用clip.length/Rate。持续床end=DspTimeAtChart(noteEnd)，样本循环自身长度不变，因此25%播放的Hold可能循环更多次。

恢复持续床时timeSamples=(chartElapsed/Rate*frequency)%samples。换rate产生epoch重新调度，该恢复相位不是原版无缝保持相位的证明。音乐source仍原pitch=Rate，普通scroll multiplier不变。音乐优先级0、效果预算、两个独立持续床和group首段攻击策略都不动。

## 背景与窗口

背景是camera child在farClip*.94处的quad，layer30与现有视口一致。尺寸由tan(verticalFov/2)*depth计算，UV按比例裁剪。RenderQueue Background，ZWriteOff/ZTestAlways，先于场地，Camera保持SolidColor没有天空盒后覆盖。不应改成晚于音符的GUI.DrawTexture，不应把quad加入raycast，也不动StageSpace。

用户附件2:1，16:9下UV x=[1/18,17/18]、y=[0,1]。Stretch可选[0,1]全部UV。用户数据单独注明来源，不附原repo字体。

窗口先恢复旧原生样式，Screen.SetResolution，跨帧后再安装borderless；并发请求用版本号避免旧请求最终覆盖新请求。只找本PID、visible、class=UnityWndClass。回调不调用UnityAPI，signed WM_NCHITTEST坐标支持负屏幕坐标；WM_NCCALCSIZE去frame，HTLEFT/RIGHT/TOP/BOTTOM/CORNER提供系统尺寸拖动。退出/模式切换恢复原WndProc和style，若他人后来hook不盲目覆盖其回调。Windows x64 Mono为目标，Editor不运行该hook；IL2CPP未认证。

普通窗口由构建时resizableWindow=true提供resize。无边框全屏与任意尺寸无边框窗口是两个不同模式。校准camera仍16:9；非等比window留边，不拉伸note和点击数学。F11是紧急恢复入口，未改Packages、没有全局AudioSettings.Reset。

## 输入与保存

既有InputCommitState统一Enter/blur→RAM，Ctrl+S唯一保存。显示偏好只从成功全局Save接线，单独本机json；不在OnDestroy/resize计时器/换难度写文件。谱面保存与显示偏好保存是两个事务，后者失败只报告自己失败，不声称跨文件原子。当前组浏览本版不持久化为谱面元数据。

## 未知事件

实际Coldsea明文末尾合法BPM：137931/174/99；140000/174/4；140690/174/4。原语法接受任意正meter，没有限制只能3/4/6/8。未知工作行号推断与事实分开，未取得本地.icp JSON载荷时禁止添加数字type→BPM猜测。

TimingReferenceMerge比较header及KnownNote语义签名，支持等价有理分母，但任意不匹配/未知note/已有BPM冲突拒绝。先验证全列表，再append原样bpm。未知icp_event保持原文；它不会进入BeatTimeline，因此添加解明bpm不会重复生效。原始SOURCE编辑仍可手动处理，不把“消除红色提示”等同于删数据。

## 对照基线

不改变CameraCalibration、StageSpace、StageRenderer、StudioProfile、FlickGeometry、AutoplayCursor、所有旧shader、Profile asset、Demo scene、原音效文件、ManualSongSession、SongMetadataEdit、ProvisionalComboTimeline、TickCountResearch、PreviewScore、SkyGroups/Radial/PointPlacement核心。新增背景shader不替换旧三份音符shader。

运行完整旧CoreSelfTests再跑新09套件，保留每个版本的实际结果。见TEST_REPORT和ARCADE_PLUS_COMPARISON。
