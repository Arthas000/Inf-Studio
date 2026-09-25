# InFalsus Studio 0.6 — 指针修正、简洁工作区、小节线与打击音

基于用户已经在 Unity 6000.3.24f1 / URP 运行的 0.5.1。**本次交付环境没有执行 Unity 或 C# 编译。** 提供的是源码更新；独立数学检查不等于运行验收。

## 1. 安装和界面入口

先在0.5.1中显式保存需要保留的内容，停止 Unity Play、关闭编辑器并备份工程。解压更新包，将其 Assets 合并到工程根目录，同名替换并保留 .meta。不要删除原文件夹，不修改 Packages / ProjectSettings，也不用旧 .unitypackage。

Tools → InFalsus Studio → Open Demo Scene，然后进入 Unity 的 Play。左侧竖栏底部和 Tools 抽屉标题显示 v0.6。界面建议16:9、至少1280×720。

默认没有横贯画面的大工具栏。左侧为窄按钮栏：

| 按钮 | 作用 |
|---|---|
| Tools > | 展开工具抽屉，再点可收起；默认收起 |
| Play / Pause | 应用内部播放/暂停，空格仍可用 |
| Save / Save * | 显式保存；星号表示未保存内容 |
| Open... | 选择歌曲工程文件夹 |
| Timing > | 独立展开/收起右侧 BPM、拍数和 track 面板 |
| Song... | 歌曲、四难度和物量等工程设置 |
| Wave > | 展开/收起底部时间轴/波形 |
| Undo / Redo | 撤销/重做 |
| Camera > | 校准面板；不要为界面挪动相机 |
| Help | 操作提示 |

原来的网格、视觉滚速、播放倍速、放置默认值、预设、音频路径等选项移到 Tools 抽屉/Camera面板中，不删除点立得操作。Tools和音符参数面板共用左侧空间，打开工具抽屉时暂时覆盖参数面板；关闭抽屉后继续查看选中的音符。

RMB立即出菜单、悬停0.5s子菜单、右键松开确认、左Ctrl解锁把手、滚轮一档一细分等规则保持不变。播放时点击场地仍只选择，不暂停。

## 2. 选中音符后的专用参数面板

点击一个音符后，在 SCORE 下方、竖栏右侧自动出现该音符自己的表单；不必先打开 Timing。取消选择/点击X即关闭。多选时，表单只操作主选中对象，避免不经确认批量覆盖不同参数。

| 类型 | 表单字段 |
|---|---|
| Tap | 时间(ms)、起始lane、宽度1/2/3/4 |
| Hold | 开始时间、结束时间、起始lane、宽度 |
| Flick | 时间、中心X、宽度、Left/Right选项 |
| SkyArea | 开始/结束时间、首端中心/宽度、末端中心/宽度、左右缓动分别选择、group |

Sky/Flick的中心和宽度以中央四轨整体的0～100%显示，原来的split只读保留。宽度10指10%。Sky的两个端点可以有不同split，不会因为表单统一显示百分比就改写分母。

Apply：校验并修改内存，形成一个撤销事务。Reload：丢弃尚未Apply的表单输入，从当前真实音符重新加载。Ctrl+S / Save才写文件。未Apply的表单文字不等于已提交谱面修改，不会被Ctrl+S隐式当作谱面参数。

修改Hold/Sky开始时间，结束时间保持不动；修改结束时间，开始时间保持不动。负时长、无效轨宽和越界的友好字段会被拒绝，整次修改不半途生效。旧事件有不标准数据时，仍可用Advanced原始语句保留/处理，不静默规整原文。

Advanced: original SPC statement默认折叠，展开后有完整语句与Sky几何诊断。Apply SOURCE (no snap)保留手动输入精度，不强制落到网格。外部把手/撤销已改变了这条语句时，旧表单不允许覆盖它，需Reload。

播放中表单Apply禁用；选择和查看仍然可用。Sky表单还保留从该段尾部继续创建一段的按钮，不给group伪造新的游戏判定语义。

## 3. Lights Out中央空域结束时的指针跳变

根因不是轴对称几何：旧逻辑在没有当前活动Sky时会恢复“最后一次已完成Flick”的尾位置，却没有判断是否有更晚的Sky刚刚完成。因此这批中央Sky的每个空档都会恢复到很早之前的左侧Flick位置。

新规则：最新完成的Sky也更新空中动作的退出位置。结束后保持真实尾部截面的中心；若恰在Flick划动后的回中阶段结束，则保持当时的受限退出位置。随后才接近下一空中目标，不跳回陈旧的Flick历史位置。

Lights Out原文中73846～77077的八段Sky首尾中心都为50%。回归重点是74077、74538、74942、75404、75750、76212、76673、77135 ms及其前后1ms。在下一目标需要移动之前，这些空档都应保持50%。非对称Sky保持自己的尾中心，不是无脑将所有结束位置设为50%。

## 4. 中央COMBO和左上数字SCORE

中央新增较大的紫色COMBO；F1隐藏编辑控件后仍保留COMBO和歌曲/SCORE信息栏。0连击时不显示中央大数字。Tools中可单独隐藏中央COMBO。

左上不再以横杠作为正常谱面的占位。暂按自动全Exact预览：

```
SCORE = floor(100000000 × 当前预计连击 / 全曲预计物量) + 当前预计连击
```

