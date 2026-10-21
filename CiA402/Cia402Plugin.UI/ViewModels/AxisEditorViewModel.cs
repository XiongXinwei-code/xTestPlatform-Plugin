using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using CiA402.Core;
using CiA402.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.ViewModels;

public class AxisEditorViewModel : INotifyPropertyChanged
{
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private IStepSettingSerializer? _serializer;
    private Step? _step;
    private AxisSetting? _setting;
    private bool _dirty;
    public ObservableCollection<FieldViewModel> Fields { get; } = new();
    public string IconPath { get; private set; } = "";
    public string Title { get; private set; } = "CiA402";
    public string Error { get; private set; } = "";
    public Action<string, Action>? ExecuteCommand { get; set; }
    public AxisEditorViewModel() => _saveTimer.Tick += (_, _) => Flush();

    public void Attach(ICia402Plugin plugin, Step step)
    {
        Flush();
        foreach (var item in Fields) item.PropertyChanged -= OnFieldChanged;
        Fields.Clear();
        _dirty = false;
        _step = step;
        _serializer = plugin.CreateSerializer();
        IconPath = plugin.IconPath ?? "";
        Title = plugin.DisplayName;
        try
        {
            _setting = step.StepSetting.Setting is { Length: > 0 } data
                ? (AxisSetting)_serializer.Deserialize(data, step.StepSetting.SettingVersion) : (AxisSetting)_serializer.CreateDefault();
            foreach (var field in SettingFields.Get(_setting.GetType()))
            {
                var item = new FieldViewModel(_setting, field);
                item.PropertyChanged += OnFieldChanged;
                Fields.Add(item);
            }
            Error = string.Join("；", SettingValidation.Validate(_setting));
        }
        catch (Exception ex) { _setting = null; Error = $"读取设置失败：{ex.Message}"; }
        Changed(string.Empty);
    }

    private void OnFieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FieldViewModel.Value)) return;
        _dirty = true;
        foreach (var field in Fields) field.RefreshVisibility();
        Error = _setting == null ? "设置不存在" : string.Join("；", SettingValidation.Validate(_setting));
        Changed(nameof(Error));
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void Flush()
    {
        _saveTimer.Stop();
        if (!_dirty || _step == null || _serializer == null || _setting == null || Fields.Any(f => f.Error.Length > 0)) return;
        try
        {
            var bytes = _serializer.Serialize(_setting);
            _dirty = false;
            if (_step.StepSetting.Setting != null && bytes.SequenceEqual(_step.StepSetting.Setting)) return;
            var step = _step;
            void Save() { step.StepSetting.Setting = bytes; step.StepSetting.SettingVersion = 1; }
            if (ExecuteCommand != null) ExecuteCommand("修改 CiA402 参数", Save); else Save();
        }
        catch (Exception ex) { Error = $"保存设置失败：{ex.Message}"; Changed(nameof(Error)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class FieldViewModel(AxisSetting setting, SettingField field) : INotifyPropertyChanged
{
    private string? _invalidText;
    public string Label => field.Metadata.Label;
    public string Description => field.Metadata.Description;
    public string Error { get; private set; } = "";
    public bool IsVisible => field.IsActive(setting);
    public bool IsStringExpression => field.IsExpression && field.Metadata.ResultType == typeof(string);
    public bool IsNumericExpression => field.IsExpression && field.Metadata.ResultType != typeof(string);
    public bool IsBoolean => field.Property.PropertyType == typeof(bool);
    public bool IsEnum => field.Property.PropertyType.IsEnum;
    public bool IsText => !field.IsExpression && !IsBoolean && !IsEnum;
    public Array Options => IsEnum ? Enum.GetValues(field.Property.PropertyType) : Array.Empty<object>();
    public string ExpressionValue { get => field.IsExpression ? Value?.ToString() ?? "" : ""; set { if (field.IsExpression) Value = value; } }
    public string TextValue { get => IsText ? Value?.ToString() ?? "" : ""; set { if (IsText) Value = value; } }
    public bool BooleanValue { get => IsBoolean && Value is true; set { if (IsBoolean) Value = value; } }
    public object? EnumValue { get => IsEnum ? Value : null; set { if (IsEnum && value != null) Value = value; } }
    public object? Value
    {
        get => _invalidText ?? field.Property.GetValue(setting);
        set
        {
            try
            {
                var type = field.Property.PropertyType;
                var converted = type.IsEnum ? value is string s ? Enum.Parse(type, s) : value
                    : type == typeof(string) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
                    : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                if (Equals(converted, field.Property.GetValue(setting)) && Error.Length == 0) return;
                field.Property.SetValue(setting, converted);
                Error = "";
                _invalidText = null;
            }
            catch (Exception) { _invalidText = value?.ToString() ?? ""; Error = "格式无效，请输入有效参数；当前内容尚未保存"; }
            PropertyChanged?.Invoke(this, new(nameof(Value)));
            PropertyChanged?.Invoke(this, new(nameof(Error)));
            PropertyChanged?.Invoke(this, new(nameof(ExpressionValue)));
            PropertyChanged?.Invoke(this, new(nameof(TextValue)));
            PropertyChanged?.Invoke(this, new(nameof(BooleanValue)));
            PropertyChanged?.Invoke(this, new(nameof(EnumValue)));
        }
    }
    public void RefreshVisibility() => PropertyChanged?.Invoke(this, new(nameof(IsVisible)));
    public event PropertyChangedEventHandler? PropertyChanged;
}
