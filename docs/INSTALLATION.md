# AIVoice2Bridge.YMM4 0.1.0 安装与使用

下载页：[v0.1.0 Release](https://github.com/CyanYuzu/AIVoice2-Bridge-YMM4-plugin/releases/tag/v0.1.0)。
普通用户下载 **`AIVoice2Bridge.YMM4-0.1.0.ymme`**；无需编译，不要用 GitHub 自动生成的 Source code 代替安装包。

## 需要先准备什么

- Windows x64，已安装 **YMM4 4.56.1.1 / .NET10**。这是本版本验证的宿主，其他版本尚未验证。
- 正版、已激活的 **A.I.VOICE2 Editor 2.13.0** 和已授权的二代声库。
- 先安装 [AIVoice2-Bridge v0.1.0](https://github.com/CyanYuzu/AIVoice2-Bridge/releases/tag/v0.1.0) 的 Editor 根目录 DLL。
  Editor 中必须同时保留桥接 `window_size_plugin.dll` 与原厂备份 `aivoice2_window_size_original.dll`。

本插件运行于 YMM4，直接连接 Editor 内 DLL。日常使用只需正常打开 YMM4 和原版 Editor，
不需要 Python、HTTP 服务、AstrBot、SDK 或其他后台程序。

## 下载文件

| 文件 | 用途 |
|---|---|
| `AIVoice2Bridge.YMM4-0.1.0.ymme` | 推荐，交给 YMM4 的插件安装器安装 |
| `AIVoice2Bridge.YMM4-0.1.0.zip` | 同一内容，供手动安装；包含 DLL、说明、校验值和版本清单 |
| `AIVoice2Bridge.YMM4.dll` | 单独 DLL，供手动放入插件目录 |
| `SHA256SUMS.txt` | Release 附件的 SHA-256 校验表 |

下载后可用 `Get-FileHash .\AIVoice2Bridge.YMM4-0.1.0.ymme -Algorithm SHA256` 比较校验表。
这些文件不包含 YMM4 SDK、Editor、声库、测试宿主或本机运行记录。

## 推荐：用 .ymme 安装

1. 保存工程，退出 YMM4。确认 Editor 根目录 DLL 已按上述链接安装。
2. 双击下载的 `.ymme`，按 YMM4 插件安装器提示安装，然后启动/重启 YMM4。
3. 若双击没有启动安装器，在 YMM4 的 **帮助（ヘルプ）→ YMM4 扩展名关联（YMM4用拡張子の関連付け）** 中注册关联，再重试。
4. 在 **文件 → 设置 → 插件列表** 中确认插件已加载，再按下节设置语音。

`.ymme` 安装方式和扩展名关联入口依据 [YMM4 官方说明](https://manjubox.net/ymm4/faq/plugin/how_to_use/)。
此预览版已验证真实宿主加载 DLL；`.ymme` 安装器点击流程及时间线按钮操作尚未逐项实测。
如无法通过安装器安装，可使用下面的手工方式。

## 手动安装

关闭 YMM4，解压 ZIP。如果 ZIP 或 DLL 的 Windows 属性中显示“解除锁定”，先解除锁定。
仅将 `AIVoice2Bridge.YMM4.dll` 放入下面的目录，目录不存在可自行创建：

```text
YMM4安装目录\user\plugin\AIVoice2Bridge\AIVoice2Bridge.YMM4.dll
```

不要放到 Editor 根目录，也不要覆盖 YMM4 自带的一代 A.I.VOICE 插件。
如果已经用 `.ymme` 安装，不要再在另一个子目录放置同一 DLL，避免重复加载。

## 首次使用

1. 正常打开原版 A.I.VOICE2 Editor，等待声库与正版授权加载完成。插件不会替你启动 Editor。
2. 打开 YMM4 的设置，在语音分类找到 **A.I.VOICE2 Bridge**。
3. **API 发现文件**保持留空，通常无需填写路径。
4. 点击 **检查连接**。显示 Editor 已就绪后，点击 **更新声库**，确认声库表格中的“可用”项。
5. 在 YMM4 角色编辑器中将语音引擎选择为 **A.I.VOICE2 Bridge**，再选择已安装声库。
6. 新增台词并使用 YMM4 的语音生成功能。生成音频进入 YMM4 自身的 WAV 缓存/工作流。
7. 按角色需要调整语速、音高、抑扬、引擎音量和原生风格，再重新生成台词。

第一次刷新十分重要：已打开的 YMM4 不会仅因 Editor 此后启动就自动补齐声库列表。
缓存可帮助工程在 Editor 关闭时载入角色列表；实际合成时仍必须保持原版 Editor 正常运行。
两者应使用同一 Windows 用户和权限级别，通常都普通运行即可。

## 可调参数

| 参数 | 范围 / 含义 |
|---|---|
| 语速 | 50–400%，默认 100% |
| 音高 | 50–200%，默认 100% |
| 抑扬 | 0–200%，默认 100% |
| 引擎音量 | 0–200%，默认 100%；独立于 YMM4 音轨音量 |
| 语言词典 | 标准日语 `standard` / 关西 `kansai` |
| 原生风格 | 仅显示声库支持的标签，每项 0–100%，总和不超过 100% |

风格全为 0 使用默认风格；切换声库保留共同标签并清除新声库不支持的项。
J/A/S/C/K/U/V/W/X 等标签直接来自原厂接口，不推测未确认标签的含义。
本版本不提供逐音素/重音编辑、三档停顿、原厂口型标记或中文/英文跨语种模型转换。

## 连接设置与故障排查

- **找不到插件**：检查 DLL 是否位于当前 YMM4 实例的 `user\plugin`，解除下载文件锁定并重启；确认宿主版本为本版验证的 .NET10 环境。
- **找不到 Editor**：先安装 Editor DLL 桥，打开原版 Editor，再“检查连接”。不要把一代 Editor 当作二代启动。
- **权限错误 / ACCESS_DENIED**：Editor 与 YMM4 使用同一用户，避免一方管理员、一方普通运行。
- **没有声库**：先在原版 Editor 中确认安装与授权，再点击“更新声库”。
- **发现路径**：默认 `%LOCALAPPDATA%\AIVoice2Bridge\v2\editor-session.json`；一般保持留空。文件存在本身不代表 Editor 当前就绪。
- **协议或原厂版本不匹配**：仅使用协议 2 桥及本 Release 验证的原厂版本，勿跳过哈希校验。
- **合成超时**：默认 600 秒，可设 10–3600 秒。超时会尝试取消自身任务；若提示取消未确认，请保留任务 ID 并确认任务状态后再重试。
- **空闲恢复**：可在没有活动任务时点击“空闲时恢复引擎”，只重建桥接器自己的 Talker，不会重启或强杀 Editor。

## 更新与卸载

更新时先退出 YMM4，再用新版 `.ymme` 或替换已有同目录 DLL；避免新旧两份 DLL 同时存在。
卸载时关闭 YMM4，删除其插件目录内的 `AIVoice2Bridge.YMM4.dll`，然后重启。
卸载 YMM4 插件不改变 Editor DLL、声库或许可；需恢复 Editor 原插件时，使用 Editor 桥包中的 `INSTALL.ps1 -Uninstall`。

## 验证范围

本机真实 YMM4 SDK 验收 57 项通过，独立进程缓存恢复检查通过；实际 YMM4 正常加载插件已确认。
验收覆盖 8 个正版声库、参数/风格、WAV/哈希、输出保护、重复一致性、超时与定向取消。
设置控件离屏构造/布局已检查；完整 YMM4 窗口中的角色选择、时间线生成和 `.ymme` 安装点击流程尚未逐项操作验收。
其他宿主版本、长期压力和听感质量未验证。详见 [验证记录](https://github.com/CyanYuzu/AIVoice2-Bridge-YMM4-plugin/blob/v0.1.0/docs/VALIDATION.md)。
