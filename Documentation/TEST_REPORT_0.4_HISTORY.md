# InFalsus Studio 0.4 测试报告

## 真实执行范围

- 对用户014_enigma.zip解包数据：四难度JSON，共3,590条note记录逐条类型/侧轨/宽度/时长/flags/有理数约束核对；实际Sky共1,069段。机器明细见enigma_import_audit.json。note_count不作为combo。
- 新Python参考/附件/资源/静态集成检查：**9051条断言通过**。其中大量是逐note与逐Sky采样点展开，并非9,000个独立功能测试，也不执行C#。
- 原有独立Python/reference脚本本轮执行363条检查通过。
- 42份C#/Shader源码通过有限括号、预处理对、常见var声明词法检查。检测器修正了Dictionary<string,object>中逗号的误报。**词法检查不是编译器**。
- 五张HUD素材原始字节与输入SHA256一致；不含字体文件。全部.meta GUID唯一，旧GUID不变。
- 13项相机/场地/标定/Flick/时间播放/Shader基线字节不变。
- 更新包覆盖0.3.1后，**109个Assets文件**逐文件hash与完整0.4一致。改动6个旧资产文件，新增27个资产文件（含meta）。
- 输入Enigma的所有文件在审计前后hash一致。主/附加Ogg经ffprobe识别；不是Unity解码播放证明。

## 尚未执行

本环境未安装Unity Editor或C#/.NET编译器；获取.NET安装脚本的尝试遇到DNS不可用，没有安装成功。因此 **未执行C#编译、CoreSelfTests、Unity PlayMode、音频输出、GPU/GUI截图、独立Player构建**。没有把旧版本用户截图当成0.4运行结果。

源码提供SongProjectSelfTests并接入CoreSelfTests。包括JSON异常/读写、四难度音乐-only工程、音频数量冲突、显式supplementary确认、workspace保存恢复、路径保护、ICP导入profile、未知记录、cursor纯时间求值与实测分数hash失效。测试里的假.mp3/.ogg字节只用于目录规则，**不用于证明解码器能播放**。

## 需要用户/下一轮执行

1. Unity6000.3.24f1导入后Run CSharp Core Checks；Check Running Camera Landmarks。
2. Open folder实测音乐-only MP3与OGG、零/多音频错误；Enigma显式主音频确认→四难度切换→实际播放/波形。
3. 编辑各难度、切换保存、undo/redo、重新打开迁移目录、只读目录失败；原始音乐/JSON/binary hash保持。
4. F1实看ScoreFrame/曲绘/难度底图层叠、SPEED与播放倍速独立；小窗口HUD不能穿透到场地放置。
5. Sky中点/左右Flick、seek/循环、冲突空域的状态，比较真实游戏。
6. 在原游戏验证ICP one-hot到缓动名称映射。位字段全部可分类 **不等于名字语义已验证**。
7. 按SCORE_VERIFICATION_PLAN采集本体计数；未知时不得发布“官方满分/连击模拟已完成”。

## 本版明确限制

文件夹MP3/OGG使用Editor AudioImporter；Player后端待做。没有官方combo/score推断，没有二进制导出，没有完整本体side事件解释。Score只提供外观、未知状态、用户实测上限和观测回放。自动指针是视觉预演，不是多目标判定规划器。首轮音乐同步导入可能短暂阻塞Editor；高密度长谱仍需性能实测。
