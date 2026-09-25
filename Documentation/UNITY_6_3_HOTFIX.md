# 历史记录：0.1.1 Networking热修复

**此文描述0.1.1。0.2已加入默认本地WAV解码和Editor音频导入，不需要用下面的旧临时禁用方案。当前以README及EDITING_GUIDE_0.2为准。**

# Unity 6.3 WebRequest Audio hotfix (v0.1.1)

Some Unity 6000.3 projects disable built-in modules `com.unity.modules.unitywebrequest` and/or `com.unity.modules.unitywebrequestaudio`.
The v0.1 source referenced `UnityWebRequestMultimedia`, `UnityWebRequest`, and `DownloadHandlerAudioClip` unconditionally, which caused CS0103 compilation errors and forced Unity into Safe Mode.

v0.1.1 makes external audio-file loading optional at compile time. The rest of InFalsus Studio now compiles without those modules.

Default behavior:
- No UnityWebRequest module dependency.
- Assign an imported `AudioClip` to `StudioBootstrap.music` if music is needed.

Optional behavior:
1. Enable built-in packages `com.unity.modules.unitywebrequest` and `com.unity.modules.unitywebrequestaudio`.
2. Add `INFALSUS_USE_UNITY_WEBREQUEST_AUDIO` to Player Settings > Scripting Define Symbols.
3. External WAV/OGG/MP3 loading is re-enabled.
