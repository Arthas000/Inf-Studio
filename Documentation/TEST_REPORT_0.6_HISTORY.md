# InFalsus Studio 0.6 — 测试报告

## 执行范围

交付环境没有Unity Editor、C#编译器或.NET SDK。本轮**没有执行C#编译、Unity运行、Shader编译、真实鼠标交互或音频设备播放**。下述检查是Python独立参考数学、文件审计和有限源码结构验证，不具备替代作用。

实际执行：

| 项目 | 结果 |
|---|---|
| Tools/verify_v06.py（带0.5.1基线） | 137 / 137通过 |
| Tools/check_source_layout.py | 60份C#/Shader文件通过有限词法检查 |
| 六个音效 | 全部OGG Vorbis、48kHz、双声道；来源与打包字节SHA256一致 |
| 既有.meta | 全部字节不变，新GUID无冲突 |
| 资源总数 | 160个Assets文件，含.meta |
| 更新包含 | 39个变更/新增Assets文件；无需删除旧文件 |
| 补丁实际覆盖基线 | 打包后再执行逐路径、逐哈希重建检查，见PATCH_VALIDATION_0.6.json |

检查详情：Reference/v06_verification.json、source_layout_check.json、key_sound_manifest_06.json。历史版本报告保留为历史，不作为本版执行证明。

## 具体检查了什么

LightsOut八段中央Sky原始数据、首尾/内部对称和此前Flick时序；新逻辑按最近完成Sky保持真实退出位置的源码连接；非对称尾部与正在回中退出的真实C#测试已提供。

小节使用Fraction参考计算，覆盖每段起点、未来BPM、3/4/5及3.5拍meter、相位重置、长时间一次取整。Renderer检查为六轨同Z及独立小节开关，不是将当前BPM复制到全视野。

PreviewScore独立整数计算验证0/中间/结尾、单调性及非累加舍入。六音效源文件数据、Attack路由、Floor/Sky两个区间并集、同时间Flick保留、DSP逆映射、暂停/seek/rate/loop排程边界通过参考/静态条件检查。

四类表单分离、参数写回原split、源行版本保护、无额外File.Write、保存核心冻结、Layout快照与命令队列接线检查。**这不是复现并修复全部潜在GUILayout错误的承诺。**

## 提供但尚未运行的真实C#和Unity测试

CoreSelfTests最后调用Release06SelfTests，包含真实SpcDocument/EditHistory/AutoplayCursor/AuthoringGrid/PreviewScore/NoteProperties/HitSoundPlan断言及真实LightsOut字节输入；保留所有已有回归。

- Tools → InFalsus Studio → Run CSharp Core Checks
- Play后 → Check Running Camera Landmarks
- Tools → InFalsus Studio → Check v0.6 Key Sound Assets

可选.NET8：`dotnet run --project Tests/CoreChecks.csproj`。该入口只是pure core，不测Unity音频/UI。

音效资源菜单检查实际Unity AudioClip是否正确导入，不自动证明两声道持续床已正确听见。仍需按操作手册播放Fixtures/06_KeySounds_AllChannels.spc，测试六种声、地面+Sky同时、Pause、6s中途Resume、Loop、0.5x/1x。录制音画才能测实际设备延迟。

## 未验证/仍有边界

原版计分分配和tick仍是候选，本版AUTO EXACT数值是明确的编辑器预览规则。SkyHit每段头及两种持续床并集是可调整策略，不声称已解明官方音效触发/loop marker。6秒样本整段循环的接缝、音量和高密度攻击声叠加需听音；96voice/严重卡顿时丢弃统计可见。

真实窗口缩放、GUILayout控制捕获、表单应用/并发拖动、音频域重载与OS输出设备切换未执行。独立Windows Player的外部歌曲目录/压缩音乐后端仍是此前范围外功能；本次Resources内六份音效不依赖那个路径。

更新前保存并备份。出现编译错误应保留第一条完整Console错误；操作失败应记录类型/时间/按键顺序与谱面副本，不以改相机解决UI问题。
