# InFalsus Studio 0.5.1 — 测试报告

## 实际执行

- `python Tools/verify_v051.py --baseline <extracted-v05> --notes <Notes.zip>`：**406 项**通过。
- 分类：原始资料16；独立数学82；有限静态接线21；资源结构205；基线字节对照82。
- `python Tools/check_source_layout.py`：**53 份 C#/Shader**通过有限词法检查（括号、预处理数量、常见var声明错误）。
- 五份源样本全部与 Notes.zip 成员字节哈希一致；候选总量和显式时间表末端分别与所有提供的类型总物量吻合。
- 独立显式tick时间表与随机时间查询公式对照；多次前进/后退得到确定相同结果。
- 四种首尾方向×两侧的把手中点位移、wall/Stationary区分、半拍计数和真正BPM变化人工规格通过独立数学检查。
- 老meta逐字节不变，新源码有独立GUID；不捆绑字体。

`Reference/v051_verification.json`列出每一项和类别；`Reference/source_layout_check.json`列出词法检查文件。源代码“连线正确”检查只说明字符串／调用路径符合所列条件，不能证明函数执行正确。

## 没有执行

**没有C#编译器、.NET SDK或Unity Editor，未执行C#测试、Shader编译、Windows构建、实际键鼠／磁盘事务回放。** 本报告不把53份词法检查称为编译通过，不把独立Python状态数学称为ManualSongSession类实测。

提供而未执行的真实C#测试在Core/Release051SelfTests.cs，接入CoreSelfTests：

- 元数据Stage／四难度编辑／切换不改变磁盘字节；重开只看到保存状态；显式Save全部提交。
- JSON BOM与超大整数保留；无效评级不发生部分字段修改。
- 在第二个文件写前注入错误，验证第一文件回滚、草稿仍脏；外部写入触发冲突且不覆盖。
- 几何把手方向、旧wall移动后状态重算、左右独立；一次事务一步undo。
- 候选累计物量单调、跳转可逆、原时间多物件计数、起点BPM假设以及track-only对照。

## 本地验收入口

Unity：Tools → InFalsus Studio → Run CSharp Core Checks。纯Core也可在安装.NET8的机器，于工程源码根执行 `dotnet run --project Tests/CoreChecks.csproj`。这仍不替代Unity Play/IMGUI/音频验收。

逐项操作表见EDITING_GUIDE_0.5.1.md第6节。尤其测试未保存关闭恢复旧状态、多个难度一起保存、SaveAs不清脏、保存失败不中途消失星号，以及向左/右的曲线把手实际视觉。

## 包覆盖验证

打包阶段会将更新包Assets实际覆盖到0.5副本，与本版完整包Assets逐路径／哈希对照，结果写PATCH_VALIDATION.json。它是安装完整性检查，不是Unity运行。

## 未确定的游戏规则

候选总量拟合与真正逐判定时间不同。多组公式在五谱全部相同，跨真实BPM变化也仍未辨识。新增EST.combo只是可验证的候选输出；不输出未经核实的中途SCORE公式。
