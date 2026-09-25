# Enigma 上传附件解包审计（0.4）

依据本次用户上传 `014_enigma.zip` 的实际原始字节，不是网络同名歌曲或猜测。ZIP没有可用的Files解析正文，因此程序解包并读取JSON，校验各文件SHA256。完整机器报告在 `Reference/enigma_import_audit.json`。

## 目录

```
014_enigma/
  song.json
  enigma.ogg
  enigma_bga.ogg
  jacket_large.png
  jacket_small.png
  Charts/
    Minimal_enigma0.json / .spc
    Evolved_enigma1.json / .spc
    Ultimate_enigma2.json / .spc
    Forbidden_enigma3.json / .spc
```

主曲由`audio.path`声明为enigma.ogg；另一个由`supplementary_audio`声明，确实是Vorbis音频，不是视频。主曲元数据163.2594375秒，附加41秒。两个均经ffprobe识别Vorbis；不是Unity解码测试。曲名Enigma，曲师sanmal。PreviewStartSeconds与PreviewEndSeconds不能当谱面偏移。

## 四难度原始记录

**所有note_count都是对象记录数，不是最终连击。** 各JSON是172BPM、4拍一小节。

| UI索引 | 难度 | 元数据Difficulty掩码 | 标级 | 对象数 | Tap | Hold | Flick | Sky段 | 事件 |
|---|---|---|---|---|---|---|---|---|---|
| 0 | Minimal | 1 | 4 | 315 | 108 | 45 | 74 | 88 | 2 |
| 1 | Evolved | 2 | 7 | 540 | 247 | 29 | 121 | 143 | 28 |
| 2 | Ultimate | 4 | 11 | 1243 | 430 | 78 | 374 | 361 | 12 |
| 3 | Forbidden | 8 | 13 | 1492 | 583 | 107 | 325 | 477 | 24 |

四份原SPC均以ICP1开头，包含二进制；不能当作UTF-8 chart/tap文本。当前导入使用配对JSON。独立Python参考转换在Validation/Enigma_Import_Reference，**它们不是Unity生成的运行输出，不是官方binary导出**。

## 不能从这些字段推出什么

不从note_count、group_id、duration或record id凭空推出官方combo tick数。类型3的side开关也不是地面lane编号。extra_flags含左右one-hot缓动位，但名称映射尚无paired plaintext/原游戏实现证明，本版给出显式profile供验证；所有源文件保留以便纠正映射。

## 原始来源校验

- `jacket_small.png`：`63445b1bb11244c1320aa8428b755ef174b25589a92c044852e78245cf3c9ecf`
- `song.json`：`9322b5d3cbc9a7de032b725cc51ed1f52598ac0c77cab820b1786828343b6d7b`
- `jacket_large.png`：`6ff24209256fd780dbb654b31673061b7e9252067d7835973468035738a2b793`
- `enigma.ogg`：`81bf545f64d0b67530247d5351e4b6b14264c18cf2662f415f229e6acdb2cc01`
- `enigma_bga.ogg`：`48b0b5eef548e3333270e6b55153e77bd161b41e05f7741b2e89fab18c11b20f`
- `Charts/Evolved_enigma1.spc`：`dde0f5b04a381494c9c9afc5fdbfd08802e6d78ca56f69b573b8c47e7ed748fe`
- `Charts/Forbidden_enigma3.json`：`d017f6041bcec5c49cd2009b3cb9c873f6c52774bf40328d06fa5ffc20020798`
- `Charts/Minimal_enigma0.json`：`386658158302f6a65cf37f9ed9825989ab588af9b12d071addbde32b510503fb`
- `Charts/Minimal_enigma0.spc`：`0d2820e3b87956e333f7836fb7d31267da3a51cc4141a2bd12a7cc073d101f0b`
- `Charts/Forbidden_enigma3.spc`：`35a4cf2e432cbe9d02d9c161fc8294c4b56f82f014726c7fdb05faba8845ecb8`
- `Charts/Ultimate_enigma2.spc`：`e31fd68c80565fb5be89e68da1b2ddc7de985a3b69021a9705123516b4493b2e`
- `Charts/Evolved_enigma1.json`：`d71ede2a3df62e9c8b0b72d973d4de48245848a31ac22de5cefd0b0eab0ea6ea`
- `Charts/Ultimate_enigma2.json`：`514a87bd30cc1d5a15c677b28b5fc190038377fe83fcf3b35f67daaa2b8c6b95`
