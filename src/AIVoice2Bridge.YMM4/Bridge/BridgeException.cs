using System;

namespace AIVoice2Bridge.YMM4.Bridge;

public sealed class BridgeException : Exception
{
    public BridgeError Error { get; }
    public string Code => Error.Code;
    public string? JobId { get; }

    public BridgeException(BridgeError error, string? jobId = null, Exception? innerException = null)
        : base(Explain(error) + (jobId is null ? "" : $"（任务：{jobId}）"), innerException)
    {
        Error = error;
        JobId = jobId;
    }

    private static string Explain(BridgeError error)
    {
        string hint = error.Code switch
        {
            "EDITOR_NOT_READY" => "A.I.VOICE2 编辑器尚未就绪。请正常打开已安装桥接 DLL 的编辑器，完成声库与授权加载后重试。",
            "EDITOR_UNAVAILABLE" => "无法连接 A.I.VOICE2 编辑器。请打开原版编辑器，并确认其根目录已安装 API 桥接 DLL。插件不会代您启动编辑器。",
            "ACCESS_DENIED" => "无法访问编辑器的本地管道。请使用与编辑器相同的 Windows 用户，并保持两者权限级别一致。",
            "PROTOCOL_MISMATCH" => "API 桥接协议不匹配。请安装本插件支持的协议 2 桥接 DLL。",
            "VOICE_NOT_INSTALLED" => "所选二代声库不可用。请在原版编辑器中确认声库安装与授权，再刷新声库列表。",
            "INVALID_PARAMETER" => "语音参数无效，请恢复默认参数或检查声库支持的范围与风格权重。",
            "INVALID_TEXT" => "合成文本必须为 1 至 48000 个 UTF-8 字节，且不能含有 NUL 字符。",
            "INVALID_OUTPUT" => "输出文件必须为绝对路径，扩展名为 .wav。",
            "OUTPUT_EXISTS" or "OUTPUT_RESERVED" => "合成输出路径已存在或被其他任务占用，请使用新的临时 WAV 文件名。",
            "QUEUE_FULL" => "编辑器合成队列已满，请等待部分任务完成后重试。",
            "BUSY" => "编辑器仍有合成任务，请等待任务结束后再恢复引擎。",
            "EDITOR_EXITED" => "编辑器在任务完成前退出。请重新打开编辑器，检查临时输出后再重新生成。",
            "OPERATION_TIMEOUT" => "等待编辑器 API 回复超时，请检查编辑器是否仍正常响应。",
            "SYNTHESIS_TIMEOUT" => "合成超时，本次任务已确认结束。可缩短文本或检查编辑器状态后重试。",
            "CANCELLATION_UNCONFIRMED" => "无法确认本次任务已取消。请先在原版编辑器中确认任务状态，必要时关闭编辑器，避免遗留排队或运行中的任务。",
            "JOB_NOT_FOUND" => "编辑器找不到此任务，请确认 API 状态目录没有被清理。",
            "INVALID_RESPONSE" => "编辑器 API 返回了无法识别的数据，请检查桥接 DLL 版本。",
            "RESPONSE_TOO_LARGE" => "编辑器 API 回复过大，请检查桥接 DLL 及声库配置。",
            _ => "A.I.VOICE2 合成失败，请检查原版编辑器状态及语音参数。"
        };
        return $"{hint} [{error.Code}]" + (string.IsNullOrWhiteSpace(error.Message) ? "" : $" {error.Message}");
    }
}
