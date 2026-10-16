using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIVoice2Bridge.YMM4.Bridge;

public sealed record BridgeVoice(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("sample_rate")] int SampleRate,
    [property: JsonPropertyName("styles")] IReadOnlyList<string>? Styles,
    [property: JsonPropertyName("available")] bool Available,
    [property: JsonPropertyName("error")] BridgeError? Error = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

public sealed record BridgeSynthesisParameters(
    [property: JsonPropertyName("volume")] double Volume = 1,
    [property: JsonPropertyName("rate")] double Rate = 1,
    [property: JsonPropertyName("pitch")] double Pitch = 1,
    [property: JsonPropertyName("emphasis")] double Emphasis = 1,
    [property: JsonPropertyName("styles")] IReadOnlyDictionary<string, double>? Styles = null);

public sealed record BridgeParameterBounds(
    [property: JsonPropertyName("min")] double? Min,
    [property: JsonPropertyName("max")] double? Max);

public sealed record BridgeParameterCatalog(
    [property: JsonPropertyName("native_bounds")] IReadOnlyDictionary<string, BridgeParameterBounds> NativeBounds,
    [property: JsonPropertyName("defaults")] BridgeSynthesisParameters Defaults);

public sealed record BridgeHealth(
    [property: JsonPropertyName("pid")] int Pid,
    [property: JsonPropertyName("protocol")] int Protocol,
    [property: JsonPropertyName("deployment")] string Deployment,
    [property: JsonPropertyName("backend")] string Backend,
    [property: JsonPropertyName("ready")] bool Ready,
    [property: JsonPropertyName("busy")] bool Busy,
    [property: JsonPropertyName("queued")] int Queued,
    [property: JsonPropertyName("fault")] BridgeError? Fault,
    [property: JsonPropertyName("child_processes_started")] int ChildProcessesStarted);

public sealed record BridgeError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("stage")] string? Stage = null,
    [property: JsonPropertyName("native_code")] int? NativeCode = null);

public sealed record BridgeJob(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("output")] string Output,
    [property: JsonPropertyName("created")] double Created,
    [property: JsonPropertyName("started")] double? Started,
    [property: JsonPropertyName("finished")] double? Finished,
    [property: JsonPropertyName("result")] BridgeAudioResult? Result,
    [property: JsonPropertyName("error")] BridgeError? Error,
    [property: JsonPropertyName("request")] JsonElement Request)
{
    public bool IsTerminal => State is "succeeded" or "failed" or "cancelled" or "interrupted";
}

public sealed record BridgeAudioResult(
    [property: JsonPropertyName("output_path")] string OutputPath,
    [property: JsonPropertyName("sample_rate")] int SampleRate,
    [property: JsonPropertyName("channels")] int Channels,
    [property: JsonPropertyName("sample_width")] int SampleWidth,
    [property: JsonPropertyName("pcm_bytes")] long PcmBytes,
    [property: JsonPropertyName("frames")] long Frames,
    [property: JsonPropertyName("file_bytes")] long FileBytes,
    [property: JsonPropertyName("duration_seconds")] double DurationSeconds,
    [property: JsonPropertyName("elapsed_seconds")] double ElapsedSeconds,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("parameters")] BridgeSynthesisParameters Parameters);

public sealed record BridgeClientOptions
{
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan SynthesisTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan CancellationTimeout { get; init; } = TimeSpan.FromSeconds(15);
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(100);
}
