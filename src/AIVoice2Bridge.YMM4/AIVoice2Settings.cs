using System.ComponentModel;
using System.IO;
using AIVoice2Bridge.YMM4.Bridge;
using AIVoice2Bridge.YMM4.Views;
using Newtonsoft.Json;
using YukkuriMovieMaker.Plugin;

namespace AIVoice2Bridge.YMM4;

public sealed record VoiceCacheEntry
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string[] Styles { get; init; } = [];
    public int SampleRate { get; init; }
    public bool Available { get; init; }
    [JsonIgnore] public string StyleSummary => string.Join(", ", Styles);
}

public sealed class AIVoice2Settings : SettingsBase<AIVoice2Settings>
{
    private string sessionPath = "";
    private int synthesisTimeoutSeconds = 600;
    private bool verifyOutputHash = true;
    private VoiceCacheEntry[] cachedVoices = [];
    private string lastStatus = "请正常打开已安装桥接 DLL 的 A.I.VOICE2 Editor，然后更新声库列表。";
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public override SettingsCategory Category => SettingsCategory.Voice;
    public override string Name => "A.I.VOICE2 Bridge";
    public override bool HasSettingView => true;
    public override object SettingView => new AIVoice2SettingsView(this);

    public string SessionPath { get => sessionPath; set => Set(ref sessionPath, value?.Trim() ?? ""); }
    public int SynthesisTimeoutSeconds
    {
        get => synthesisTimeoutSeconds;
        set
        {
            if (value is < 10 or > 3600) throw new ArgumentOutOfRangeException(nameof(value), "合成超时须为 10 至 3600 秒。");
            Set(ref synthesisTimeoutSeconds, value);
        }
    }
    public bool VerifyOutputHash { get => verifyOutputHash; set => Set(ref verifyOutputHash, value); }
    public VoiceCacheEntry[] CachedVoices { get => cachedVoices; set => Set(ref cachedVoices, value ?? []); }
    [JsonIgnore, Browsable(false)]
    public string LastStatus { get => lastStatus; set => Set(ref lastStatus, value); }

    public override void Initialize()
    {
        synthesisTimeoutSeconds = Math.Clamp(synthesisTimeoutSeconds, 10, 3600);
        cachedVoices = (cachedVoices ?? []).Where(v => !string.IsNullOrWhiteSpace(v.Id))
            .DistinctBy(v => v.Id, StringComparer.Ordinal).ToArray();
    }

    public BridgeClient CreateClient() => new(
        string.IsNullOrWhiteSpace(SessionPath) ? null : Path.GetFullPath(SessionPath),
        new BridgeClientOptions { SynthesisTimeout = TimeSpan.FromSeconds(SynthesisTimeoutSeconds) });

    public async Task<BridgeHealth> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await CreateClient().GetHealthAsync(cancellationToken).ConfigureAwait(false);
            if (!health.Ready) throw new BridgeException(health.Fault ?? new("EDITOR_NOT_READY"));
            LastStatus = $"已连接原版 Editor（PID {health.Pid}），队列 {health.Queued}，协议 {health.Protocol}。";
            return health;
        }
        catch (Exception e) { LastStatus = e.Message; throw; }
    }

    public async Task RefreshVoicesAsync(CancellationToken cancellationToken = default)
    {
        await RefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var client = CreateClient();
            var health = await client.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            if (!health.Ready) throw new BridgeException(health.Fault ?? new("EDITOR_NOT_READY"));
            var voices = await client.GetVoicesAsync(cancellationToken).ConfigureAwait(false);
            if (!voices.Any(v => v.Available))
                throw new BridgeException(new("VOICE_NOT_INSTALLED", "原版编辑器没有返回可用的已授权声库。"));
            CachedVoices = voices.Select(v => new VoiceCacheEntry
            {
                Id = v.Id, Name = v.DisplayName, Styles = (v.Styles ?? []).Distinct(StringComparer.Ordinal).ToArray(),
                SampleRate = v.SampleRate, Available = v.Available
            }).OrderBy(v => v.Name, StringComparer.CurrentCulture).ToArray();
            Save();
            LastStatus = $"已更新 {CachedVoices.Count(v => v.Available)} 个可用声库（Editor PID {health.Pid}）。";
        }
        catch (Exception e) { LastStatus = e.Message; throw; }
        finally { RefreshLock.Release(); }
    }

    public async Task RecoverAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await CreateClient().RecoverAsync(cancellationToken).ConfigureAwait(false);
            LastStatus = "桥接器的独立合成上下文已恢复。";
        }
        catch (Exception e) { LastStatus = e.Message; throw; }
    }
}
