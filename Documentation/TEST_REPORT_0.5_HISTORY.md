# InFalsus Studio 0.5 测试报告

## 已实际执行

1. `python Tools/verify_v05.py --baseline ../base/InFalsusStudio_Unity_Prototype --song ../input/014_enigma`：**2408项**独立Python资料核对、数学oracle、静态源码连接与冻结基线检查通过。报告：`Reference/v05_verification.json`。
2. 五份用户SPC原字节hash核对，Tap/Hold/Sky/Flick分别与文件名标注一致；候选A与另一不同B在逐事件上也相同，不唯一性已计算验证。
3. 1807条Enigma Minimal/Forbidden明文和JSON逐条配对；565段Sky、1130条边界缓动码全部匹配；规范化签名与运行资源中的参考数据一致。
4. 独立随机三角形裁剪oracle：300例的所有最终顶点位于X[-2,2]，面积不增；实际Enigma24070ms扫动oracle：每毫秒位置均留在当前收窄Sky内并经过左目标。
5. 九位HUD布局在0.1/0.5/1/2/4倍比例内完成数字/分隔符/发光预算检查。
6. `python Tools/check_source_layout.py`：**48份**C#/Shader通过括号、预处理配对和部分常见声明错误的有限词法检查。这不是语言编译器。
7. 附件录屏抽帧直接观察，确认相同连击下分数仍变动；无OCR，无完整认证tick事件提取。
8. 更新包合并覆盖v0.4后的逐文件一致性与.meta完整性检查，见`Reference/v05_package_check.json`（打包时生成）。

## 未执行（不能宣称通过）

本环境无Unity Editor、C#/.NET编译器，未执行C#自测、MonoBehaviour、Shader编译、Unity GUI事件、真实音频、Windows文件选择、用户目录权限故障/断电恢复与构建EXE。没生成0.5运行截图。

本版有实际C#测试入口但只是提供：CoreSelfTests→Release05SelfTests；Tests/Runner可读五份Research/CountSamples；Editor的Check v0.5 Air Mesh Clipping调用真实MeshBatch。需要用户Unity运行。

## 自检中修正过的问题

- 原旧C#测试含越界的全宽Sky尾移，按新硬限制调整预期（仍保留持续时间/分母验证）。
- 原旧“非对称Sky中心”样例在中间发生左右交叉，替换成有效且仍非对称的样例；新增独立冲突诊断用例。
- 修复在Repaint-only HUD路径增加BeginGroup会扰乱后续控件ID的风险：实际数字用有预算的固定Rect，不增加GUI group。
- 静态检查最初因状态提示文字不完全相同失败，检查实际播放分支确实先返回后修正查找文字；不是改掉行为断言来掩盖播放暂停。

## 风险与降级

视觉100ms扫动不等于真实判定窗，多方向/多个不相交Sky可能无法同时满足；显示诊断，不造perfect分。

计数候选A并非唯一模型；新谱只显示PROVISIONAL，已匹配样本显示已提供测量总数/MAX，中途score仍未知。

源JSON双文件更新具有before/after恢复记录、单文件原子替换和普通异常回滚；不是断电跨文件原子事务。极端竞态/权限行为必须本地测试。

GUI右上字段靠Enter/OK显式提交，切难度会取消未提交字段；不会把输入写到另一个难度。过小窗口显示仍需实机调整，但不能为此改变场地相机。

## 回归基线

相机/场地/侧轨/Flick高度/材质/原音乐时钟均逐文件比较不变；编辑网格和点立得核心没有重写。已修改MeshBatch是为了硬边界几何裁剪，不能继续宣称“所有渲染文件都未改”。

新版本真实验收通过前，保留v0.4目录与歌曲目录备份。
