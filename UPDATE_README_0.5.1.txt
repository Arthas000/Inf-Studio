0.5 -> 0.5.1
保存当前谱面，停止Play并关闭Unity，备份工程与歌曲目录。
将本包Assets合并到工程根，同名替换，保留.meta；不要复制成Assets/Assets。
不删旧文件夹，不改Packages，不导入旧unitypackage。
Demo进入Play后显示UNITY EDITOR 0.5.1。
注意：新版本取消自动保存。Enter/Apply只改内存，Save project/Ctrl+S才写盘；不保存停止Play，修改丢弃。
Docs当前规则以0.5.1为准。交付环境未执行C#/Unity，先跑Run CSharp Core Checks再操作验收。
