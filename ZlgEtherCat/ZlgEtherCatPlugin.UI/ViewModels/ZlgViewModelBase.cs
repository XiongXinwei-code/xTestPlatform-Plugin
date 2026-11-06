using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Windows.Threading;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;

namespace ZlgEtherCat.UI.ViewModels;

public abstract class ZlgViewModelBase<T> : INotifyPropertyChanged where T : ZlgSetting, new()
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly PluginCommon.EditorSaveScheduler _pending = new();
    private IStepSettingSerializer? _serializer;
    private Step? _step;
    private PendingSaveContext? _pendingSaveContext;
    private SaveFailure? _lastSaveFailure;
    private readonly Dictionary<string, string> _numericTexts = new(StringComparer.Ordinal);
    private bool _settingNumericText;
    private bool _changingOperation;
    protected T Model { get; private set; } = new();
    public string Error { get; private set; } = "";
    public Action<string, Action>? ExecuteCommand { get; set; }
    public Action? CommitPendingInput { get; set; }

    public void AttachSerializer(IStepSettingSerializer serializer)
    {
        _dispatcher.VerifyAccess();
        Flush();
        _serializer = serializer;
    }

    public void AttachStep(Step step)
    {
        _dispatcher.VerifyAccess();
        Flush();
        if (_serializer is null) throw new InvalidOperationException("请先绑定设置序列化器");
        var parsed = step.StepSetting.Setting is { Length: > 0 }
            ? (T)_serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion)
            : (T)_serializer.CreateDefault();
        _step = step;
        _numericTexts.Clear();
        Model = parsed;
        Error = string.Join("；", Model.Validate());
        OnPropertyChanged(string.Empty);
    }

    protected void Changed([CallerMemberName] string? name = null)
    {
        _dispatcher.VerifyAccess();
        Error = string.Join("；", Model.Validate());
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(Error));
        if (_step is null || _serializer is null) return;
        // Capture immutable bytes and the original step before any later refresh.
        var bytes = _serializer.Serialize(Model);
        var step = _step;
        var version = _serializer.SettingVersion;
        var saveContext = new PendingSaveContext(step);
        var hostCommand = ExecuteCommand;
        _pendingSaveContext = saveContext;
        _pending.ExecuteCommand = (description, save) => ExecutePendingSave(saveContext, hostCommand, description, save);
        _pending.Queue(() =>
        {
            step.StepSetting.Setting = bytes;
            step.StepSetting.SettingVersion = version;
        });
    }

    public void Flush()
    {
        _dispatcher.VerifyAccess();
        try
        {
            _pending.Flush();
            Error = string.Join("；", Model.Validate());
            OnPropertyChanged(nameof(Error));
        }
        catch (Exception ex)
        {
            // The queued boundary records the actual source, including timer-driven saves.
            if (!ReferenceEquals(_lastSaveFailure?.Exception, ex)) Error = $"保存未完成：{ex.Message}";
            OnPropertyChanged(nameof(Error));
            throw;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Save the old action before switching. Host commands may synchronously attach another step.</summary>
    protected void SwitchOperation<TOperation>(TOperation value, Func<T, TOperation> get, Action<T, TOperation> set)
        where TOperation : struct, Enum
    {
        _dispatcher.VerifyAccess();
        if (_changingOperation || EqualityComparer<TOperation>.Default.Equals(get(Model), value)) return;
        var originalStep = _step;
        try
        {
            _changingOperation = true;
            CommitPendingInput?.Invoke();
            if (!ReferenceEquals(_step, originalStep)) return;
            Flush();
            if (!ReferenceEquals(_step, originalStep)) return;
            // Flush can refresh the same step with a new model. Always use the current model.
            set(Model, value);
            Changed(string.Empty);
            Flush();
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(_lastSaveFailure?.Exception, ex))
                Error = ReferenceEquals(_step, originalStep)
                    ? $"切换功能时参数提交未完成，请检查当前输入：{ex.Message}"
                    : $"原步骤（{originalStep?.StepSetting.StepType ?? "未绑定步骤"}）参数提交失败；当前步骤未继续应用本次功能切换：{ex.Message}";
            OnPropertyChanged(nameof(Error));
        }
        finally
        {
            _changingOperation = false;
            OnPropertyChanged(string.Empty);
        }
    }

    protected string GetNumericText(string field, object value) => _numericTexts.TryGetValue(field, out var raw)
        ? raw : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    protected void NumericValueChanged(string field)
    {
        if (!_settingNumericText) _numericTexts.Remove(field);
        OnPropertyChanged(field + "Text");
    }

    protected void SetIntegerText(string field, string text, Action<int> setter)
    {
        _dispatcher.VerifyAccess();
        _numericTexts[field] = text;
        bool valid = TryInteger(text, out var parsed);
        try { _settingNumericText = true; setter(valid ? parsed : int.MinValue); }
        finally { _settingNumericText = false; }
        OnPropertyChanged(field + "Text");
    }

    protected void SetDoubleText(string field, string text, Action<double> setter)
    {
        _dispatcher.VerifyAccess();
        _numericTexts[field] = text;
        bool valid = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && double.IsFinite(parsed);
        // A finite out-of-range sentinel survives strict serialization and cannot execute.
        try { _settingNumericText = true; setter(valid ? parsed : double.MinValue); }
        finally { _settingNumericText = false; }
        OnPropertyChanged(field + "Text");
    }

    private static bool TryInteger(string text, out int value)
    {
        var input = text.Trim();
        if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            bool success = uint.TryParse(input.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex) && hex <= int.MaxValue;
            value = success ? (int)hex : int.MinValue;
            return success;
        }
        return int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void ExecutePendingSave(PendingSaveContext source, Action<string, Action>? hostCommand, string description, Action save)
    {
        // Match EditorSaveScheduler: remove this item before invoking reentrant host code.
        if (ReferenceEquals(_pendingSaveContext, source)) _pendingSaveContext = null;
        _lastSaveFailure = null;
        try
        {
            if (hostCommand is null) save();
            else hostCommand(description, save);
            Error = string.Join("；", Model.Validate());
            OnPropertyChanged(nameof(Error));
        }
        catch (Exception ex)
        {
            // A newly queued edit wins; otherwise retain the failed item's original source.
            _pendingSaveContext ??= source;
            if (!ReferenceEquals(_lastSaveFailure?.Exception, ex))
            {
                _lastSaveFailure = new SaveFailure(source, ex);
                string retention = ReferenceEquals(_pendingSaveContext, source)
                    ? "原待保存内容已保留"
                    : "新的编辑已替代本次待保存项，本次失败的保存不会作为重试项保留";
                Error = ReferenceEquals(_step, source.Step)
                    ? $"保存失败；{retention}：{ex.Message}"
                    : $"原步骤（{source.Step.StepSetting.StepType}）保存失败；当前步骤未因本次保存改动；{retention}：{ex.Message}";
                OnPropertyChanged(nameof(Error));
            }
            throw;
        }
    }

    private sealed class PendingSaveContext(Step step)
    {
        public Step Step { get; } = step;
    }

    private sealed record SaveFailure(PendingSaveContext Source, Exception Exception);
}