起始为0，所有预计判定完成后为100000000+N。不是按歌曲时长百分比增长，也不是每次加一个预先取整的固定分数，倒回/跳转后直接按当前时间重算。空谱显示0。

这是**编辑器AUTO EXACT估计**：物量和tick时间仍是0.5.1候选模型，分数分配/舍入并没有通过原游戏内核或足够录屏证明。左上明确保留EST提示。支持的实测score记录仍可覆盖数字并显示RECORDED。特殊未知数据无法可靠计数时才显示不可用，不输出貌似可信的数值。

SCORE字母按参考截图向右下重新内缩，数字、MAX/当前量和状态分别分行，不压住分数。曲绘和四难度底图叠层不变。

## 5. 小节线不再依赖细分网格开关

Tools中的 Measure lines even when snap grid is OFF 默认开启。

关闭Beat grid：只保留淡灰色约1.15像素宽的小节线，不强制鼠标按小节吸附；这是播放参考线。开启Beat grid：使用原来的细分/整拍/小节颜色，避免重复叠两套小节线。

每个正BPM timing从自己的time建立新小节起点，并用该段BPM和每小节拍数直接计算后续小节。下一段开始后不延用旧相位。缺省meter继承旧值，特殊非正BPM仍按既有保留/提示策略，不声明原游戏完全兼容。

计算遍历完整timing列表和当前可见时间范围。比如未来5123 ms处有BPM变化，只要那个位置已在视野里，就会显示其新小节起点，不等播放头到5123才更换。所有六轨共用同一时间/Z，两侧线顺着原斜面绘制。

## 6. 六种打击音

| 原附件 | 资源名 | 默认触发 |
|---|---|---|
| 59_note_hit_3_draft2.wav.ogg | FloorHit | 中央Tap |
| 59_note_side_hit_draft2.wav.ogg | SideHit | lane0/5 Tap |
| 58_note_hit_skylane_draft1.wav.ogg | SkyHit | 每条SkyArea开始 |
| 60_note_flick_5_draft2.wav.ogg | Flick | 每个Flick原始时间 |
| 58_note_hold_floor+sides_draft1.wav.ogg | FloorHold | 任意六轨Hold持续区域 |
| 58_note_hold_skylane_5.wav.ogg | SkyHold | SkyArea持续区域 |

目前已读的上游SPC语法和本工程没有独立SkyTap类型，因此按用户指定的后备规则，将SkyHit放在SkyArea开头。这不等于证明游戏所有内部格式绝无SkyTap。没有添加一个本体未确认的新音符语句。

FloorHold与SkyHold是两个独立音频总线。地面Hold与空域同时持续，两个样本叠加播放，结束其中一种不会停掉另一种。多个地面Hold互相重叠/相接时使用同一个地面持续床；连续Sky段的持续床也是区间并集，不在每个小段接缝重启循环。攻击音和持续床独立。

Tools→KEY SOUNDS：开关、总音量、攻击音量、持续音量。上方另有音乐音量。

Sky攻击策略可选Each segment（默认，每段头）、Region start（连续区域只打一声）、Off（仅关闭Sky头部攻击音，不关闭Sky持续床）。另有可选“ground Hold头部额外Tap攻击音”，默认关闭；这项是便于对照的编辑器策略，不当作已解明的官方混音。

音乐和key音共享AudioSettings.dspTime/transport锚点，提前排定将来的声音，不依赖某帧是否刚好命中时间。暂停、seek、倍速、loop和换谱会清理未发生的排程；在Hold中间恢复，只恢复持续样本对应相位，不补打一串过去攻击音。Loop B之后的攻击音不会被前瞻排程穿透播放。

**音效本轮只用于谱面播放试听，不模拟真实按键判定，也不在每次估计combo tick上重复播放Hold声音。** Flick攻击按原谱时间，不使用100ms可视扫动延迟。

六份文件原始OGG字节保持不变，Unity将它们作为Resources音频导入。不需要UnityWebRequest、额外NuGet包或外置ffmpeg。默认最多96个调度voice；严重卡顿/满voice时有late/capacity统计。持续样本目前整段循环；没有宣称其首尾一定无缝，实际音量平衡、设备延迟和循环接缝需实机听音。

## 7. 最小验收

1. Lights Out从约73500播放到77500：Sky尾部/空档不回到左边旧Flick。
2. 全屏中央有COMBO、左上有数字分数，暂停/后退对应减少；SCORE/MAX互不遮挡。
3. 初始Tools和Timing收起。选择Tap/Hold/Sky/Flick各一个，左上分别出现对应字段；Apply→Undo→Redo→保存→重开验证。
4. 载入Fixtures/06_Future_Measures.spc，关Beat grid，从3500 ms观察未来5123 ms附近，提前看见新段小节起点。
5. Fixtures/06_KeySounds_AllChannels.spc从0播放，1/1.5/2/2.5/3/3.5秒分别识别攻击音；5.5～8秒听地面与Sky两个持续床叠加。暂停立即止音，从6秒恢复不重复Sky头攻击，循环到B不漏放B后的事件。
6. 重新执行0.5.1手动保存闭环，确认停Play/切难度不写项目文件。

先运行Tools/InFalsus Studio/Run CSharp Core Checks与Check Running Camera Landmarks；新菜单Check v0.6 Key Sound Assets检查真实Unity音频导入。以上C#菜单在交付环境未执行。
