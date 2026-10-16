# A.I.VOICE2 Bridge — YMM4 语音插件

在 YMM4 的引擎列表中加入 **A.I.VOICE2 Bridge**，通过本机已安装的二代 Editor API DLL
使用正版声库。接入方式遵循本机 YMM4 自带一代 A.I.VOICE 的语音插件接口：声库枚举、
角色语音参数、`IVoiceSpeaker.CreateVoiceAsync`、WAV 缓存和设置页。

生产组件只有 `AIVoice2Bridge.YMM4.dll`，运行于 YMM4 进程中，直接连接 Editor 的本地命名管道。
插件不会启动 Editor、Python、服务或任何辅助程序。一代引擎入口保持原样，二代作为独立引擎出现。

## 安装和使用

1. 确认原版 A.I.VOICE2 Editor 根目录已安装协议 2 的 [AIVoice2 Bridge](https://github.com/CyanYuzu/AIVoice2-Bridge) `window_size_plugin.dll` 桥，
   并保留它依赖的原插件备份 `aivoice2_window_size_original.dll`。本插件不替换 Editor 的任何文件。
2. 正常打开原版 Editor，等待声库与授权完成加载。
3. 双击 `dist/AIVoice2Bridge.YMM4-0.1.0.ymme`，由 YMM4 安装；也可关闭 YMM4 后手动将
   `dist/package/AIVoice2Bridge.YMM4.dll` 放到
   `YMM4\user\plugin\AIVoice2Bridge\AIVoice2Bridge.YMM4.dll`。
   也可执行 `scripts/install.ps1 -Ymm4Directory 'C:\Apps\YukkuriMovieMaker_v4'` 完成相同的文件复制。
4. 重启 YMM4，在“设置 → 语音 → A.I.VOICE2 Bridge”中检查连接并更新声库。
5. 在角色编辑器选择引擎 **A.I.VOICE2 Bridge** 与具体声库，然后添加台词生成音频。

设置页包含发现文件路径、超时、WAV SHA-256 校验、连接检查、声库刷新和空闲时恢复。
默认从 `%LOCALAPPDATA%\AIVoice2Bridge\v2\editor-session.json` 发现 API，工作区改名不影响连接。
声库列表由 Editor 实际返回后缓存；Editor 关闭时仍可载入角色列表，合成时会提示正常打开它。

## 语音参数

| 参数 | 范围 | 映射到原厂桥 |
|---|---|---|
| 语速 | 50–400%，默认100% | `rate = 百分比 / 100` |
| 音高 | 50–200%，默认100% | `pitch = 百分比 / 100` |
| 抑扬 | 0–200%，默认100% | `emphasis = 百分比 / 100` |
| 引擎音量 | 0–200%，默认100% | `volume = 百分比 / 100` |
| 语言词典 | 标准 / 关西 | `standard` / `kansai` |
| 原生风格 | 每项0–100% | `styles` 中原厂标签与权重 |

风格面板仅显示当前声库支持的标签，可混合多种风格，总权重不超过100%；全部为0使用默认风格。
标签 J/A/S/C/K/U/V/W/X 直接来自原厂接口，不推测未知标签的含义。参数支持项目保存、克隆及撤销。
切换声库时保留共同风格，清除新声库不支持的风格。YMM4 的音轨音量仍独立生效。

本插件不提供二代 Editor 尚未通过桥暴露的预设、逐音素/重音编辑、三档停顿参数或原厂口型时间标记。
当前 YMM4 的 `CreateVoiceAsync` 契约没有 CancellationToken，因此宿主的取消操作不能直接传递至此接口；
插件的合成超时会针对自身任务请求取消并确认终态。未确认取消会明确报出任务 ID。
正常合成使用静态信号量串行调度，输出经格式及可选哈希校验后提交给 YMM4，已有文件不覆盖。

## 开发与验证

已适配目标：本机 **YMM4 4.56.1.1 / .NET10 / Windows x64**，Editor **2.13.0 / Bridge 协议2**。
其他 YMM4 或 Editor 版本需要重新验证。

```powershell
.\scripts\build.ps1 -Ymm4Directory 'C:\Apps\YukkuriMovieMaker_v4'
# 正常打开原版 Editor 后执行开发验收：
.\scripts\test.ps1 -Ymm4Directory 'C:\Apps\YukkuriMovieMaker_v4'
```

还可设置环境变量 `YMM4_PATH`，或把 `Directory.Build.props.example` 复制为
`Directory.Build.props` 并填入本机 YMM4 路径。后者已被 Git 忽略，不会随源码公开。
直接使用 `dotnet build` 时同样支持该本地配置或 `-p:Ymm4Directory=...`。
源码仓库不包含宿主 SDK、原版程序、声库、生成的 DLL/安装包和原始本机运行记录。
公开验收摘要见 [docs/validation-summary.json](docs/validation-summary.json)；文档中 `evidence/`
引用的是仅保留在开发机的原始记录，未对外上传反编译源码或本机数据。

`tests/Acceptance` 仅为开发验收宿主，不属于发行包或运行依赖。它通过本机真实 YMM4 SDK 契约
调用插件并使用真实声库生成 WAV，设置与音频均保存在本工作区。安装后的 GUI 加载与开发宿主验收
分别记录，具体证据见 `docs/FINDINGS.md` 和 `docs/VALIDATION.md`。

卸载时关闭 YMM4，删除插件目录中的 `AIVoice2Bridge.YMM4.dll`，或执行 `scripts/install.ps1 -Ymm4Directory 'C:\Apps\YukkuriMovieMaker_v4' -Uninstall`。
这不会改变 Editor 桥、原厂声库或其他 YMM4 插件。
