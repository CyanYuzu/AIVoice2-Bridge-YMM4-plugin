using AIVoice2Bridge.YMM4.Bridge;
using System.IO;
using YukkuriMovieMaker.Plugin.Voice;

namespace AIVoice2Bridge.YMM4;

public sealed class AIVoice2VoicePlugin : IVoicePlugin
{
    public const string Engine = "A.I.VOICE2 Bridge";
    public string Name => Engine;
    public IEnumerable<IVoiceSpeaker> Voices => AIVoice2Settings.Default.CachedVoices.Where(v => v.Available)
        .Select(v => new AIVoice2VoiceSpeaker(v)).OrderBy(v => v.SpeakerName, StringComparer.CurrentCulture).ToArray();
    public bool CanUpdateVoices => true;
    public bool IsVoicesCached => AIVoice2Settings.Default.CachedVoices.Any(v => v.Available);
    public Task UpdateVoicesAsync() => AIVoice2Settings.Default.RefreshVoicesAsync();
}

public sealed class AIVoice2VoiceSpeaker(VoiceCacheEntry voice) : IVoiceSpeaker
{
    public const string StableApi = "AIVoice2Bridge.Protocol2";
    private static readonly SemaphoreSlim SynthesisLock = new(1, 1);
    public string EngineName => AIVoice2VoicePlugin.Engine;
    public string SpeakerName => voice.Name;
    public string API => StableApi;
    public string ID => voice.Id;
    public bool IsVoiceDataCachingRequired => false;
    public SupportedTextFormat Format => SupportedTextFormat.Text;
    public IVoiceLicense? License => null;
    public IVoiceResource? Resource => null;
    public bool IsMatch(string api, string id) => api == StableApi && id == ID;
    public IReadOnlyList<string> AvailableStyles => voice.Styles;

    public IVoiceParameter CreateVoiceParameter() => new AIVoice2VoiceParameter
    {
        Styles = new AIVoice2StyleParameter { AvailableStyles = voice.Styles.ToArray() }
    };

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter)
    {
        if (currentParameter is not AIVoice2VoiceParameter parameter) return CreateVoiceParameter();
        parameter.Styles.AdaptTo(voice.Styles);
        return parameter;
    }

    public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter) =>
        Task.FromException<string>(new NotSupportedException("当前桥接 API 接收原文，不提供读音或逐音素编辑接口。"));

    public Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoicePronounce? pronounce,
        IVoiceParameter? parameter, string filePath) => CreateVoiceCoreAsync(text, parameter, filePath, CancellationToken.None);

    // The current YMM4 interface has no CancellationToken; this overload supports controlled integration tests and future hosts.
    public Task<IVoicePronounce?> CreateVoiceAsync(string text, IVoiceParameter parameter, string filePath,
        CancellationToken cancellationToken) => CreateVoiceCoreAsync(text, parameter, filePath, cancellationToken);

    private async Task<IVoicePronounce?> CreateVoiceCoreAsync(string text, IVoiceParameter? parameter,
        string filePath, CancellationToken cancellationToken)
    {
        if (parameter is not AIVoice2VoiceParameter current)
            throw new ArgumentException("请为 A.I.VOICE2 声库重新选择匹配的语音参数。", nameof(parameter));
        // Snapshot all parameters before yielding, so edits cannot mutate an admitted task.
        var request = current.ToBridgeParameters(voice.Styles);
        var language = current.Language == LanguageDictionary.Kansai ? "kansai" : "standard";
        var settings = AIVoice2Settings.Default;
        var client = settings.CreateClient();
        var verifyHash = settings.VerifyOutputHash;
        var destination = Path.GetFullPath(filePath);
        if (File.Exists(destination)) throw new IOException("YMM4 指定的目标文件已存在，插件不会覆盖该文件。" );
        var folder = Path.GetDirectoryName(destination) ?? throw new ArgumentException("无效的输出目录。", nameof(filePath));
        Directory.CreateDirectory(folder);
        var scratch = Path.Combine(folder, ".aivoice2-" + Guid.NewGuid().ToString("N") + ".wav");
        await SynthesisLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var preserveScratch = false;
        try
        {
            var result = await client.SynthesizeAsync(text, ID, request, scratch, language, cancellationToken).ConfigureAwait(false);
            await WaveValidation.ValidateAsync(scratch, result, verifyHash, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(scratch, destination, overwrite: false);
            // There are no verified phoneme timings in the DLL API. Let YMM4 use its normal waveform handling.
            return null;
        }
        catch (BridgeException e) when (e.Code == "CANCELLATION_UNCONFIRMED")
        {
            // A surviving engine task may still own this file. Preserve it for diagnosis and avoid racing the original callback.
            preserveScratch = true;
            throw;
        }
        finally
        {
            SynthesisLock.Release();
            // Only a completed WAV is removed. Partial files belong to the engine's callback and restart recovery.
            // Cancelled/unconfirmed jobs must never race a callback by deleting .partial here.
            if (!preserveScratch && File.Exists(scratch))
            {
                try { File.Delete(scratch); } catch (IOException) { }
            }
        }
    }
}
