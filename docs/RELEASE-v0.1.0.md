首个公开预览版，为 YMM4 添加独立的 **A.I.VOICE2 Bridge** 语音引擎，保留自带的一代入口。

## 下载什么

- **推荐：`AIVoice2Bridge.YMM4-0.1.0.ymme`**：YMM4 插件安装包。
- `AIVoice2Bridge.YMM4-0.1.0.zip`：相同内容，供手动安装。
- `AIVoice2Bridge.YMM4.dll`：单独 DLL。
- `SHA256SUMS.txt`：附件校验表。Source code 是源码，不是用户安装包。

## 环境与前置组件

验证目标为 **Windows x64 / YMM4 4.56.1.1 / .NET10 / A.I.VOICE2 Editor 2.13.0**。
先按 [Editor 桥 v0.1.0](https://github.com/CyanYuzu/AIVoice2-Bridge/releases/tag/v0.1.0) 安装根目录 `window_size_plugin.dll`，
保留原厂备份 `aivoice2_window_size_original.dll`；声库需由用户合法安装并激活。
使用时只需 YMM4 和原版 Editor；不需要 Python、HTTP 服务、AstrBot 或 SDK。

## 安装与首次使用

1. 保存工程并退出 YMM4，下载并双击 `.ymme`，按 YMM4 插件安装器提示安装，重启 YMM4。
2. 若 `.ymme` 关联失效，在 YMM4 **帮助 → YMM4 扩展名关联** 中注册；也可手动解压 ZIP，
   将 DLL 放入 `YMM4\user\plugin\AIVoice2Bridge\AIVoice2Bridge.YMM4.dll` 后重启。不要重复放置两份 DLL。
3. 正常打开原版二代 Editor，等待声库与授权加载完成；两款软件使用同一 Windows 用户和权限级别。
4. 在 YMM4 的 **A.I.VOICE2 Bridge** 语音设置页保持“API 发现文件”留空，点击 **检查连接 → 更新声库**。
5. 在角色编辑器选择 **A.I.VOICE2 Bridge** 和声库，添加台词生成语音；可调整语速、音高、抑扬、音量、词典与当前声库支持的原生风格。

`.ymme` 使用方式参见 [YMM4 官方安装说明](https://manjubox.net/ymm4/faq/plugin/how_to_use/)。
不支持的项目调谐、三档停顿、口型标记或跨语种模型转换未包含在本版中。

## 卸载和排查

退出 YMM4，删除插件目录中的 `AIVoice2Bridge.YMM4.dll` 即可。此操作不删除 Editor 桥或声库。
无声库时先确认 Editor 内安装与授权，再点“更新声库”；连接失败时确认协议 2 桥已安装、Editor 已就绪，
并避免 Editor 与 YMM4 的管理员权限不一致。

真实 SDK 验收 57 项与缓存重启检查通过，实际 YMM4 加载已确认。
`.ymme` 安装器、角色选择及时间线按钮的完整点击流程尚未逐项实测，因此以预览版发布。
完整步骤、参数表、超时恢复及验证边界见[使用说明](https://github.com/CyanYuzu/AIVoice2-Bridge-YMM4-plugin/blob/v0.1.0/docs/INSTALLATION.md)。
