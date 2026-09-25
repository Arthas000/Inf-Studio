# InFalsus Studio 0.4 操作手册

## 1. 更新

基于现有 v0.3.1 合并更新 Assets，同名替换保留 `.meta`。不删除用户的 `Assets/InFalsusStudioUser/Audio`，不修改 Packages。备份实际谱面与校准配置后升级。新文件夹工程第一次打开会按你的请求自动写入自己的 manifest 和工作谱，因此应放在可写工作目录中。

## 2. 打开歌曲目录

在 Demo 的 Play Mode 顶部点击 **Open folder...**，从系统目录选择器选歌曲根目录。

- 顶层没有 MP3/OGG：报错，不创建空工程替代。
- 恰好一个：验证音频、谱面和配置后创建或打开工程。
- 多于一个：报错。唯一可继续的例外，是 `song.json` 已明确声明一份主音频及所有其余 supplementary_audio，且用户在对话框中确认；例外写入工程配置。
- 子文件夹音频不计入候选。本版不递归寻找“可能的主音乐”。WAV 仍可通过旧 Audio 按钮单独加载，但不属于歌曲目录的 MP3/OGG 候选。
- 损坏音乐即使扩展名是 `.ogg` 或 `.mp3` 也会导入失败。不会在音频验证失败时用静音假装打开成功。
- 解压后的 Enigma 必须选择含 `song.json` 的那一层，而不是它的上级目录或 Charts 子目录。

Enigma 初次对话框：显示发现 `enigma.ogg` 和 `enigma_bga.ogg` 两份音频，点击 **Use declared main** 使用前者。后者是元数据声明的附加 **Vorbis 音频**，不是依据文件名猜测成视频，也不会删除或更名。

## 3. 文件结构与安全保存

```
你的歌曲目录/
  audio.ogg 或 audio.mp3      # 原始音乐，不修改
  song.json                  # 已有来源元数据，可选，不修改
  jacket_small.png            # 可选曲绘
  Charts/                    # 已有来源谱面，可选，不修改
  infalsus.studio.json        # 本编辑器工程文件
  .ifstudio/
    Charts/
      0_Minimal.spc
      1_Evolved.spc
      2_Ultimate.spc
      3_Forbidden.spc
    Score/                   # 自愿添加的实测数据
      0.json ... 3.json
```

工程只保存相对路径，整个歌曲文件夹可以迁移。Unity 的解码缓存单独放入 `Assets/InFalsusStudioUser/Audio/song_<音频SHA256>.ogg/mp3`，同一音乐重开复用，不同目录同名音乐不会互相覆盖。

**Save/Ctrl+S** 保存当前难度的工作谱及工程信息；**Save As** 先保存工作谱，再导出一份新明文SPC。导出不能覆盖来源文件、其他难度工作谱或歌曲目录内已有文件。它不是游戏二进制导出功能。

切换难度前自动保存；保存失败则停在当前难度并提示。未完成鼠标预览先取消，不把半成品作为正式谱面。每个难度在当前运行会话保留独立撤销历史，切换不会共用同一撤销栈。

工作谱存在而 manifest 不存在、manifest 格式不支持、已有工作谱缺失，均停止并提示，不擅自覆盖成空谱。来源文件后续改变，只提示，不覆盖已经编辑的工作副本。关闭整个 Play Mode 的未保存修改仍遵循旧版 Recovery 策略：正式退出前务必 Ctrl+S。

## 4. 难度与元信息

右上角四个按钮：**0 Minimal / 1 Evolved / 2 Ultimate / 3 Forbidden**，标签为 MIN/EVO/ULT/FBD。音乐、曲名、曲师和全局 audio offset 共享；谱面、标级、来源和实测数据按难度独立。

打开 Enigma 应见 `Enigma / sanmal`，四个标级为 4 / 7 / 11 / 13，BPM 保留来源 172，而不是套用新工程的 100。

点击 **Song...** 可编辑曲名、曲师、当前标级，点击 Apply song metadata 写工程 manifest。Inspector 仍负责实际 `chart(...)` 与 `bpm(...)` 的 BPM/每小节拍数；音乐时长不会被拿来猜 BPM。

