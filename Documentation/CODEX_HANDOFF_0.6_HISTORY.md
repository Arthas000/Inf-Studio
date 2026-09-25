# Codex接手：InFalsus Studio 0.6

用户当前Unity6000.3.24f1/URP。0.5.1已实机使用；0.6新增代码在交付环境未C#/Unity编译运行。先读README、EDITING_GUIDE_0.6、DESIGN_MANUAL、TEST_REPORT。不要从零生成另一套轨道，不换输入框架或依赖包。

## 已修问题与新增内容

AutoplayCursor在Sky tail后不再恢复陈旧Flick位置；计算最近完成空中动作的退出位置。重点原谱LightsOut73846～77077八段及其tail±1ms；不能仅测试有无当前Sky，也不能把所有结束位置固定50%。同时间左右Flick簇路径保留。

新增PreviewScore显示AUTO EXACT估计，floor(100M*C/N)+C；0.5.1候选combo时刻完全不改。中央combo与左上score使用同一时刻原始计数。分数不是官方模型、不是音乐百分比。已有实测score记录优先并标明RECORDED。

Tools竖栏默认收起，Timing独立收起。note被选中后在左上单对象表单显示，Tap/Hold/Flick/Sky各自字段。GUI布局仍是QueueGui/Layout快照；不能在Draw中根据live时间/类型改变控件序列。表单SourceId+OriginalSource失效检查，History.Execute原子Apply，没改的token不动。

BarsBetween按所有timing从各自原点枚举未来小节。snapGrid关仍画灰色小节；snapGrid开沿用彩色格线不重复绘制。不把grid显示和BPM当前值绑在一起，不在BPM到判定线时才换未来线。

六份key音按用户附件接入Resources，具体路径/哈希看key_sound_manifest_06.json。HitSoundPlan分攻击和两个独立持续床；StudioKeySounds共用StudioTransport DSP锚点，revision停止旧排程，loopB限制前瞻。每个Sky头攻击默认开，可切Region start或Off；这不证明有独立SkyTap。

## 必须保留的行为

- 相机、StageSpace、StageRenderer、FlickGeometry、profile、shader和侧轨原数据不动。
- Stage/Apply只改RAM；Ctrl+S/Save唯一落盘；切难度不保存；停Play不保存；SaveAs仅导出。不要让新note面板“Apply”写文件。
- 右键立即菜单、松开右键确认、0.5s类型子菜单；只有左Ctrl允许拖动把手/主体。播放场地左击不暂停。
- 一次手势一个undo。SOURCE不受snap自动改写，%字段写回原split。
- 完整未来timing列表决定小节，track仅影响视觉积分；原始时间计数，不用可视Flick扫动时间。
- 音效暂停/seek/换谱/倍率要撤销已经预排的source，不依靠单帧Time比较相等。持续音不是每个combo tick一声。
- hold_floor+sides和hold_skylane必须能同时播放；停止floor不停止sky。重叠同种持续段用独立总线并集，避免重复音量堆叠。

## 真正的本地验收（这里尚未执行）

1. Unity完整导入/编译，修首条真实C#或Shader错误。执行Run CSharp Core Checks，入口现在包含Release06SelfTests；保留所有历史回归，不只跑新类。
2. Play后Check Running Camera Landmarks。空场、LightsOut73900/74077/74078/74538、EnigmaFBD24070真实截图。
3. Check v0.6 Key Sound Assets确认六个OGG已导入；以Fixtures/06_KeySounds_AllChannels.spc试听全部攻击、并行持续床、6s中途恢复、循环B附近和倍速。音频菜单不测实际延迟，需要实录对照。
4. 工具栏和Timing默认收起。点四类note检查左上字段，无track混入；Apply/undo/redo、group可选字段、不同split、原文长小数保持、过期表单拒绝覆盖。反复切note和面板不能GUILayout报错。
5. Fixtures/06_Future_Measures.spc：播放头在3500时观察未来5123 timing新小节；关/开网格分别验证灰/彩色线；六轨同Z，窗口缩放无脱节。
6. SCORE起始0、中间按C/N、结尾100M+N，倒回正确减少。它不等于原游戏每帧动画显示；状态必须保留估计标识。
7. 重新跑手动保存无副作用验证。NoteProperties改RAM，不得破坏ManualSongSession的多难度草稿和外部文件冲突回滚。
8. Profile热点：目前visible note网格仍简单重建，combo HUD重复查询、UI样式分配可之后优化；先测再改。不牺牲数据和相机。

## 实际执行边界

Tools/verify_v06.py为Python Fraction/数值/文件哈希/有限源码条件；check_source_layout.py只是括号、词法与常见声明检查，不解析C#类型。不能声称它们证明程序集可编译。真正的pure-core可选在.NET8运行Tests/CoreChecks.csproj，Unity菜单是直接入口。

未实现真实游戏判定/分数内核、保调变速、无缝循环、系统设备延迟补偿或独立Player的外部MP3/OGG解码后端。Resources内的本次六个OGG与外部歌曲解码是不同路径。不要捆绑字体，也不要将用户素材发到公开仓库。
