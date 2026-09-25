# InFalsus Studio 0.8 — 实现契约

本文件覆盖0.7的数字idle防抖、百分比横格、几何连通头部策略。继承用户已经运行的0.7；本版代码没有在交付环境经过C#/Unity编译或Windows运行。测试边界详见TEST_REPORT。

## 固定部分

相机、StageSpace、StageRenderer、FlickGeometry、StudioProfile和标定资源、场景、三个Shader、六个key音、侧签贴图、自动指针、物量候选、分数、显式ManualSongSession保持原字节。没有为避开UI遮挡而改变相机或轨道。

新增NoteRenderer投影批次，不变空中实际网格和拾取定义。所有旧meta GUID保持，新源文件有新GUID。

## 1. 延迟字段不是延迟保存

`StudioBootstrap.InputFields`负责控件文本草稿。FieldDraft保存Text/Live/Dirty、控件在根GUI空间的实际Repaint矩形、captured callback、Ticket。数值输入不通过idle计时提交。

OnGUI最前读取键盘Return/KeypadEnter，避免TextField先消费它。点击框外在源面板绘制前捕获失焦；提交动作排到下一Layout，不在打开的GUILayout组中直接换文档。回车/失焦只更新RAM；Save显式冲刷合法输入再调用原ManualSongSession。

排队动作捕获对象身份和值；Ticket使Save已处理过的同一次输入不会再被队列提交第二次。换歌清空field字典，旧回调不得覆盖新歌；批量字段按历史对象+稳定ID捕获，只改指定字段。一次数字失焦之后再点宽度按钮，两者依次作用于最新内存文档，不能用整行快照把合法第二步误判过期。

框外用于失焦的点击不进入场地创建/拖动，防止使用尚未提交的旧细分网格误放一枚note。普通GUI按钮仍可收到点击并排队，提交先于按钮动作。

源语句是单行输入，Return/blur提交；未知字符/参数按既有SpcDocument保存。合法但未被渲染的opaque来源不删除。真正无效输入保留草稿、提示失败，Save失败不清脏。

HUD的标题/曲师/等级也走相同入口，旧OK/Apply按钮取消。模型内NoteProperties.Apply函数仍存在，它不是磁盘写入；不要根据函数名删除原子数据校验。

## 2. 版面

借用当前GUISkin克隆后配置约13*huds字体、输入框垂直居中与padding，OnGUI finally恢复原skin，不能修改别的Editor面板。各左侧面板ScrollView内容宽度明确不超过面板width-26，横向Scrollbar用none；长Label换行，单行路径允许输入框内部滚动。

侧签设计坐标：右边1908，y=179+i*82，未选64x77，选中78x91且上移5。仍先画未选再画选中，命中也尊重前层。右上进度/曲绘未改。

播放按钮透明命中框(22,102,67,31)，只画icon，不画独立Button背景或文字。SCORE原素材下侧小梯形自然承载它。

流速通过Round(value,1,AwayFromZero)量化，slider/输入/恢复都同源。百分比播放档位1/.75/.5/.25循环，不是输入框。参考BPM不改谱面BPM、网格或计数。

## 3. SkyGroups

源group>=0是标签；组首选最早TimeMs，同时间选稳定SourceId。时间空隙、空间变化不产生第二个头。负/缺失group不是全局组，暂各自独立。`SkyConnections`保留兼容类型名，Starts现在委托SkyGroups.Heads，头框/头音/VFX首段逻辑共用它。

`EditHistory.BeforeCommit`是可选的纯内存规范化钩子：Execute在克隆edit后调用，Preview每次从transactionStart克隆edit后调用。取消恢复初始副本，Undo一步包含端点和所有自动group变更。运行时LoadDocument以及切难度恢复History时挂同一个SkyGroups.NormalizeAuthoredChanges。低层历史回归未挂钩时保留原测试隔离。

规范化只处理新增或本次首10参数变化的Sky及其受影响的连接后继，不对加载时所有对象做全局重分组。新建Add自带max+1；前尾/后头同时间且区间重叠则后继接前组。改前段也传播到后段；断开不拆组。显式只改group字段视为用户覆盖，不当几何变化。

