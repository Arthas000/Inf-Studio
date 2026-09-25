# Codex接手：InFalsus Studio 0.5

用户已在Unity6000.3.24f1验收0.4音乐/切难度/曲绘层叠，当前0.5新增代码未在交付环境编译。继续现有项目，不重做相机或用新场地掩盖交互问题。

## 第一轮真实验证

导入到工程副本，修第一条C#错误；运行Tools/InFalsus Studio/Run CSharp Core Checks。新Release05SelfTests已接到核心入口，补充元数据无损/恢复路径、同时间Flick、reach/编辑和九位HUD布局。

新菜单Check v0.5 Air Mesh Clipping实际调用MeshBatch构造跨界Quad与扩展光条再断言顶点范围。Check Running Camera Landmarks继续验证原基线。以上测试交付环境均未执行，不伪造日志。

完整源码目录 `dotnet run --project Tests/CoreChecks.csproj` 可额外跑五谱计数签名（需.NET8且当前目录为本包根）。Python `Tools/verify_v05.py --baseline <解压v0.4根> --song <原014_enigma根>` 的2408通过是数据/数学/静态检查，非C#。

## 实际操作用例

1. Enigma FBD 23600ms附近播放，随机左键点空白/音符，音乐继续；要编辑必须先主动暂停。输入标题时按空格不会暂停。
2. 24070ms两枚16码左Flick，在活动收窄Sky中从22/24扫到5/24；其它右簇方向相反。Auto+hidePast时未接触组员短暂停在Z0，扫到才隐藏；不是把SPC time改了。
3. 导入/原文写越界Flick/Sky应警告、原文不变；实际填充/光边/命中三角形均裁至世界X±2。鼠标不允许写新的越界范围。
4. SCORE在720p/1080p/小窗口/最大值/横杠时不越框。右边现有贴图布局冻结。
5. 点击曲绘等同OpenFolder。点击标题/曲师/等级数字，Enter/OK保存，Esc/X取消；等级code不可改。关闭重开核对manifest+song.json。
6. 元数据diff只能包含目标标量；大整数如-1753900809042789876一字不变，独立翻译和其它难度不动。只读目录/文件、外部并发修改、取消、重复保存失败都要实测；记录恢复路径。
7. 打开Inspector/Song播放、切选择、输入新文本，确保Layout/Repaint不回归旧异常。

## 关键文件

- AutoplayCursor：纯绝对时间轨迹，不是计分器；Allowed求当前Sky交集，反向/混向显式诊断。VisualContactTime仅用于表现层。
- AirReachBounds：0..1，不是6轨；EditOperations使用，原文输入绕过后仍受诊断/显示裁剪。
- MeshBatch：X半空间裁三角形，边缘插值颜色；NoteRenderer使用最终网格做空气拾取。不要把它改成只clamp顶点或只裁Flick基线。
- SongHud：固定Rect，ScoreHudLayout包含9位+2分隔符；不在Repaint-only路径调用GUI.BeginGroup。
- LosslessJsonPatch：原文scalar span。绝不整体ProjectJson序列化源song.json，因为double会破坏Int64。
- SongMetadataEdit：显式源数据回写授权，双文件备份+单文件替换+可检测冲突回滚。普通谱面Save不能调用它。
- TickCountResearch：fit不是定律；Source参考匹配失效管理。总数匹配不产生逐tick表或分数。

## 已解明与未解明

565段Enigma配对明文已确认one-hot easing名称（不是还待确认）；保留现在映射，别重导入覆盖工作谱。

五谱总计/四类全部拟合成功，且一个不同模型逐对象结果也相同；真正BPM变化不存在于样本。优先测120BPM Hold251ms/Sky252ms与跨120→240的持续音符。Score录屏有明显数值插值，不能把采样差当实时加分。

不要使用旧版本的“source song.json永不改”笼统规则：0.5只为用户主动元数据修改开放了精确对应字段，其余来源仍只读。也不要扩大到自动回写原谱/二进制/曲绘/音乐。

## 交付诚实性

记录真正Unity版本、编译日志、Core测试、Mesh测试和截图；说明失败项。不要把本包Python日志、原游戏裁剪图或用户0.4截图称为0.5运行证明。素材及附带参考谱面仅供此私有项目验证，不捆绑字体文件。
