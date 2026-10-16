# 本机 YMM4 接入证据

目标为本地安装的 YMM4 **4.56.1.1**，
产品提交 `4b3cdaa22974d33188f874898334f03549b29fc6`。宿主随附 .NET10.0.11；
插件按公开 SDK 构建为 `net10.0-windows10.0.19041.0` / WPF。

## 已观察的一代实现

| 文件 / 类型 | 方法或字段 | 观察到的行为与证据 |
|---|---|---|
| `YukkuriMovieMaker.dll` / `YukkuriMovieMaker.Voice.AIVOICEVoicePlugin` | `Voices`、`UpdateVoicesAsync`、`IsVoicesCached` | 从设置缓存列出话者，提供刷新入口；`evidence/first-generation/AIVOICEVoicePlugin.cs` |
| 同文件 / `AIVOICEVoiceSpeaker` | `CreateVoiceParameter`、`MigrateParameter`、`CreateVoiceAsync` | 实现 `IVoiceSpeaker`，静态信号量串行合成，设置百分比除以100，取得导出文件后交付 YMM4；`AIVOICEVoiceSpeaker.cs` |
| 同文件 / `AIVOICEVoiceParameter` | `Speed`、`Pitch`、`PitchRange`、`Style` | 50–400%语速、50–200%音高、0–200%抑扬，属性通知及撤销，嵌套风格；`AIVOICEVoiceParameter.cs` |
| 同文件 / `YukkuriMovieMaker.Voice.AIVOICE.AIVOICEAPI` | `GetVoiceNames`、`CreateVoice` | 向 `Win64Client.Client` 管道发送一代专用命令；`evidence/first-generation/AIVOICEAPI.cs`。这条内部命令协议不适用于二代 DLL |
| `YukkuriMovieMaker.Plugin.dll` / `IVoicePlugin`、`IVoiceSpeaker` | 公开插件接口 | 合成入口是 `CreateVoiceAsync(text, pronounce, parameter, filePath)`，不存在 `IVoiceEngine`；`evidence/contracts/IVoice*.cs` |
| 同文件 / `Controls.PropertiesEditor.EditorCache` | `Attach` | 条件可见性绑定字段的 `PropertyOwner`，因此嵌套风格对象可按 `HasJ` 等属性显示当前声库的滑块 |

ILSpy 输出是对本机程序集的反编译结果，不等于原始源代码，也不单独证明运行行为。
上表的一代代码只用于确定宿主集成模式；发行包不包含原厂或 YMM4 的程序集及反编译源码。

## REA 证据

使用桥工作区既有 REA **6.5.0** 的本地 CLI 检查托管程序集，未重写全局 MCP 配置。
原始反编译内容和本机运行数据仅保存在开发机，公开仓库仅保留技术发现与验收摘要。

- `evidence/rea-ymm-managed.json`：程序集静态身份，Evidence
  `ev_fc5427a8a8aa9fd0e1c0fa268c20f8cf8139770997cfba3bfe9e9083be4b97ee`。
- `evidence/rea-first-generation-members.json`：选定一代类型的成员、CIL 锚点与调用边，Evidence
  `ev_7b96eb0138ef74d73534c990130ed7c32700715caf17320dce5c05f8daaca941`。
- `evidence/rea-plugin-contract.json`：当前公开语音契约、成员签名及静态局限。

所有元数据 token、MVID 和源码位置仅对 Evidence 记录的精确二进制哈希有效。
类型选择范围外的调用对象仅保留名字；未宣称完整恢复 YMM4 的语音调用图。

## 本插件的实现选择

沿用一代的公开插件契约、声库缓存、百分比参数、嵌套风格、串行合成及宿主文件路径交付方式。
二代传输直接调用现有原版 Editor 内的协议2 DLL：

`YMM4 → IVoiceSpeaker.CreateVoiceAsync → .NET命名管道 → Editor内DLL → 原厂合成接口 → WAV → YMM4`

插件与语音参数均不启动任何进程。正常安装仅增加一个 YMM4 插件 DLL，
不修改 YMM4 自带一代插件、Editor 桥或声库。风格标签严格依据实际枚举结果显示。
当前原厂输出为 48kHz / PCM16 / 单声道；不提供未验证的音素标记、重音编辑或一代预设兼容层。

## 官方公开依据

- [插件构建与 .ymme 打包](https://manjubox.net/ymm4/faq/plugin/how_to_make/)
- [安装位置与加载检查](https://manjubox.net/ymm4/faq/plugin/how_to_use/)
- [官方语音插件样例](https://github.com/manju-summoner/YukkuriMovieMaker4PluginSamples/tree/master/YMM4SamplePlugin/Voice)
- [官方属性编辑器样例](https://github.com/manju-summoner/YukkuriMovieMaker4PluginSamples/tree/master/YMM4SamplePlugin/PropertyEditor)
