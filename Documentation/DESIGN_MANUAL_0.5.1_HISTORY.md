# InFalsus Studio 0.5.1 — 设计契约

此文覆盖0.5中的“元数据提交立即写盘／切难度自动保存／固定左右ease映射”规则。基线0.5由用户运行验收；本次新增C#尚未编译或运行，不用独立Python测试冒充。

## 模块与数据流

| 模块 | 职责 |
|---|---|
| Core/ManualSongSession | 打开时捕获文件字节基线，跨四难度脏状态、暂存实测证据、唯一显式批次Save |
| Core/SongMetadataEdit.Stage | 校验后更新内存字段；不写文件 |
| StudioBootstrap.ManualSave | 当前History同步草稿、元数据输入提交到内存、物量诊断和报告导出 |
| Core/GeometricSkyEase | 从实时左右边界首尾计算方向、单边wall／等X状态、三挡几何映射 |
| Core/ProvisionalComboTimeline | 候选A随机访问的逐物件与累计物量，不生成score |
| Core/Release051SelfTests | 真C#回归入口，须Unity/.NET执行 |

原Core/SongMetadataEdit.Apply只保留为旧低层显式写入API和历史回归；运行时HUD／表单不得再调用它。所有运行时“Enter/Apply”必须调用Stage。工程Save统一调用ManualSongSession.Save。不要将“按钮叫保存”与“数据何时写盘”混为一谈。

## 1. 显式保存边界

`SongFolderProject.Difficulties[i].Document` 是内存草稿；每个difficulty有独立EditHistory。SyncActiveSongDraft只克隆文档、更新内存offset，不调用Save。

创建新workspace是明确Open Folder流程的一部分，不受未保存草稿规则阻止。已有工程打开时，ManualSongSession记录manifest/source song.json/四工作谱/四证据文件的原始字节。修改曲名、评级、音符、offset、证据均只改变内存，难度切换保存的是History对象而非文件。

脏标记是当前草稿与各自**上次保存基线**的对比，不是ResetEditingForDocument建立的临时快照。切回已修改难度后要恢复ManualSongSession的baseline，否则会出现“切一圈星号消失”的错误。

Save先结束活动手势、接收仍在编辑的metadata字段，再同步当前History。构造全部变更文件的before/after清单，所有文件预检查通过后才准备备份并执行替换。来源song.json用LosslessJsonPatch，只替换变化的title/artist/等级对应token；保留超大整数、未知字段、BOM和EOL。Manifest是自有结构可序列化。

每个文件写入前再次核对旧字节，避免覆盖外部变更；全部写完且回读一致后才更新baseline、清除脏标记。失败反向回滚已写文件，仅当目标仍等于本次after时允许恢复；若外部程序又修改了它，拒绝回滚覆盖外部数据，并报告SaveHistory目录。不是跨文件的断电事务。

Save As导出独立文件，不清空工程脏标记，不隐式先Save。OnDestroy、定时器、difficulty切换、Discard均不得写项目内容或恢复副本。旧Recovery文件不清理／不自动加载。换歌／New／Load若有草稿，先Save/Cancel/Discard。重开同一目录时若对话框选择Save，必须重新Prepare读取刚保存的数据，不使用弹窗前的旧prepared快照；音频哈希也需重新核对。

## 2. 缓动把手跟随真实几何

对任意一条边界，`ΔX=Xend-Xstart`。在u=.5：

```
Linear midpoint = (start+end)/2
Out midpoint - Linear midpoint = (sqrt(1/2)-1/2)*ΔX
In  midpoint - Linear midpoint = -(sqrt(1/2)-1/2)*ΔX
```

因此屏幕向左／向右手势不能仅依据“左边界还是右边界”映射码值，必须考虑ΔX符号。Position(code)=sign(ΔX)×{0,+1,-1}；鼠标dx加到这个位置，再按三挡选择；拖动从手势初始数据求值，禁止逐帧累加。

Stationary只在abs(end-start)<1e-9；Pinned=Stationary且start≈0或1。每次AddHandle/BeginEaseSelector直接从当前ChartMath.SkyAt的首尾求值，不能缓存旧wall状态。一侧pinned不影响另一侧。固定X移到墙内仍然Stationary，不等于仍然Pinned；GUI给两种不同说明。只有端点不同才有可改变的正弦边界形状，不添加控制点、不伪造原生曲率。

SetEase针对明确修改的那一侧：Pinned时写0，否则写所选0/1/2。不在导入、预览或普通加载时全谱归一化。SOURCE手改保留原文。

## 3. 候选计数与随机定位

总量继续TickCountResearch候选A。逐时间推进由新ProvisionalComboTimeline对原始time直接查询，不用deltaTime、回放历史状态、track积分或AutoplayCursor.VisualContactTime。

`interval=ceil(30000/onsetBpm)`；Hold:头，内部start+i*interval，尾；Sky:start+i*interval，数量以ceil((D-1)/interval)和最小1限定；Tap/Flick在原time各1。所有时刻以原start为基准，整数间隔是当前候选规则本身，不是编辑节拍网格取整；网格仍从绝对拍号算，不能混用。

按时间查询返回Tap/Hold/Sky/Flick分类、total；选中事件可查所属开始BPM、周期、预计总量、当前量、下一候选tick。正常Seeking不记忆上次值，后退可正确减少。未知/非法字段不输出“完整可信”的计数。超过支持范围拒绝并提示。

五份参考总量全匹配只说明总量经验拟合，不证明tick时刻或BPM跨段处理。跨真正BPM变化仍暂用startBpm，标为hypothesis；track不进计数。界面EST表示候选，MAX／SCORE真实记录处理仍保持独立。不得为了数值好看按歌曲百分比生成分数。

## 4. GUI与基线

新增诊断行采用固定GUILayout调用数量；时间变化只改变文本，不改变当前Layout的控件结构。原GuiFrame冻结和QueueGui继续保留。Ctrl+S在文本焦点检查之前截获并排队到Layout边界执行；HUD/音乐metadata输入有验证，错误保持草稿。

相机、StageSpace、StageRenderer、FlickGeometry、MeshBatch、NoteRenderer、AutoplayCursor、StudioTransport、LeftControlGate、Profile和场景、Shader均保持0.5原字节。所有旧meta GUID不变，新文件补meta。无新Package/font/InputSystem依赖。

## 5. 验证边界

Tools/verify_v051.py是独立Fraction运算、原始资料哈希、有限静态连接和资源对照；Tools/check_source_layout.py只是词法检查。两者不能证明C#类型正确或真实GUI可用。

真正C#入口：Tools/InFalsus Studio/Run CSharp Core Checks或`dotnet run --project Tests/CoreChecks.csproj`。Release051SelfTests覆盖失败注入、并发编辑冲突、多个difficulty的draft/no-write/commit，以及曲率方向和逐时间计数。交付环境未执行。
