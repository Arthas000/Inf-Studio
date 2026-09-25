# 0.3相对0.2

- 左Ctrl解锁主体/把手/Timeline拖动；普通选择安全，Shift切换选区；松Ctrl结束一次事务。
- 右键长按一级/点立得/宽度方向子菜单；0.5秒悬停；仅右键释放触发，空白无操作，子菜单以原按钮中心展开。
- Tap/Hold独立1..4默认宽度，两侧0/5强制1；Flick子菜单仅左右；Sky无子菜单，默认10%宽/Linear。
- PointPlacement草稿状态替代拖出长条；Sky四步、Flick/Hold两步、Tap一步，最终单次Undo提交。
- AuthoringGrid统一节拍线/高亮/吸附/Timeline；按BPM段原点和格号一次取整到毫秒；小节拍数不再硬编码4；网格ON不可Alt绕过。
- 0..100空域X标尺，默认5%间隔；保留原split；十进制边界编辑与干净生成字段，不全谱降精度。
- Inspector源语句TextArea真实接收修改并单句Apply，可绕过吸附；参数Apply和原文保留继续支持。
- version3侧车会话含网格及默认值，读version2；新增测试fixture/C#回归/独立Python静态与数学检查。
- 相机、场地、Flick外形/固定高度、Profile、Scene、Shaders和原谱样本与0.2字节一致。无额外Unity包依赖。

这是一份新增实现，交付环境未执行C#编译或Unity，详见Documentation/TEST_REPORT.md。
