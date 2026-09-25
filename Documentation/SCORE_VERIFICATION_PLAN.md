# Hold / SkyArea 物量与 Score：可验证的反推计划

## 不把未知量伪装成完成

用户给出的满分规则是 `100000000 + 最终combo`。这说明总combo已知时的最大分数，但不直接说明每次判定的基础分配、EXACT奖励、长条tick与舍入。

固定查阅上游 `yuhao7370/In_Falsus_Editor` commit `d61cd73395ebe6959358f2f5669ba9cf259eadeb` 的 `src/chart/codec/methods_export.rs`：tap_count / hold_count / flick_count / skyarea_count只过滤事件类型计数，total_notes返回tap+hold+flick，甚至不含Sky。它不是最终连击算法，不能复用作本体物量。当前查阅内容没有可采信的Hold/Sky tick公式。

本版score不按对象数、曲长百分比或猜测每半拍一个tick增长。自动指针仅预演，不能用本编辑器录屏验证本编辑器自己猜的判定。

## 路线A：不需要在本体导入自制谱

最小第一批证据：**同一游戏版本，Enigma Minimal 难度，从歌曲开始录到36秒，尽量60fps或更高，画面中能读清combo/score；再补全曲结束的结算和最大combo。** 同时提供该版本的配套谱面（已有附件若版本一致可直接沿用）。保持难度/游戏设置一致，不用视频起点当歌曲0ms。标记每次miss/早晚判定，未全连的结束combo不能当总物量。

附件里已经找到这些有用片段（均为chart毫秒）：

| 源记录 | 类型 | 时间 | 用途 |
|---|---|---|---|
| Minimal note0 | Sky | 1395–2703 | 孤立第一段，观察开始/内部/结束增量 |
| Minimal note1/2 | 同group Sky相接 | 2791–3488 / 3488–4099 | 接缝是否重复计数、连续链是否重置 |
| Minimal note41 | Hold | 28605–28953 | 没有其他音符重叠；长度348ms |
| Minimal note53 | Hold | 34186–34535 | 没有其他音符重叠；长度349ms |

172BPM一拍约348.837ms，两条348/349ms短Hold很适合辨认取整边界。但**尚未据此断言任一条有几个物量**。帧率不足以直接分辨1ms，所以看两条完成后的离散combo增量，比只看帧间时刻更可靠。

先从多个清晰片段抽取 `(chartTime, comboBefore, comboAfter, scoreBefore, scoreAfter, judgement)`，推导候选规则；再拿另一个难度/歌曲的独立片段验证。不以单条长条恰好符合就宣布定论。

## 路线B：已有可用且获准的本地自制谱测试入口时

`Validation/Score_Probes` 含人工明文SPC。**本版不提供游戏ICP1二进制生成/安装方法，不承诺原游戏可直接读取这些SPC。** 只在你已有可用测试入口时将其作为实验规格；否则走路线A。

每个实验尽量只有一个被测对象，分别记录完成前后稳定combo与分数，避免其他Tap/Flick混入。CSV已提供空白观测列，不含编造的expectedCombo。

实验区分的问题：

1. BPM120，持续长度1/124/125/126/249/250/251/499/500/501/1000/2000ms，Hold和Sky分开测：开始是否计1、结束是否计1、内部tick周期、头尾是否双算。
2. 相同2000ms长度，BPM60/120/240：按毫秒计tick还是按音乐拍计tick。
3. 同长501ms，从2000/2037/2123开始：tick锚在音符自身起点还是全局/局部timing网格。
4. 持续中途BPM120→240：用开头BPM、分段积分还是特殊规则。
5. 同一2000ms Sky，单段与两段相接、同group与不同group：切段是否重复计头尾、group是否改变tick归属。
6. 同样长度但中央四宽/左右侧Hold；track暂停/倒流：不要未经实测假定宽度、侧轨、视觉变速一定不影响计数。
7. 单Tap、单Flick观测：先分开验证单体计分，再谈所有类型叠加。

## 计分必须独立于物量模型验证

哪怕已经得到准确总combo，也仍不能默认中途 `floor(100000000*combo/totalCombo)+combo` 就是实际显示分数。需要比较全EXACT逐事件score、最后补偿、整数舍入、长条内部各tick是否同权。先把tick计数和score更新拆成两个接口与两组回归。

游戏遇到miss、断条、重叠、特殊事件时的计分另测；本轮可先确认全EXACT的Auto展示。但任何定论都要说明版本和适用范围。

## 暂存实测证据

打开歌曲项目后，在Song面板填写实测总combo，Store measured total会建立 `.ifstudio/Score/<index>.json`，包含当前工作谱hash。

结构示例（数值都是占位，不是Enigma事实）：

```json
{
  "format": "InFalsusScoreEvidence",
  "version": 1,
  "chartSha256": "由编辑器按当前工作谱填写",
  "totalCombo": -1,
  "note": "填写游戏版本、难度、采样方式与判定情况",
  "observations": []
}
```

observations可以添加实际测到的 `{ "timeMs": ..., "score": ..., "combo": ... }`，严格时间递增，本版面向未断连perfect观测回放。**不要将CSV中的空白填为猜测数值**。改谱导致hash不一致会拒绝旧证据。没有数据时保持未知，而不是制造看起来顺滑的分数。

## 来源

- 本次用户提供014_enigma.zip：精确路径与SHA256见ENIGMA_IMPORT_AUDIT.md。
- 上游计数实现：`https://github.com/yuhao7370/In_Falsus_Editor/blob/d61cd73395ebe6959358f2f5669ba9cf259eadeb/src/chart/codec/methods_export.rs`
- 满分100000000+combo：本次用户给出的规则，尚未作为独立官方文献验证。