没有曲绘显示 NO JACKET 占位；有来源曲绘优先选择 small，缺 small 用 large。两种图片是同曲的尺寸资源，不是两个难度。

编辑 HUD 占据顶部，工具栏下移但不改相机的 pixelRect、FOV 或场地。F1 仅隐藏工具栏/编辑把手，游戏风格的左右 HUD 保留。Song 面板中的 Show score/song HUD 可关闭此HUD。

## 5. 自动游玩指针

默认开启 **Visual autoplay cursor**。它只移动生成的紫色箭头与黄色导针，不移动 Windows 鼠标，不阻止编辑鼠标，不模拟真实输入，不生成计分判定。

- 当前 SkyArea：以原始时间求左右边界，取 `(L+R)/2`，不是只插值起止中心。没有额外跟随延迟。
- Flick：精确时刻在中心，之后100ms向编码16的左/编码4的右划动默认中央总宽5.5%；下一事件更早出现会截断前一次划动。
- 空档：保留上一落点，在下一空域事件前140ms平滑接近它的起点。
- 暂停、拖时间、倒放跳转、循环：位置直接由目标 chart time 求出，与过了多少帧无关。
- 同时存在多个不同中心的Sky，单指针无法同时居中。初版确定性选择最新开始/来源排序的段，Song 面板显示 OVERLAP；不把平均位置伪装成同时击中。
- Sky与Flick重叠期间优先精确跟Sky，Flick被视为冲突提示。这是视觉演示策略，不是推断出来的游戏操作规则。

黄色导针与箭头同在空域层，从箭头前端向远方延伸。箭头三角形前后深度只是装饰；时间判定仍用chart time，不从模型尖端的Z反算另一时刻。

关闭 Visual autoplay cursor 回到中间静止。Empty 预设也保持中间静止；有音符的预设使用目标时刻求姿态。

## 6. Score 的当前边界

分数底框使用你提供的素材，数字为程序绘制的九位数字与分组符号，不捆绑游戏字体。没有“遭遇得分”。

在未知 Hold/Sky tick 规则时，**不会用记录数假装物量，也不会按音乐百分比增长分数**：开始前 `000'000'000`，开始后 `---'---'---`，旁边明确 `TICK RULE UNVERIFIED`。

Song 面板 `Store measured total` 接受原游戏同谱面实测最终连击，-1表示未知。输入后只显示按你提供规则计算的 `MAX = 100000000 + 实测物量`；它不足以推断每个中间时刻的分数。

`.ifstudio/Score/<难度>.json` 可填原游戏观测到的 timeMs / score / combo，点击 Reload score observations JSON 回放测得数值；标为 RECORDED，不做假插值。数据带工作谱SHA256：改谱后失效，不能把旧总连击继续当成新谱事实。输入新的总连击不会清空已有同版本观测；存在冲突时拒绝。

验证方法见 `SCORE_VERIFICATION_PLAN.md`。不要从本编辑器录屏验证本体计数；本编辑器没有该规则，拿自己验证自己没有意义。

## 7. 建议实机验收

1. 在空目录加一个 MP3/OGG，打开应创建四个100 BPM空谱。多一个或删掉全部音乐应明确报错。
2. 解压 Enigma，确认主音频例外，检查四难度信息、对象数量与波形，再按Play试听。
3. 切难度、Ctrl+Z/Y、保存退出再打开，验证工作谱、来源文件与共享音乐未混淆。
4. F1看HUD和指针；在有变宽/位移的Sky上暂停几处，看黄色轴是否在两边界中间；左右Flick看实际划动方向。
5. 开Song面板/Inspector播放、频繁切难度，不应重现旧GUILayout错误。第一次导入音乐可能短暂阻塞Editor，缓存后重开不应再复制相同音乐。
6. Run CSharp Core Checks + Check Running Camera Landmarks，再与原游戏相同时刻截图比较JSON空域缓动枚举。没有Unity实机结果前，不称“完美移植”。
