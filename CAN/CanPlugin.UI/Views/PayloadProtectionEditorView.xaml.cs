using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CAN.Models;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

/// <summary>发送与 DBC 接收复用保护配置；接收只提取 RC 和校验 CRC，不应用发送计数规则。</summary>
public partial class PayloadProtectionEditorView : UserControl
{
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(
        nameof(SequenceFile), typeof(SequenceFile), typeof(PayloadProtectionEditorView), new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(
        nameof(EditPosition), typeof(EditPosition), typeof(PayloadProtectionEditorView), new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }
    public static readonly DependencyProperty ReadModeProperty = DependencyProperty.Register(
        nameof(ReadMode), typeof(bool), typeof(PayloadProtectionEditorView), new PropertyMetadata(false));
    public bool ReadMode { get => (bool)GetValue(ReadModeProperty); set => SetValue(ReadModeProperty, value); }
    public static readonly DependencyProperty DataIdRowsProperty = DependencyProperty.Register(
        nameof(DataIdRows), typeof(IReadOnlyList<CanDataIdTableRow>), typeof(PayloadProtectionEditorView), new PropertyMetadata(null));
    public IReadOnlyList<CanDataIdTableRow> DataIdRows => (IReadOnlyList<CanDataIdTableRow>?)GetValue(DataIdRowsProperty) ?? [];
    public static readonly DependencyProperty TableWarningProperty = DependencyProperty.Register(
        nameof(TableWarning), typeof(string), typeof(PayloadProtectionEditorView), new PropertyMetadata(""));
    public string TableWarning => (string)GetValue(TableWarningProperty);
    public static readonly DependencyProperty FixedModeProperty = ModeProperty(nameof(FixedMode), true);
    public static readonly DependencyProperty ProfileModeProperty = ModeProperty(nameof(ProfileMode));
    public static readonly DependencyProperty Profile1ModeProperty = ModeProperty(nameof(Profile1Mode));
    public static readonly DependencyProperty Profile2ModeProperty = ModeProperty(nameof(Profile2Mode));
    public static readonly DependencyProperty NibbleModeProperty = ModeProperty(nameof(NibbleMode));
    public static readonly DependencyProperty GenericCrcEditableProperty = ModeProperty(nameof(GenericCrcEditable), true);
    public static readonly DependencyProperty CustomProfilePlacementProperty = ModeProperty(nameof(CustomProfilePlacement));
    public static readonly DependencyProperty InlineTableSourceProperty = ModeProperty(nameof(InlineTableSource), true);
    public static readonly DependencyProperty ProfilePlacementSummaryProperty = DependencyProperty.Register(
        nameof(ProfilePlacementSummary), typeof(string), typeof(PayloadProtectionEditorView), new PropertyMetadata(""));
    public bool FixedMode => (bool)GetValue(FixedModeProperty);
    public bool ProfileMode => (bool)GetValue(ProfileModeProperty);
    public bool Profile1Mode => (bool)GetValue(Profile1ModeProperty);
    public bool Profile2Mode => (bool)GetValue(Profile2ModeProperty);
    public bool NibbleMode => (bool)GetValue(NibbleModeProperty);
    public bool GenericCrcEditable => (bool)GetValue(GenericCrcEditableProperty);
    public bool CustomProfilePlacement => (bool)GetValue(CustomProfilePlacementProperty);
    public bool InlineTableSource => (bool)GetValue(InlineTableSourceProperty);
    public string ProfilePlacementSummary => (string)GetValue(ProfilePlacementSummaryProperty);
    private CanPayloadProtectionSetting? _setting;
    private bool _unloaded;
    private bool _updatingTable;
    private long _tableGeneration;

    private static DependencyProperty ModeProperty(string name, bool initial = false) =>
        DependencyProperty.Register(name, typeof(bool), typeof(PayloadProtectionEditorView), new PropertyMetadata(initial));

    public PayloadProtectionEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => { if (!_unloaded) AttachSetting(); };
        Loaded += (_, _) => { _unloaded = false; AttachSetting(); };
        Unloaded += (_, _) => { _unloaded = true; DetachSetting(); };
    }

    private void DetachSetting()
    {
        if (_setting != null) _setting.PropertyChanged -= OnSettingChanged;
        _setting = null;
        ++_tableGeneration; // Retained cells can no longer write to this or a later step.
    }

