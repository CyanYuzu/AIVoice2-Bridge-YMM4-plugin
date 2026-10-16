using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AIVoice2Bridge.YMM4.Bridge;

/// <summary>Connects only to the DLL hosted by an already running original editor.</summary>
public sealed class BridgeClient
{
    private const int MaximumRequestBytes = 60000;
    private const int MaximumResponseBytes = 65536;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false
    };
    private readonly string sessionPath;
    private readonly BridgeClientOptions options;

    public BridgeClient(string? sessionPath = null, BridgeClientOptions? options = null)
    {
        this.sessionPath = sessionPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIVoice2Bridge", "v2", "editor-session.json");
        this.options = options ?? new BridgeClientOptions();
        foreach (TimeSpan timeout in new[] { this.options.ConnectionTimeout, this.options.OperationTimeout,
            this.options.SynthesisTimeout, this.options.CancellationTimeout, this.options.PollInterval })
            if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(options), "API 超时与轮询间隔须为正数且不超过 Int32 毫秒。");
    }

    public Task<BridgeHealth> GetHealthAsync(CancellationToken cancellationToken = default) =>
        CallAsync<BridgeHealth>(new { op = "health" }, cancellationToken);

    public async Task<IReadOnlyList<BridgeVoice>> GetVoicesAsync(CancellationToken cancellationToken = default) =>
        await CallAsync<BridgeVoice[]>(new { op = "voices" }, cancellationToken).ConfigureAwait(false);

    public Task<BridgeParameterCatalog> GetParametersAsync(CancellationToken cancellationToken = default) =>
        CallAsync<BridgeParameterCatalog>(new { op = "parameters" }, cancellationToken);

    public Task<BridgeJob> GetStatusAsync(string id, CancellationToken cancellationToken = default) =>
        CallAsync<BridgeJob>(new { op = "status", id = ValidateId(id) }, cancellationToken);

    public Task<BridgeJob> CancelAsync(string id, CancellationToken cancellationToken = default) =>
        CallAsync<BridgeJob>(new { op = "cancel", id = ValidateId(id) }, cancellationToken);

    public Task<JsonElement> RecoverAsync(CancellationToken cancellationToken = default) =>
        CallAsync<JsonElement>(new { op = "recover" }, cancellationToken);

    /// <summary>Advanced queue API; callers own cancellation and eventual terminal-state observation.</summary>
    public Task<BridgeJob> SubmitAsync(string text, string voiceId, BridgeSynthesisParameters parameters,
        string outputPath, string language = "standard", string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var request = CreateSubmit(text, voiceId, parameters, outputPath, language, requestId ?? NewId());
        return SubmitWithCleanupAsync(request, cancellationToken);
    }

    private async Task<BridgeJob> SubmitWithCleanupAsync(SubmitRequest request, CancellationToken cancellationToken)
    {
        bool submissionAttempted = false;
        try { return await CallAsync<BridgeJob>(request, cancellationToken, () => submissionAttempted = true).ConfigureAwait(false); }
        catch (Exception e) when (submissionAttempted && IsTransportFailure(e))
        {
            // The server may have committed a job even if its acknowledgement was lost.
            await ConfirmStoppedAsync(request.RequestId, e).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<BridgeAudioResult> SynthesizeAsync(string text, string voiceId,
        BridgeSynthesisParameters parameters, string outputPath, string language = "standard",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = CreateSubmit(text, voiceId, parameters, outputPath, language, NewId());
        using var timeout = new CancellationTokenSource(options.SynthesisTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        bool submissionAttempted = false;
        try
        {
            var job = await CallAsync<BridgeJob>(request, linked.Token, () => submissionAttempted = true).ConfigureAwait(false);
            while (!job.IsTerminal)
            {
                await Task.Delay(options.PollInterval, linked.Token).ConfigureAwait(false);
                job = await GetStatusAsync(request.RequestId, linked.Token).ConfigureAwait(false);
            }
            if (job.State == "succeeded" && job.Result is { } result)
            {
                if (!string.Equals(Path.GetFullPath(result.OutputPath), request.OutputPath, StringComparison.OrdinalIgnoreCase))
                    throw new BridgeException(new("INVALID_RESPONSE", "任务返回了与请求不同的 WAV 路径。"), job.Id);
                return result;
            }
            if (job.State == "cancelled")
                throw new OperationCanceledException("本次 A.I.VOICE2 合成任务已取消。", linked.Token);
            throw new BridgeException(job.Error ?? new("INVALID_RESPONSE", $"任务状态：{job.State}"), job.Id);
        }
        catch (Exception e) when (submissionAttempted && IsTransportFailure(e))
        {
            await ConfirmStoppedAsync(request.RequestId, e).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException("本次 A.I.VOICE2 合成任务已确认结束。", e, cancellationToken);
            if (timeout.IsCancellationRequested)
                throw new BridgeException(new("SYNTHESIS_TIMEOUT"), request.RequestId, e);
            throw;
        }
    }

    private static bool IsTransportFailure(Exception exception) => exception is OperationCanceledException
        || exception is BridgeException { Code: "OPERATION_TIMEOUT" or "EDITOR_UNAVAILABLE" or "ACCESS_DENIED" or "INVALID_RESPONSE" };

    private async Task ConfirmStoppedAsync(string id, Exception originalError)
    {
        using var cleanup = new CancellationTokenSource(options.CancellationTimeout);
        Exception? lastError = null;
        try
        {
            while (true)
            {
                try
                {
                    var job = await CancelAsync(id, cleanup.Token).ConfigureAwait(false);
                    if (job.IsTerminal) return;
                    while (!job.IsTerminal)
                    {
                        await Task.Delay(options.PollInterval, cleanup.Token).ConfigureAwait(false);
                        job = await GetStatusAsync(id, cleanup.Token).ConfigureAwait(false);
                    }
                    return;
                }
                catch (BridgeException e) when (e.Code == "JOB_NOT_FOUND")
                {
                    // Submit and cancel use separate server threads. A missing task does not yet prove
                    // that a submit whose acknowledgement was lost will never be committed.
                    lastError = e;
                    await Task.Delay(options.PollInterval, cleanup.Token).ConfigureAwait(false);
                }
                catch (BridgeException e) when (IsTransportFailure(e))
                {
                    lastError = e;
                    await Task.Delay(options.PollInterval, cleanup.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception e) when (e is OperationCanceledException or BridgeException)
        {
            throw new BridgeException(new("CANCELLATION_UNCONFIRMED",
                $"请查询任务 {id}；取消确认失败：{(lastError ?? e).Message}"), id, originalError);
        }
    }

    private async Task<T> CallAsync<T>(object request, CancellationToken cancellationToken, Action? beforeWrite = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        if (bytes.Length > MaximumRequestBytes)
            throw new BridgeException(new("INVALID_TEXT", $"请求大小为 {bytes.Length} 字节，上限为 {MaximumRequestBytes}。"));
        using var operation = new CancellationTokenSource(options.OperationTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operation.Token);
        try
        {
            var session = await DiscoverAsync(linked.Token).ConfigureAwait(false);
            using var pipe = new NamedPipeClientStream(".", $"AIVoice2Bridge2-{session.Pid}",
                PipeDirection.InOut, PipeOptions.Asynchronous);
            using (var connection = CancellationTokenSource.CreateLinkedTokenSource(linked.Token))
            {
                connection.CancelAfter(options.ConnectionTimeout);
                try { await pipe.ConnectAsync(connection.Token).ConfigureAwait(false); }
                catch (OperationCanceledException e) when (!linked.IsCancellationRequested)
                { throw new BridgeException(new("EDITOR_UNAVAILABLE", "连接本地命名管道超时。"), innerException: e); }
            }
            pipe.ReadMode = PipeTransmissionMode.Message;
            // One WriteAsync produces exactly one message. Do not split the JSON across writes.
            beforeWrite?.Invoke();
            await pipe.WriteAsync(bytes, linked.Token).ConfigureAwait(false);
            await pipe.FlushAsync(linked.Token).ConfigureAwait(false);
            using var response = new MemoryStream();
            byte[] buffer = new byte[4096];
            do
            {
                int count = await pipe.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
                if (count == 0) throw new BridgeException(new("INVALID_RESPONSE", "管道在回复完成前关闭。"));
                if (response.Length + count > MaximumResponseBytes)
                    throw new BridgeException(new("RESPONSE_TOO_LARGE"));
                response.Write(buffer, 0, count);
            } while (!pipe.IsMessageComplete);
            using var document = JsonDocument.Parse(response.GetBuffer().AsMemory(0, (int)response.Length));
            var envelope = document.RootElement;
            if (!envelope.TryGetProperty("ok", out var ok) || ok.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new BridgeException(new("INVALID_RESPONSE", "缺少布尔类型的 ok 字段。"));
            if (!ok.GetBoolean())
            {
                BridgeError? error = envelope.TryGetProperty("error", out var details)
                    ? details.Deserialize<BridgeError>(JsonOptions) : null;
                throw new BridgeException(error ?? new("INVALID_RESPONSE", "缺少 error 字段。"));
            }
            if (!envelope.TryGetProperty("result", out var value))
                throw new BridgeException(new("INVALID_RESPONSE", "缺少 result 字段。"));
            if (typeof(T) == typeof(JsonElement)) return (T)(object)value.Clone();
            return value.Deserialize<T>(JsonOptions) ?? throw new BridgeException(new("INVALID_RESPONSE", "result 为空。"));
        }
        catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
        { throw new BridgeException(new("OPERATION_TIMEOUT"), innerException: e); }
        catch (UnauthorizedAccessException e)
        { throw new BridgeException(new("ACCESS_DENIED", e.Message), innerException: e); }
        catch (IOException e)
        { throw new BridgeException(new("EDITOR_UNAVAILABLE", e.Message), innerException: e); }
        catch (JsonException e)
        { throw new BridgeException(new("INVALID_RESPONSE", e.Message), innerException: e); }
    }

    private async Task<EditorSession> DiscoverAsync(CancellationToken cancellationToken)
    {
        using var file = new FileStream(sessionPath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (file.Length > 16384) throw new BridgeException(new("PROTOCOL_MISMATCH", "发现文件过大。"));
        var session = await JsonSerializer.DeserializeAsync<EditorSession>(file, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (session is null || session.Protocol != 2 || session.Pid <= 0
            || session.Deployment != "editor-drop-in-dll"
            || session.Pipe != $@"\\.\pipe\AIVoice2Bridge2-{session.Pid}")
            throw new BridgeException(new("PROTOCOL_MISMATCH", "发现文件与 DLL 协议不匹配。"));
        return session;
    }

    private static SubmitRequest CreateSubmit(string text, string voiceId, BridgeSynthesisParameters parameters,
        string outputPath, string language, string id)
    {
        if (string.IsNullOrWhiteSpace(text) || text.IndexOf('\0') >= 0 || Encoding.UTF8.GetByteCount(text) > 48000)
            throw new BridgeException(new("INVALID_TEXT"));
        if (string.IsNullOrWhiteSpace(voiceId)) throw new BridgeException(new("VOICE_NOT_INSTALLED"));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (language is not ("standard" or "kansai")) throw new BridgeException(new("INVALID_LANGUAGE"));
        if (string.IsNullOrWhiteSpace(outputPath) || !Path.IsPathFullyQualified(outputPath)
            || !string.Equals(Path.GetExtension(outputPath), ".wav", StringComparison.Ordinal))
            throw new BridgeException(new("INVALID_OUTPUT"));
        outputPath = Path.GetFullPath(outputPath);
        // Existence and reservation are checked atomically by the server after request-id
        // deduplication. A local existence check would break idempotent retries of finished jobs.
        var styles = parameters.Styles is null ? new Dictionary<string, double>() :
            parameters.Styles.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (new[] { parameters.Volume, parameters.Rate, parameters.Pitch, parameters.Emphasis }.Any(v => !double.IsFinite(v))
            || styles.Any(p => string.IsNullOrEmpty(p.Key) || !double.IsFinite(p.Value) || p.Value < 0 || p.Value > 1)
            || styles.Values.Sum() > 1 + 1e-9)
            throw new BridgeException(new("INVALID_PARAMETER", "数值必须有限，风格权重须在 0 至 1 内且总和不超过 1。"));
        return new("submit", ValidateId(id), text, voiceId, language, outputPath, parameters with { Styles = styles });
    }

    private static string NewId() => "ymm4_" + Guid.NewGuid().ToString("N");
    private static string ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 100 || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')))
            throw new ArgumentException("任务 ID 须为 1 至 100 个 ASCII 字母、数字、下划线或连字符。", nameof(id));
        return id;
    }

    private sealed record EditorSession(
        [property: JsonPropertyName("pid")] int Pid,
        [property: JsonPropertyName("protocol")] int Protocol,
        [property: JsonPropertyName("deployment")] string Deployment,
        [property: JsonPropertyName("pipe")] string Pipe);

    private sealed record SubmitRequest(
        [property: JsonPropertyName("op")] string Op,
        [property: JsonPropertyName("request_id")] string RequestId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("voice")] string Voice,
        [property: JsonPropertyName("language")] string Language,
        [property: JsonPropertyName("output_path")] string OutputPath,
        [property: JsonPropertyName("parameters")] BridgeSynthesisParameters Parameters);
}
