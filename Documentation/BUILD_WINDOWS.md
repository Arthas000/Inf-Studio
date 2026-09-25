# InFalsus Studio 0.9 — 在本机Unity生成Windows可运行版

本次包是源码；交付环境没有Unity/C#编译器，因此不含已认证可运行exe，也没有伪造运行截图。已有0.8独立Windows文件选择与MP3/OGG后端继续；新增window边缘hook与背景shader需要实际Windows运行验证。

## 图形入口

保存→停止Play→合并0.9 Assets→等待编译。确认Windows Build Support已安装于当前使用的同一个Unity（例如6000.3.24f1），不要为打包先升级引擎。

1. `Tools / InFalsus Studio / Run CSharp Core Checks`：执行真正C#测试，保留日志，任何失败先修再打包。
2. `Tools / InFalsus Studio / Prepare Windows Audio Modules (0.9)...`：已启用则就绪，缺失则明确询问启用两个内置UnityWebRequest/Audio模块。普通Editor音乐导入不强制启用它们；Player外部MP3/OGG路径需要。
3. `Tools / InFalsus Studio / Build Windows Editor 0.9...`：选输出父目录，自动建立一个新的版本子目录。必须位于Assets外，不能混合已有exe输出。
4. 只有BuildPlayer.Succeeded且exe真实存在才提示成功。将整个目录一起运行/压缩，不只拷exe。

输出包括 `InFalsusStudio.exe`、`InFalsusStudio_Data`、`UnityPlayer.dll`及Unity依赖；还有`CoreChecks.txt`、`BuildReport.txt`、`READ_ME.txt`。编译成功不等于UI/native/audio验收通过。

## 运行后的显示设置

设置 → 背景/窗口：1280×720、1920×1080、2560×1440、3840×2160、自定义宽高；普通窗口、可调尺寸无边框窗口、独占全屏、无边框全屏。普通和无边框窗口拖边缘缩放；F11恢复普通720p窗口。首次构建默认1600×900 resizable window。保存过本机偏好时启动优先使用该偏好。

构建临时设置defaultScreenWidth/Height/fullScreenMode/resizableWindow/runInBackground/Mono backend，finally还原工程。音频define仅传给本次BuildPlayerOptions.extraScriptingDefines，不永久覆盖全局symbols。原生窗口hook只有Windows Player执行，Editor不会修改Unity主窗口。

## 批处理

关闭该Unity工程，然后在PowerShell执行，路径指向实际工程：

```powershell
.\Tools\Build-Windows.ps1 -ProjectPath "D:\YourUnityProject" -UnityPath "D:\Unity\6000.3.24f1\Editor\Unity.exe"
```

脚本能从ProjectVersion自动查常用Hub路径，找不到再要求实际-UnityPath；不猜不存在的路径。内部类名`InFalsusStudio.Editor.StudioBuild08.BuildBatch`为兼容0.8脚本保留，它构建当前0.9代码。

## 本机验收矩阵

| 测试 | 预期 |
|---|---|
| 普通窗口四角拖动 | 客户区尺寸跟随，场地保持16:9/必要时留边，输入无偏移 |
| 无边框窗口四边/四角拖动 | 无标题栏但可缩放，不是无边框全屏 |
| 双屏与负坐标 | 左副屏上的边缘方向正常，窗口不消失 |
| 普通→独占→无边框全屏→窗口 | 可恢复，F11兜底，不能在全屏期待边缘缩小 |
| 720p/1080p/1440p/4K | 运行时实际分辨率可查看；硬件不支持的独占请求可能适配 |
| 背景PNG/JPG/损坏文件/超大文件 | 有效图可铺满，失败保留上一张，不影响SPC |
| 25%音乐+Flick和两条Hold音 | 音乐/谱面放慢，key音正常音高，地面/Sky持续音并行 |
| Open folder/Ctrl+S/难度切换 | 与Editor语义相同，退出未保存修改丢弃 |

没有承诺IL2CPP、macOS/Linux无边框实现，默认目标Windows x64 Mono。