    private void AttachSetting()
    {
        DetachSetting();
        _setting = DataContext as CanPayloadProtectionSetting;
        if (_setting != null) _setting.PropertyChanged += OnSettingChanged;
        RefreshModes();
        RefreshTable();
    }

    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshModes();
        if (!_updatingTable && (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is nameof(CanPayloadProtectionSetting.CrcDataIdTable) or nameof(CanPayloadProtectionSetting.CrcDataIdTableUseExpression)))
            RefreshTable();
    }

    private void RefreshModes()
    {
        var mode = _setting?.CrcDataIdMode ?? CanCrcDataIdMode.Fixed;
        bool profile1 = mode is CanCrcDataIdMode.Profile1Both or CanCrcDataIdMode.Profile1Alt or CanCrcDataIdMode.Profile1Low or CanCrcDataIdMode.Profile1Nibble;
        bool profile = profile1 || mode == CanCrcDataIdMode.Profile2;
        SetValue(FixedModeProperty, mode == CanCrcDataIdMode.Fixed);
        SetValue(ProfileModeProperty, profile);
        SetValue(Profile1ModeProperty, profile1);
        SetValue(Profile2ModeProperty, mode == CanCrcDataIdMode.Profile2);
        SetValue(NibbleModeProperty, mode == CanCrcDataIdMode.Profile1Nibble);
        SetValue(GenericCrcEditableProperty, !profile || _setting?.CrcDataIdEnabled != true);
        SetValue(InlineTableSourceProperty, _setting?.CrcDataIdTableUseExpression != true);
        bool standardPlacement = _setting?.CrcDataIdUseProfilePlacement != false;
        SetValue(CustomProfilePlacementProperty, profile && !standardPlacement);
        var actualPlacement = standardPlacement
            ? mode == CanCrcDataIdMode.Profile2 ? CanCrcDataIdPlacement.AfterData : CanCrcDataIdPlacement.BeforeData
            : _setting!.CrcDataIdPlacement;
        string position = actualPlacement switch
        {
            CanCrcDataIdPlacement.BeforeData => "计算范围之前：Data ID 输入 → 数据",
            CanCrcDataIdPlacement.AfterData => "计算范围之后：数据 → Data ID 输入",
            _ => "无效位置，请重新选择"
        };
        SetValue(ProfilePlacementSummaryProperty, $"当前拼接：{position}（{(standardPlacement ? "Profile 标准顺序" : "自定义顺序")}）");
    }

    private void RefreshTable()
    {
        var owner = _setting;
        long generation = ++_tableGeneration;
        var rows = new CanDataIdTableRow[16];
        for (int index = 0; index < rows.Length; index++)
        {
            int capturedIndex = index;
            string value = owner?.CrcDataIdTable is { } table && index < table.Length ? table[index] ?? "" : "";
            rows[index] = new(index, value, edited =>
            {
                if (_unloaded || generation != _tableGeneration || !ReferenceEquals(owner, _setting) || owner == null) return;
                // Keep malformed extra entries visible to validation until the user explicitly resets the table.
                int length = Math.Max(16, owner.CrcDataIdTable?.Length ?? 0);
                var copy = Enumerable.Repeat("", length).ToArray();
                owner.CrcDataIdTable?.CopyTo(copy, 0);
                copy[capturedIndex] = edited;
                _updatingTable = true;
                try { owner.CrcDataIdTable = copy; }
                finally { _updatingTable = false; }
                UpdateTableWarning();
            });
        }
        SetValue(DataIdRowsProperty, rows);
        UpdateTableWarning();
    }

    private void UpdateTableWarning()
    {
        int count = _setting?.CrcDataIdTable?.Length ?? 0;
        SetValue(TableWarningProperty, count == 16 ? "" : $"当前保存了 {count} 项，必须恰好 16 项；可用“新建 16 项空表”重新填写。");
    }

    private void OnResetDataIdTable(object sender, RoutedEventArgs e)
    {
        if (_setting != null) _setting.CrcDataIdTable = Enumerable.Repeat("", 16).ToArray();
    }

    private void OnApplyProfileLayout(object sender, RoutedEventArgs e)
    {
        if (_setting is not { } s || !ProfileMode || ReadMode) return;
        s.CrcEnabled = true; s.CrcDataIdEnabled = true;
        s.CrcDataIdUseProfilePlacement = true;
        s.CrcStartByte = 0; s.CrcEndByte = 0;
        s.CrcDataStartByte = 0; s.CrcDataEndByte = -1; s.CrcFieldTreatment = CanCrcFieldTreatment.Skip;
        s.RcEnabled = true; s.RcStartByte = 1; s.RcEndByte = 1;
        s.RcUseBitField = true; s.RcBitOffset = 0; s.RcBitLength = 4;
        s.RcMinimum = 0; s.RcMaximum = Profile2Mode ? 15u : 14u;
        // The first on-wire counter is user-owned, independent of the profile suggestion.
        s.RcStep = 1; s.RcOverflow = CanCounterOverflow.Wrap;
        s.CrcCounterByte = 1; s.CrcCounterBitOffset = 0;
        s.CrcDataIdNibbleByte = 1; s.CrcDataIdNibbleBitOffset = 4;
    }

    private void OnSetFirstCounter(object sender, RoutedEventArgs e)
    {
        if (_unloaded || ReadMode || _setting is not { RcEnabled: true } s) return;
        if (sender is Button { Tag: "0" }) s.RcInitialValue = 0;
        else if (sender is Button { Tag: "1" }) s.RcInitialValue = 1;
    }
}

/// <summary>编辑一项时整体替换设置数组，以触发标准编辑器的防抖保存；不原地修改共享数组。</summary>
public sealed class CanDataIdTableRow : INotifyPropertyChanged
{
    private string _value;
    private readonly Action<string> _changed;
    public CanDataIdTableRow(int counter, string value, Action<string> changed) { Counter = counter; _value = value; _changed = changed; }
    public int Counter { get; }
    public string CounterLabel => $"{Counter} →";
    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            _changed(value);
            PropertyChanged?.Invoke(this, new(nameof(Value)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
