using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.UI.Converters;
using LabVIEWCallPlugin.UI.Models;
using PluginCommon;
using PluginCommon.UI;
using StepEditor.Abstractions;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI.Views;

public partial class LabVIEWCallEditorView : UserControl, IRefreshableEditor
{
    private readonly Func<string, string, string, CancellationToken, int, ConnectPanelInfo> _readPanel;
    private readonly ViFileProbe _files;
    private readonly Action<string, CancellationToken, int> _editVi;
    private readonly EditorSaveScheduler _save = new();
    private readonly IStepSettingSerializer _serializer = new LabVIEWCallStepPlugin().CreateSerializer();
    private Step _step;
    private byte[]? _published;
    private CancellationTokenSource? _load;
    private bool _applyingPanel;

    public Action<string, Action>? ExecuteCommand
    {
        get => _save.ExecuteCommand;
        set => _save.ExecuteCommand = value;
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(LabVIEWCallSetting), typeof(LabVIEWCallEditorView));
    public LabVIEWCallSetting? Setting
    {
        get => (LabVIEWCallSetting?)GetValue(SettingProperty);
        private set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(
        nameof(SequenceFile), typeof(SequenceFile), typeof(LabVIEWCallEditorView));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(
        nameof(EditPosition), typeof(EditPosition), typeof(LabVIEWCallEditorView));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public LabVIEWCallEditorView(Step step, SequenceFile? sequenceFile) : this(step, sequenceFile, ReadPanel) { }

    internal LabVIEWCallEditorView(Step step, SequenceFile? sequenceFile,
        Func<string, string, string, CancellationToken, int, ConnectPanelInfo> readPanel, Func<string, bool>? fileExists = null,
        Action<string, CancellationToken, int>? editVi = null)
    {
        _readPanel = readPanel;
        _files = fileExists == null ? ViFileProbe.Shared : new ViFileProbe(fileExists);
        _editVi = editVi ?? OpenViEditor;
        _step = step;
        InitializeComponent();
        SequenceFile = sequenceFile;
        PluginEditorCommit.Attach(this, _save.Flush);
        Loaded += (_, _) =>
        {
            LvPanel.ViewModel.BindingsChanged -= OnBindingsChanged;
            LvPanel.ViewModel.BindingsChanged += OnBindingsChanged;
            RefreshFromStep(_step);
        };
        Unloaded += (_, _) =>
        {
            try { _save.Flush(); }
            finally
            {
                CancelLoad();
                LvPanel.ViewModel.BindingsChanged -= OnBindingsChanged;
                DetachSetting();
            }
        };
    }

    public void RefreshFromStep(Step step)
    {
        _save.Flush();
        var bytes = step.StepSetting?.Setting;
        if (ReferenceEquals(step, _step) && Setting != null &&
            bytes is { Length: > 0 } && _published != null && bytes.AsSpan().SequenceEqual(_published)) return;

        CancelLoad();
        DetachSetting();
        _step = step;
        LvPanel.ViewModel.ShowSnapshot([], [], null);
        _published = bytes?.ToArray();
        try
        {
            Setting = bytes is { Length: > 0 }
                ? (LabVIEWCallSetting)_serializer.Deserialize(bytes, step.StepSetting!.SettingVersion)
                : new LabVIEWCallSetting();
            Setting.PropertyChanged += SettingChanged;
            _ = LoadPanelAsync(online: false);
        }
        catch (Exception ex)
        {
            LvPanel.ViewModel.ShowSnapshot([], [], null);
            Feedback.Text = $"无法读取步骤设置，原始配置已保留：{ex.Message}";
        }
    }

    private void DetachSetting()
    {
        if (Setting != null) Setting.PropertyChanged -= SettingChanged;
        Setting = null;
    }

    private void OnBindingsChanged(bool output, string json)
    {
        if (Setting == null) return;
        if (output) Setting.OutputParameters = json;
        else Setting.InputParameters = json;
    }

    private void EditorContent_SizeChanged(object sender, SizeChangedEventArgs e)
        => EditorContent.MinHeight = e.NewSize.Width < 900 ? 700 : 440;

    private void SettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LabVIEWCallSetting setting || !ReferenceEquals(setting, Setting)) return;
        if (!_applyingPanel && (e.PropertyName is nameof(LabVIEWCallSetting.ViServerPort) or nameof(LabVIEWCallSetting.ViFilePath)))
        {
            CancelLoad();
            LvPanel.ViewModel.MarkNotChecked($"连接配置已更改；尚未在线检查 127.0.0.1:{setting.ViServerPort}。已保存的端子映射仍保留。");
            Feedback.Text = "";
        }
        var original = _step;
        _save.Queue(() =>
        {
            var bytes = _serializer.Serialize(setting);
            if (ReferenceEquals(_step, original)) _published = bytes.ToArray();
            original.StepSetting.SettingVersion = _serializer.SettingVersion;
            original.StepSetting.Setting = bytes;
        });
    }

    private void CancelLoad()
    {
        var previous = _load;
        _load = null;
        previous?.Cancel();
        previous?.Dispose();
        LvPanel.ViewModel.IsLoading = false;
    }

    // Automatic refresh always uses saved settings. Access to the VI server is explicitly requested.
    internal async Task LoadPanelAsync(bool online, string? candidatePath = null)
    {
        if (Setting is not { } owner) return;
        var originalPath = owner.ViFilePath;
        int port = owner.ViServerPort;
        var path = candidatePath ?? originalPath;
        bool replacingVi = !string.Equals(path, originalPath, StringComparison.OrdinalIgnoreCase);
        var inputs = owner.InputParameters;
        var outputs = owner.OutputParameters;
        CancelLoad();
        var request = _load = new CancellationTokenSource();
        var token = request.Token;
        var vm = LvPanel.ViewModel;
        vm.IsLoading = true;
        Feedback.Text = "";
        try
        {
            if (online) ValidatePort(port);
            var native = online ? await WithFileAsync("刷新 VI 连接器", path, owner.TimeoutMs, token,
                actual => _readPanel(path, replacingVi ? "" : inputs, replacingVi ? "" : outputs, actual, port)) : null;
            if (native != null && (native.VIState == VIState.Bad || !Enum.IsDefined(native.VIState) ||
                                  string.IsNullOrWhiteSpace(native.ControlOut) || string.IsNullOrWhiteSpace(native.IndicatorOut)))
                throw new InvalidDataException($"VI 未返回有效的连接器信息（状态 {native.VIState}）。");
            var inputDocument = native?.ControlOut ?? inputs;
            var outputDocument = native?.IndicatorOut ?? outputs;
            var panel = await Task.Run(() => (Inputs: LvPanelConverter.ConvertFromJson(inputDocument),
                Outputs: LvPanelConverter.ConvertFromJson(outputDocument)), token);
            if (!ReferenceEquals(request, _load) || !ReferenceEquals(Setting, owner)) return;
            if (owner.ViFilePath != originalPath || owner.ViServerPort != port || owner.InputParameters != inputs || owner.OutputParameters != outputs) return;
            vm.ShowSnapshot(panel.Inputs, panel.Outputs, native);
            if (native != null)
            {
                _applyingPanel = true;
                try
                {
                    owner.ViFilePath = path;
                    owner.InputParameters = inputDocument;
                    owner.OutputParameters = outputDocument;
                }
                finally { _applyingPanel = false; }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (ReferenceEquals(request, _load))
            {
                Feedback.Text = $"读取 VI 面板失败，配置已保留：{ex.Message}";
                if (online) vm.MarkOnlineFailure($"无法完成 127.0.0.1:{port} 的在线检查；请查看上方错误信息。原端子映射仍保留。");
            }
        }
        finally
        {
            if (ReferenceEquals(request, _load))
            {
                vm.IsLoading = false;
                _load = null;
                request.Dispose();
            }
        }
    }

    private static ConnectPanelInfo ReadPanel(string path, string inputs, string outputs, CancellationToken token, int port)
        => IsolatedViBridge.Shared.ReadPanel(path, inputs, outputs, token, port);

    private async Task<T> WithFileAsync<T>(string operation, string path, int timeoutMs, CancellationToken caller,
        Func<CancellationToken, T> native)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        if (timeoutMs > 0) deadline.CancelAfter(timeoutMs);
        try
        {
            if (!await _files.ExistsAsync(path, deadline.Token).ConfigureAwait(false))
                throw new FileNotFoundException($"找不到 VI 文件或无法访问路径：{path}", path);
            return await ViCallGate.RunAsync(operation, 0, deadline.Token, native).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!caller.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException($"{operation} 等待超过 {timeoutMs} ms。", ex);
        }
    }

    private static void OpenViEditor(string path, CancellationToken token, int port)
        => IsolatedViBridge.Shared.Edit(path, token, port);

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port), "VI Server 端口必须在 1 至 65535 之间。");
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        if (Setting is not { } owner) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "选择 VI 文件", Filter = "LabVIEW VI (*.vi)|*.vi", CheckFileExists = true };
        if (dialog.ShowDialog() != true || !ReferenceEquals(owner, Setting)) return;
        await LoadPanelAsync(true, dialog.FileName);
    }

    private async void ReLoadButton_Click(object sender, RoutedEventArgs e) => await LoadPanelAsync(true);

    private async void EditButton_Click(object sender, RoutedEventArgs e) => await EditViAsync();

    internal async Task EditViAsync()
    {
        if (Setting is not { } owner) return;
        var path = owner.ViFilePath;
        int port = owner.ViServerPort;
        CancelLoad();
        var request = _load = new CancellationTokenSource();
        var token = request.Token;
        try
        {
            LvPanel.ViewModel.IsLoading = true;
            ValidatePort(port);
            await WithFileAsync("在 LabVIEW 中打开 VI", path, owner.TimeoutMs, token,
                actual => { _editVi(path, actual, port); return true; });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (ReferenceEquals(request, _load))
            {
                Feedback.Text = $"打开 VI 失败：{ex.Message}";
                LvPanel.ViewModel.MarkOnlineFailure($"无法在 127.0.0.1:{port} 的 LabVIEW 中打开 VI；原端子映射仍保留。");
            }
        }
        finally
        {
            if (ReferenceEquals(request, _load))
            {
                LvPanel.ViewModel.IsLoading = false;
                _load = null;
                request.Dispose();
            }
        }
    }
}
