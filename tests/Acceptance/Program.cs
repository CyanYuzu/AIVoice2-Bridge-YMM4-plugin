using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIVoice2Bridge.YMM4;
using AIVoice2Bridge.YMM4.Bridge;
using AIVoice2Bridge.YMM4.Views;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Plugin.Voice;

internal static class Program
{
    private static readonly List<object> Checks = [];
    private static readonly List<object> Audio = [];
    private static string root = "", run = "";
    private static object? batch;
    private const string Text = "こんにちは。音声合成のテストです。";

    [STAThread]
    private static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        var ymm = Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(ymm, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        // The portable SDK test host keeps settings beside itself, never in the user's real YMM4 folder.
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "user"));
        run = Path.Combine(root, "evidence", "acceptance-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(run);
        try
        {
            if (args.Contains("--cache-only"))
            {
                var cache = new AIVoice2VoicePlugin();
                Check("voice cache survives test-host process restart", cache.IsVoicesCached && cache.Voices.Count() == 8);
                Save("cache-restart.json"); return 0;
            }
            if (args.Contains("--render-only")) { CheckControls(); Save("ui-validation.json"); return 0; }
            RunAsync().GetAwaiter().GetResult();
            CheckControls();
            Save("acceptance.json"); return 0;
        }
        catch (Exception error)
        {
            Checks.Add(new { name = "unexpected exception", passed = false, detail = error.ToString() });
            Save("acceptance.json"); Console.Error.WriteLine(error); return 1;
        }
    }

    private static void Check(string name, bool passed, object? detail = null)
    {
        Checks.Add(new { name, passed, detail }); Console.WriteLine($"{name}: {passed}");
        if (!passed) throw new InvalidOperationException(name);
    }
    private static void Save(string name)
    {
        var report = new { at = DateTimeOffset.Now, run_directory = run, checks = Checks, audio = Audio, batch,
            sdk = typeof(IVoicePlugin).Assembly.Location, plugin = typeof(AIVoice2VoicePlugin).Assembly.Location,
            settings_directory = YukkuriMovieMaker.Commons.AppDirectories.SettingDirectory };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(run, "report.json"), json);
        File.WriteAllText(Path.Combine(root, "evidence", name), json);
    }
    private static string Output(string name) => Path.Combine(run, name);
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static double Duration(string path) => (new FileInfo(path).Length - 44) / 96000d;
    private static async Task<string> RenderAsync(IVoiceSpeaker voice, string name, AIVoice2VoiceParameter? parameter = null)
    {
        var output = Output(name);
        await voice.CreateVoiceAsync(Text, null, parameter ?? voice.CreateVoiceParameter(), output);
        var bytes = await File.ReadAllBytesAsync(output);
        Check(name + " real SDK synthesis WAV", bytes.Length > 44 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24)) == 48000
            && BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(22)) == 1
            && BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(34)) == 16);
        Audio.Add(new { name, voice_id = voice.ID, output, sha256 = Hash(output), duration_seconds = Duration(output) });
        return output;
    }
    private static async Task RunAsync()
    {
        var client = new BridgeClient();
        var deadline = DateTime.UtcNow.AddSeconds(90);
        BridgeHealth? health = null;
        while (DateTime.UtcNow < deadline)
        {
            try { health = await client.GetHealthAsync(); if (health.Ready) break; } catch (BridgeException) { }
            await Task.Delay(300);
        }
        Check("original Editor protocol 2 ready", health is { Ready: true, Protocol: 2, Fault: null });
        Check("SDK settings isolated inside workspace", YukkuriMovieMaker.Commons.AppDirectories.SettingDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        var plugin = new AIVoice2VoicePlugin();
        await plugin.UpdateVoicesAsync();
        var voices = plugin.Voices.ToArray();
        Check("native YMM4 IVoicePlugin contract", plugin is IVoicePlugin && plugin.CanUpdateVoices);
        Check("eight licensed voices enumerated", voices.Length == 8 && plugin.IsVoicesCached);
        Check("voice IDs stable and unique", voices.Select(v => v.ID).Distinct().Count() == voices.Length
            && voices.All(v => v.IsMatch(AIVoice2VoiceSpeaker.StableApi, v.ID)));
        var voice = voices.Single(v => v.ID == "YuzukiYukari_ns_48");
        var original = (AIVoice2VoiceParameter)voice.CreateVoiceParameter();
        var baseline = await RenderAsync(voice, "baseline.wav", original);
        var repeat = await RenderAsync(voice, "repeat-without-extension", original);
        Check("YMM4 extensionless output supported and deterministic", Hash(baseline) == Hash(repeat));
        foreach (var other in voices.Where(v => v.ID != voice.ID)) await RenderAsync(other, other.ID + ".wav");
        original.Speed = 140;
        var faster = await RenderAsync(voice, "faster.wav", original);
        Check("percentage speed mapped to native rate", Duration(faster) < Duration(baseline) * .85);
        original.Speed = 100; original.Pitch = 120;
        Check("pitch control changes waveform", Hash(await RenderAsync(voice, "pitch.wav", original)) != Hash(baseline));
        original.Pitch = 100; original.Emphasis = 130;
        Check("emphasis control changes waveform", Hash(await RenderAsync(voice, "emphasis.wav", original)) != Hash(baseline));
        original.Emphasis = 100; original.Volume = 50;
        Check("engine volume changes waveform", Hash(await RenderAsync(voice, "volume.wav", original)) != Hash(baseline));
        original.Volume = 100; original.Styles.J = 60; original.Styles.S = 20;
        Check("multiple native styles change waveform", Hash(await RenderAsync(voice, "style-mix.wav", original)) != Hash(baseline));
        var clone = (AIVoice2VoiceParameter)((IVoiceParameter)original).Clone()!;
        Check("SDK project parameter clone retains nested style", clone.Speed == 100 && clone.Styles.J == 60 && clone.Styles.S == 20 && clone.Styles.HasJ);
        var undo = false; clone.UndoRedoCommandCreated += (_, _) => undo = true; clone.Styles.J = 50;
        Check("nested style changes propagate undo events", undo);
        var simple = new AIVoice2VoiceSpeaker(new VoiceCacheEntry { Id = "test", Name = "test", Styles = ["J"], Available = true });
        simple.MigrateParameter(clone);
        Check("voice switching retains common style and clears unsupported style", clone.Styles.J == 50 && clone.Styles.S == 0 && !clone.Styles.HasS);
        original.Styles.J = 0; original.Styles.S = 0;
        Check("parameters reset between jobs", Hash(await RenderAsync(voice, "reset.wav", original)) == Hash(baseline));
        original.Language = LanguageDictionary.Kansai; await RenderAsync(voice, "kansai.wav", original); original.Language = LanguageDictionary.Standard;
        original.Speed = 1000;
        Check("YMM4 SDK constrains speed to declared range", original.Speed is >= 50 and <= 400, original.Speed);
        original.Speed = 100; original.Styles.J = 80; original.Styles.S = 30;
        try { original.ToBridgeParameters(["J", "S"]); Check("invalid style sum rejected", false); }
        catch (BridgeException e) { Check("invalid style sum rejected", e.Code == "INVALID_PARAMETER"); }
        original.Styles.J = original.Styles.S = 0;
        try { await client.SynthesizeAsync(Text, voice.ID, new(Rate: 100), Output("native-error.wav")); Check("native error surfaced", false); }
        catch (BridgeException e) { Check("native error surfaced", e.Code == "ENGINE_ERROR" && e.Error.Stage == "ai_TalkerConfig_setRate"); }
        Check("native engine recovers for next YMM4 call", Hash(await RenderAsync(voice, "after-native-error.wav", original)) == Hash(baseline));
        var protectedPath = Output("protected.wav"); await File.WriteAllTextAsync(protectedPath, "preserve");
        try { await voice.CreateVoiceAsync(Text, null, original, protectedPath); Check("existing output preserved", false); }
        catch (IOException) { Check("existing output preserved", await File.ReadAllTextAsync(protectedPath) == "preserve"); }
        var cancelFile = Output("cancel.wav"); using (var cancel = new CancellationTokenSource())
        {
            var longTask = ((AIVoice2VoiceSpeaker)voice).CreateVoiceAsync(string.Concat(Enumerable.Repeat(Text, 100)), original, cancelFile, cancel.Token);
            await Task.Delay(300); cancel.Cancel();
            try { await longTask; Check("scoped cancellation", false); }
            catch (OperationCanceledException) { Check("scoped cancellation", !File.Exists(cancelFile)); }
        }
        var timeoutClient = new BridgeClient(options: new BridgeClientOptions { SynthesisTimeout = TimeSpan.FromMilliseconds(200) });
        try { await timeoutClient.SynthesizeAsync(string.Concat(Enumerable.Repeat(Text, 100)), voice.ID, new(), Output("timeout.wav")); Check("timeout task confirmed stopped", false); }
        catch (BridgeException e) { Check("timeout task confirmed stopped", e.Code == "SYNTHESIS_TIMEOUT", new { e.Code, e.JobId }); }
        Check("cancellation cleans partial files", !Directory.EnumerateFiles(run, "*.partial").Any());
        var clock = Stopwatch.StartNew();
        var batchFiles = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => RenderAsync(voice, $"batch-{i}.wav")));
        clock.Stop();
        Check("concurrent SDK calls serialize deterministically", batchFiles.All(p => Hash(p) == Hash(baseline)));
        batch = new { count = batchFiles.Length, wall_seconds = clock.Elapsed.TotalSeconds,
            audio_seconds = batchFiles.Sum(Duration), real_time_factor = clock.Elapsed.TotalSeconds / batchFiles.Sum(Duration) };
        Check("all successful plugin scratch outputs committed", !Directory.EnumerateFiles(run, ".aivoice2-*").Any());
        await AIVoice2Settings.Default.RecoverAsync();
        Check("explicit recovery preserves waveform", Hash(await RenderAsync(voice, "after-recover.wav")) == Hash(baseline));
        try { await new BridgeClient(Output("missing-session.json")).GetHealthAsync(); Check("missing Editor actionable error", false); }
        catch (BridgeException e) { Check("missing Editor actionable error", e.Code == "EDITOR_UNAVAILABLE"); }
        var invalidSession = Output("invalid-session.json"); await File.WriteAllTextAsync(invalidSession, "{\"protocol\":1}");
        try { await new BridgeClient(invalidSession).GetHealthAsync(); Check("incompatible protocol refused", false); }
        catch (BridgeException e) { Check("incompatible protocol refused", e.Code == "PROTOCOL_MISMATCH"); }
    }

    private static void CheckControls()
    {
        var settings = AIVoice2Settings.Default;
        var view = new AIVoice2SettingsView(settings) { Background = SystemColors.WindowBrush };
        view.Measure(new Size(850, 1100)); view.Arrange(new Rect(0, 0, 850, 1100)); view.UpdateLayout();
        var image = new RenderTargetBitmap(850, 1100, 96, 96, PixelFormats.Pbgra32); image.Render(view);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
        using (var file = File.Create(Path.Combine(run, "settings-view.png"))) png.Save(file);
        Check("WPF settings controls instantiate and render on STA", view.Content is ScrollViewer);
        var styles = new AIVoice2StyleParameter { AvailableStyles = ["J"] };
        var show = typeof(AIVoice2StyleParameter).GetProperty(nameof(styles.J))!.GetCustomAttribute<ShowPropertyEditorWhenAttribute>()!;
        var field = new TextBlock(); BindingOperations.SetBinding(field, UIElement.VisibilityProperty, show.GetBinding(styles));
        Check("per-voice style condition shows supported key", field.Visibility == Visibility.Visible);
        styles.AvailableStyles = [];
        Check("per-voice style condition hides unavailable key", field.Visibility == Visibility.Collapsed);
    }
}
