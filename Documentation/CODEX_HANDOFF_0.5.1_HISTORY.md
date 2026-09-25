# Codex 接手 — InFalsus Studio 0.5.1

沿用用户Unity6000.3.24f1和当前URP工程。用户已验收0.5的播放、指针簇、文件夹和metadata功能；本版改成显式保存并增加候选累计物量及几何直觉把手。新代码未在交付环境C#/Unity编译运行，首先跑真实验证。

## 不可退回的行为

- Enter/OK/Apply只更新内存；Save project/Ctrl+S才保存所有修改过的difficulty与metadata。切难度不保存、保持独立History；停止Play未保存即丢弃。不要自动写Recovery或在OnDestroy写文件。
- Save As只是导出副本，不隐式Save原工程，也不清除它的脏标记。
- Source JSON精确标量patch，保留大整数/BOM/换行/未知字段；源binary/JSON charts/audio/jacket不改写。失败不能清星号，外部改动不能被静默覆盖。
- 0.5.1核心内存API为SongMetadataEdit.Stage，旧Apply仅保留低层历史测试；runtime不得调用Apply。
- ease拖动方向由该边界EndX-StartX决定，不再固定outward=Out。只有该边界首尾全贴0或100才wall锁；整条平移同X边界是Stationary，不是wall锁。
- 预计combo使用原note time；AutoplayCursor的可视接触延迟、track滚速不能进入计数。候选总量已对五谱吻合，但tick时刻和真BPM跨段仍未确定，不假造SCORE。
- 不动用户已认可的相机/侧轨/Flick高度/簇路径/Shader/输入系统/Packages。

## 先做真实验收

1. 合并补丁，修首条真实编译错误（不整体换架构）。跑CoreSelfTests；已有Release05样本加新Release051测试都必须执行，不能只跑新测试。
2. 两个difficulty各改一音符，改title与rating，切回，验证字节和mtime仍不变；停止Play再打开，旧内容恢复；再次编辑后Ctrl+S重开，全部保留。
3. Ctrl+S在HUD文本焦点中、Song面板输入中、选中SOURCE中分别测试。SOURCE未Apply不会自动写入。metadata无效输入保留并报错。
4. SaveAs导出文件后工程仍脏。打开同一folder并在离开弹窗选Save，应读到刚保存的版本；Discard则旧版。音乐变更不允许旧clip配新chart。
5. Save失败、readonly、外部修改、第二文件替换注入失败：普通失败回滚且草稿仍在；多文件非断电原子，SaveHistory必须可追溯。
6. 四种Sky方向和左右wall样本；向屏幕左拖中段曲线向左，右同理；只改一侧码；一个undo准确恢复；端点移动后wall判定立刻重算。
7. 显示EST.combo@time和单note候选ticks。Seek来回、loop、变速后不能累积偏差。Notes五谱总量保持617/2169/1334/1698/3108；停/变track不改数量。
8. Inspector或Song面板打开时播放/切选区/拖拽/保存，Console不出现Layout/Repaint控件数异常。

## 新文件

- Core/ManualSongSession.cs：基线+显式批次保存；仅Save落盘。
- Core/GeometricSkyEase.cs：当前端点到三挡视觉方向与wall诊断。
- Core/ProvisionalComboTimeline.cs：候选计数绝对时间查询。
- Core/Release051SelfTests.cs：真实C#回归。
- StudioBootstrap.ManualSave.cs：运行时接线和诊断输出。

## 后续研究

原文件证明Enigma开头与LightsOut22154渐快是track，不是真BPM变化。对来源谱做完整bpm/track审计再判断。物量报告带原始chartSHA、time、sourceId、total/current/next；将用户实际连击反例与之匹配。不要把用户的“起点BPM假设”当已知真实游戏规则。

高优先级尚未在此交付环境验证：实际Unity编译、手势/失焦/磁盘冲突交互、Player打包和性能。报告中分清原始资料验证、Python数学验证、真实C#执行、Unity运行，禁止混为一谈。