多个前继以最近开始时间、稳定源ID确定，不合并无关组。容差：时间0.001ms、范围1e-9。显示投影/轨速/播放头不参加连接判据。新组分配int溢出时拒绝，不回绕到旧组。

默认声音策略仍用历史enum ContinuousRegionStart（为兼容已有调用），其含义更新为GroupFirst；UI只显示组首/关闭。持续音区间并集与group头分离，间隙必须静音；不延长持续床跨越同组间隙。

## 4. 点立得

RMB MouseDown在尝试打开菜单前，优先检查 WorkflowActive 或 pointPlacement.Pending。取消+Use本次事件，保持放置类型/Point mode。放置Ready后选择新note，但不改工具；radial.Press只有tool==Select且有选区才进入Selection菜单，否则按InPointMode开Point。

NoCreate: UseTool(Select)取消草稿，InPointMode=true；Exit则false。空白释放不执行任何更改。侧轨固定一宽与中央宽键自动归位不动。仍是四步Sky、两步Hold；浏览不改已锁端点。

## 5. 空域有理网格与投影

时间网格AuthoringGrid未改。AirAuthoringGrid.SetDivision(N)取1..4096，Snap=round(x*N)/N，Ticks直接i/N。旧不可整除的StepPercent侧车能读，显示为legacy；用户一旦设N就转为等分网格。

原始提供的五谱空域center/width/split共13284字段都是整数（见精度审计）。这不能推导“归一化只许0位小数”。RationalAirCoordinates以有界连分数恢复网格有理数，首尾分别取center/width分母的LCM。新建例如1/3宽1/10→10/30与3/30。优先保留能精确表示的既有整数尺度。编辑已有端点时，其源尺度若能精确有限十进制表达本次值也保留；否则换该端尺度。有限旧值由原AuthoredNumber最多10小数格式器输出（既有格式约定，不是从游戏精度证明得到）；原SOURCE及未改字段不经过它。

有理近似界限1e-11、整数分母上限1e8；普通1/N网格与现有谱值远小于上限。超过界限明确沿用有限格式后备，不声称任意实数被精确表示。UI可显示六位小数，但源循环网格不依赖这个显示数值。

投影取实际已ClipX的air三角形，复制X/Z，Y=.004（中央地面Y0）；独立透明batch排序在空气下面，非实时灯光阴影。无投影hit triangles，且不会影响计数、auto或声音。原Flick装饰Z保留投影，不解释成duration。

## 6. Windows Player

普通Editor项目仍用AudioImporter，不无条件引用可选Networking模块。Player构建预检两内置模块，extraScriptingDefines启用本地file URI的UnityWebRequestAudio路径。不再拿UnityEditor API充当Player功能。

StudioFileDialogs：EditorUtility代理或Windows原生Unicode对话框。Windows文件/文件夹选择放STA线程，只执行Win32，主线程等待后才继续Unity逻辑。SaveDecision明确Save/Cancel/Discard，无自动保存。OpenFolder解码主音乐成功后才初始化workspace与切换当前歌曲；来源哈希复核、多个音乐冲突、原件不变规则保留。

构建入口调用真实BuildPlayer，以Succeeded+exe存在判定成功；记录CoreChecks与BuildReport。临时窗口/Mono设置在finally恢复。没有在本环境构建exe，因此这里只描述实现，不写“Windows运行已通过”。

## 7. 旧测试更新的理由

Release06关于未分组Sky几何连接数量的一个断言、Release07关于group不影响头的一个断言，以及AuthoringSelfTests新Sky缺省无group的精确字符串断言，被新的用户实测规则明确替代；其余历史测试保留。RationalAirCoordinates保留有限十进制旧分母以继续满足旧精度/对边不动回归。

Release08新增组头/时间空隙/同时间不重叠/编辑前继传播/断开保组/撤销取消、有理1/N、注释无损、Point模式保持回归。提供真实C#入口，但尚未在交付环境执行。

资料：Unity 6000.3 GUI.GetNameOfFocusedControl、BuildPlayerOptions.extraScriptingDefines、UnityWebRequestMultimedia.GetAudioClip，及Win32 GetOpenFileNameW。官方文档不是本项目的运行验证。
