InFalsus Studio 0.2 更新包（从0.1.1升级）

先在旧版另存正在编辑的SPC，退出Unity Play并关闭Unity，备份工程。
把本包 Assets 文件夹合并到你的Unity工程根目录，同名文件替换，保留.meta。
最终路径必须是 工程/Assets/InFalsusStudio/... ，不能是Assets/Assets。
无需删除旧文件夹，不用导入unitypackage，不更改Packages或ProjectSettings。
文档、Tools、Tests、Fixtures可一并放入工程根目录供Codex使用；它们不放到Assets里面。
重新打开Unity → Tools → InFalsus Studio → Open Demo Scene → Game → Play。
顶部应显示UNITY EDITOR 0.2。
详情见Documentation/EDITING_GUIDE_0.2.md。

注意：这是新增源码迭代，未在交付环境执行Unity/C#编译。
先运行Run CSharp Core Checks，再做真实鼠标键盘/保存回读验证。
原相机、轨道、场景、Shader、已有meta和参考谱面不变。补丁只替换列出的资产。
自己的源码、材质、Profile改动先备份；本补丁包含CalibratedProfile新增高度字段。
