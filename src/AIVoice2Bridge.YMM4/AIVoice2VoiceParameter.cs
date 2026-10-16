using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using AIVoice2Bridge.YMM4.Bridge;
using Newtonsoft.Json;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Plugin.Voice;

namespace AIVoice2Bridge.YMM4;

public enum LanguageDictionary
{
    [Display(Name = "标准日语词典")] Standard,
    [Display(Name = "关西词典")] Kansai
}

public sealed class AIVoice2VoiceParameter : VoiceParameterBase
{
    private double speed = 100, pitch = 100, emphasis = 100, volume = 100;
    private LanguageDictionary language;
    private AIVoice2StyleParameter styles = new();

    [Display(Name = "语速", Description = "原厂合成语速；100% 为默认值。", Order = 10)]
    [Range(50d, 400d), DefaultValue(100d), TextBoxSlider("F0", "%", 50, 400, Delay = -1)]
    public double Speed { get => speed; set => Set(ref speed, value); }

    [Display(Name = "音高", Description = "原厂音高参数；100% 为默认值。", Order = 20)]
    [Range(50d, 200d), DefaultValue(100d), TextBoxSlider("F0", "%", 50, 200, Delay = -1)]
    public double Pitch { get => pitch; set => Set(ref pitch, value); }

    [Display(Name = "抑扬", Description = "二代引擎的 emphasis 参数；不保证与一代同数值同效果。", Order = 30)]
    [Range(0d, 200d), DefaultValue(100d), TextBoxSlider("F0", "%", 0, 200, Delay = -1)]
    public double Emphasis { get => emphasis; set => Set(ref emphasis, value); }

    [Display(Name = "引擎音量", Description = "原厂合成增益；独立于 YMM4 音轨音量。", Order = 40)]
    [Range(0d, 200d), DefaultValue(100d), TextBoxSlider("F0", "%", 0, 200, Delay = -1)]
    public double Volume { get => volume; set => Set(ref volume, value); }

    [Display(Name = "语言词典", Order = 50), EnumComboBox]
    public LanguageDictionary Language { get => language; set => Set(ref language, value); }

    [Display(Name = "原生风格", Description = "只显示当前声库支持的标签。多个风格权重之和不得超过100%。全为0时使用默认风格。", AutoGenerateField = true, Order = 60)]
    public AIVoice2StyleParameter Styles { get => styles; set => Set(ref styles, value ?? new()); }

    public AIVoice2VoiceParameter() { SubscribeChildUndoRedoable(styles); }

    public BridgeSynthesisParameters ToBridgeParameters(IEnumerable<string> supportedStyles)
    {
        static void Validate(double value, double min, double max, string name)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new BridgeException(new("INVALID_PARAMETER", $"{name}须在 {min}% 至 {max}% 范围内。"));
        }
        Validate(Speed, 50, 400, "语速"); Validate(Pitch, 50, 200, "音高");
        Validate(Emphasis, 0, 200, "抑扬"); Validate(Volume, 0, 200, "引擎音量");
        if (!Enum.IsDefined(Language)) throw new BridgeException(new("INVALID_PARAMETER", "不支持的语言词典。"));
        return new(Volume / 100, Speed / 100, Pitch / 100, Emphasis / 100, Styles.ToWeights(supportedStyles));
    }
}

public sealed class AIVoice2StyleParameter : VoiceParameterBase
{
    private double j, a, s, c, k, u, v, w, x;
    private string[] availableStyles = [];
    [Browsable(false)]
    public string[] AvailableStyles
    {
        get => availableStyles;
        set => Set(ref availableStyles, value ?? [], nameof(AvailableStyles), nameof(HasJ), nameof(HasA), nameof(HasS),
            nameof(HasC), nameof(HasK), nameof(HasU), nameof(HasV), nameof(HasW), nameof(HasX));
    }
    [Browsable(false), JsonIgnore] public bool HasJ => availableStyles.Contains("J", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasA => availableStyles.Contains("A", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasS => availableStyles.Contains("S", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasC => availableStyles.Contains("C", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasK => availableStyles.Contains("K", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasU => availableStyles.Contains("U", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasV => availableStyles.Contains("V", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasW => availableStyles.Contains("W", StringComparer.Ordinal);
    [Browsable(false), JsonIgnore] public bool HasX => availableStyles.Contains("X", StringComparer.Ordinal);

    [Display(Name = "风格 J", Order = 1), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasJ), true)]
    public double J { get => j; set => Set(ref j, value); }
    [Display(Name = "风格 A", Order = 2), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasA), true)]
    public double A { get => a; set => Set(ref a, value); }
    [Display(Name = "风格 S", Order = 3), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasS), true)]
    public double S { get => s; set => Set(ref s, value); }
    [Display(Name = "风格 C", Order = 4), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasC), true)]
    public double C { get => c; set => Set(ref c, value); }
    [Display(Name = "风格 K", Order = 5), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasK), true)]
    public double K { get => k; set => Set(ref k, value); }
    [Display(Name = "风格 U", Order = 6), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasU), true)]
    public double U { get => u; set => Set(ref u, value); }
    [Display(Name = "风格 V", Order = 7), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasV), true)]
    public double V { get => v; set => Set(ref v, value); }
    [Display(Name = "风格 W", Order = 8), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasW), true)]
    public double W { get => w; set => Set(ref w, value); }
    [Display(Name = "风格 X", Order = 9), Range(0d, 100d), DefaultValue(0d), TextBoxSlider("F0", "%", 0, 100, Delay = -1), ShowPropertyEditorWhen(nameof(HasX), true)]
    public double X { get => x; set => Set(ref x, value); }

    public void AdaptTo(IEnumerable<string> supportedStyles)
    {
        var supported = supportedStyles.ToHashSet(StringComparer.Ordinal);
        if (!supported.Contains("J")) J = 0;
        if (!supported.Contains("A")) A = 0;
        if (!supported.Contains("S")) S = 0;
        if (!supported.Contains("C")) C = 0;
        if (!supported.Contains("K")) K = 0;
        if (!supported.Contains("U")) U = 0;
        if (!supported.Contains("V")) V = 0;
        if (!supported.Contains("W")) W = 0;
        if (!supported.Contains("X")) X = 0;
        AvailableStyles = supported.ToArray();
    }

    public Dictionary<string, double> ToWeights(IEnumerable<string> supportedStyles)
    {
        var supported = supportedStyles.ToHashSet(StringComparer.Ordinal);
        var all = new Dictionary<string, double> { ["J"] = J, ["A"] = A, ["S"] = S, ["C"] = C,
            ["K"] = K, ["U"] = U, ["V"] = V, ["W"] = W, ["X"] = X };
        foreach (var (key, weight) in all)
        {
            if (!double.IsFinite(weight) || weight < 0 || weight > 100)
                throw new BridgeException(new("INVALID_PARAMETER", $"风格 {key} 权重须在 0% 至 100% 范围内。"));
            if (weight > 0 && !supported.Contains(key))
                throw new BridgeException(new("INVALID_PARAMETER", $"该声库不支持风格 {key}。请清除此风格权重。"));
        }
        if (all.Values.Sum() > 100.000001)
            throw new BridgeException(new("INVALID_PARAMETER", "所有风格权重之和不得超过 100%。"));
        return all.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, pair => pair.Value / 100, StringComparer.Ordinal);
    }
}
