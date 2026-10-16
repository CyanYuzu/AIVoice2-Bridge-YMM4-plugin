using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AIVoice2Bridge.YMM4.Bridge;

namespace AIVoice2Bridge.YMM4.Views;

public sealed class AIVoice2SettingsView : UserControl
{
    private readonly AIVoice2Settings settings;
    private readonly List<Button> actionButtons = new();
    private readonly TextBox timeoutInput;
    private CancellationTokenSource? actionCancellation;
    private bool isWorking;

    public AIVoice2SettingsView(AIVoice2Settings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        DataContext = settings;
        MinWidth = 360;

        var panel = new StackPanel { Margin = new Thickness(12) };
        panel.Children.Add(Paragraph("请正常启动已安装 API 桥接 DLL 的原版 A.I.VOICE2 Editor，完成声库与授权加载后检查连接。此插件直接使用编辑器进程中的合成接口。", 0));

        panel.Children.Add(LabelFor("API 发现文件（留空自动发现）"));
        var sessionInput = new TextBox
        {
            Margin = new Thickness(0, 0, 0, 4),
            MinWidth = 300,
            ToolTip = @"默认：%LOCALAPPDATA%\AIVoice2Bridge\v2\editor-session.json"
        };
        sessionInput.SetBinding(TextBox.TextProperty, EditBinding(nameof(AIVoice2Settings.SessionPath)));
        panel.Children.Add(sessionInput);
        panel.Children.Add(Paragraph(@"默认读取 %LOCALAPPDATA%\AIVoice2Bridge\v2\editor-session.json。通常无需填写路径。", 0));

        panel.Children.Add(LabelFor("单条语音合成超时（秒，10 至 3600）"));
        timeoutInput = new TextBox
        {
            Width = 110,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var timeoutBinding = EditBinding(nameof(AIVoice2Settings.SynthesisTimeoutSeconds));
        timeoutBinding.ValidationRules.Add(new TimeoutRule());
        timeoutInput.SetBinding(TextBox.TextProperty, timeoutBinding);
        panel.Children.Add(timeoutInput);

        var verify = new CheckBox
        {
            Content = "校验输出 WAV 的 SHA-256",
            Margin = new Thickness(0, 0, 0, 8),
            ToolTip = "比较 WAV 文件与编辑器返回的哈希值，确认读取的是本次完整输出。"
        };
        verify.SetBinding(CheckBox.IsCheckedProperty, EditBinding(nameof(AIVoice2Settings.VerifyOutputHash)));
        panel.Children.Add(verify);

        var actions = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        AddAction(actions, "检查连接", async token =>
        {
            BridgeHealth health = await settings.CheckConnectionAsync(token);
            settings.LastStatus = health.Ready
                ? $"编辑器已就绪（PID {health.Pid}），排队 {health.Queued} 项。" + (health.Busy ? "正在合成。" : "当前空闲。")
                : "编辑器 API 尚未就绪，请等待原版编辑器完成启动与授权加载。"
                  + (health.Fault is null ? "" : $" [{health.Fault.Code}] {health.Fault.Message}");
        });
        AddAction(actions, "更新声库", async token =>
        {
            await settings.RefreshVoicesAsync(token);
            int available = settings.CachedVoices.Count(voice => voice.Available);
            settings.LastStatus = $"声库列表已更新：{available} 个可用，共 {settings.CachedVoices.Length} 个。";
        });
        AddAction(actions, "空闲时恢复引擎", async token =>
        {
            await settings.RecoverAsync(token);
            settings.LastStatus = "桥接合成引擎已恢复，可以重新生成语音。";
        });
        panel.Children.Add(actions);
        panel.Children.Add(Paragraph("恢复仅重建桥接器自己的合成上下文；有活动任务时请先等待任务结束。", 0));

        panel.Children.Add(LabelFor("声库"));
        var voices = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 130,
            MaxHeight = 260,
            Margin = new Thickness(0, 0, 0, 8)
        };
        voices.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(AIVoice2Settings.CachedVoices)));
        voices.Columns.Add(new DataGridTextColumn { Header = "名称", Binding = new Binding("Name"), MinWidth = 90 });
        voices.Columns.Add(new DataGridTextColumn { Header = "声库 ID", Binding = new Binding("Id"), MinWidth = 150 });
        voices.Columns.Add(new DataGridTextColumn
        {
            Header = "风格",
            Binding = new Binding("Styles") { Converter = new StylesConverter() },
            MinWidth = 80
        });
        voices.Columns.Add(new DataGridTextColumn { Header = "采样率 / Hz", Binding = new Binding("SampleRate"), MinWidth = 80 });
        voices.Columns.Add(new DataGridCheckBoxColumn { Header = "可用", Binding = new Binding("Available"), MinWidth = 45 });
        panel.Children.Add(voices);

        panel.Children.Add(LabelFor("连接状态"));
        var status = Paragraph("", 0);
        status.SetBinding(TextBlock.TextProperty, new Binding(nameof(AIVoice2Settings.LastStatus)));
        panel.Children.Add(status);

        Content = new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Unloaded += (_, _) => actionCancellation?.Cancel();
    }

    private void AddAction(Panel panel, string caption, Func<CancellationToken, Task> action)
    {
        var button = new Button
        {
            Content = caption,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 4),
            MinHeight = 28
        };
        actionButtons.Add(button);
        panel.Children.Add(button);
        button.Click += async (_, _) =>
        {
            if (isWorking) return;
            timeoutInput.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (Validation.GetHasError(timeoutInput))
            {
                settings.LastStatus = "请输入 10 至 3600 之间的整数作为合成超时秒数。";
                return;
            }
            isWorking = true;
            foreach (var item in actionButtons) item.IsEnabled = false;
            using var cancellation = new CancellationTokenSource();
            actionCancellation = cancellation;
            settings.LastStatus = $"正在{caption}…";
            try { await action(cancellation.Token); }
            catch (OperationCanceledException) { settings.LastStatus = "操作已取消。"; }
            catch (BridgeException error) { settings.LastStatus = error.Message; }
            catch (Exception error) { settings.LastStatus = $"操作失败：{error.Message}"; }
            finally
            {
                actionCancellation = null;
                isWorking = false;
                foreach (var item in actionButtons) item.IsEnabled = true;
            }
        };
    }

    private static Binding EditBinding(string property) => new(property)
    {
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        ValidatesOnExceptions = true,
        NotifyOnValidationError = true
    };

    private static TextBlock LabelFor(string text) => new()
    {
        Text = text,
        Margin = new Thickness(0, 10, 0, 5),
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap
    };

    private static TextBlock Paragraph(string text, double topMargin) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, topMargin, 0, 5)
    };

    private sealed class TimeoutRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            int.TryParse(value?.ToString(), NumberStyles.Integer, cultureInfo, out int seconds)
            && seconds is >= 10 and <= 3600
                ? ValidationResult.ValidResult
                : new ValidationResult(false, "请输入 10 至 3600 之间的整数。");
    }

    private sealed class StylesConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is IEnumerable<string> styles ? string.Join(", ", styles) : "";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
