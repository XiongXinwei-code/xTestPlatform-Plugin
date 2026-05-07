using LabVIEWCallPlugin.Models;
using LabVIEWCallPlugin.LVadapter;
using MessagePack;
using MessagePack.Resolvers;
using StepEditor.Abstractions;
using LabVIEWCallPlugin.UI.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using xTestPlatform.Core.SequenceModels;
using static LabVIEWCallPlugin.UI.ViewModels.LvPanelViewModel;

namespace LabVIEWCallPlugin.UI.Views
{
    /// <summary>
    /// LabVIEWCallEditorView.xaml 的交互逻辑。
    /// 参照 VIModelView.xaml.cs 实现，使用 LabVIEWCallSetting 替换 TestSetting。
    /// 不修改 LVadapter 项目中的任何文件。
    /// </summary>
    public partial class LabVIEWCallEditorView : UserControl, IRefreshableEditor
    {
        // ── MessagePack 序列化选项（与 StepPluginBase 保持一致）──────────
        private static readonly MessagePackSerializerOptions _msgPackOptions =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance);

        // ── 防止循环更新的标志 ───────────────────────────────────────────
        private bool _isLoadingFromStep;

        // ── 当前编辑的 Step（用于持久化） ───────────────────────────────
        private Step _step;

        // ── 防抖持久化 ──────────────────────────────────────────────────
        private CancellationTokenSource? _persistCts;

        // ── 框架通过反射注入，用于触发快照（支持撤销/重做）────────────
        public Action<string, Action>? ExecuteCommand { get; set; }

        // ════════════════════════════════════════════════════════════════
        #region Dependency Properties

        // ── Setting ─────────────────────────────────────────────────────
        public static readonly DependencyProperty SettingProperty =
            DependencyProperty.Register(
                nameof(Setting),
                typeof(LabVIEWCallSetting),
                typeof(LabVIEWCallEditorView),
                new PropertyMetadata(null, OnSettingChanged));

        public LabVIEWCallSetting? Setting
        {
            get => (LabVIEWCallSetting?)GetValue(SettingProperty);
            set => SetValue(SettingProperty, value);
        }

        private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LabVIEWCallEditorView view) return;

            if (e.OldValue is LabVIEWCallSetting old)
                old.PropertyChanged -= view.OnSettingPropertyChanged;

            if (e.NewValue is LabVIEWCallSetting newSetting)
            {
                newSetting.PropertyChanged += view.OnSettingPropertyChanged;
                _ = view.LoadFromSettingAsync(newSetting);
            }
        }

        // ── SequenceFile ─────────────────────────────────────────────────
        public static readonly DependencyProperty SequenceFileProperty =
            DependencyProperty.Register(
                nameof(SequenceFile),
                typeof(SequenceFile),
                typeof(LabVIEWCallEditorView),
                new PropertyMetadata(null));

        public SequenceFile? SequenceFile
        {
            get => (SequenceFile?)GetValue(SequenceFileProperty);
            set => SetValue(SequenceFileProperty, value);
        }

        // ── EditPosition ─────────────────────────────────────────────────
        public static readonly DependencyProperty EditPositionProperty =
            DependencyProperty.Register(
                nameof(EditPosition),
                typeof(EditPosition),
                typeof(LabVIEWCallEditorView),
                new PropertyMetadata(null));

        public EditPosition? EditPosition
        {
            get => (EditPosition?)GetValue(EditPositionProperty);
            set => SetValue(EditPositionProperty, value);
        }

        #endregion
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 由 LabVIEWCallPlugin.CreateEditor() 调用。
        /// </summary>
        public LabVIEWCallEditorView(Step step, SequenceFile? sequenceFile)
        {
            _step = step;
            InitializeComponent();

            SequenceFile = sequenceFile;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // ── IRefreshableEditor ───────────────────────────────────────────────
        public void RefreshFromStep(Step step)
        {
            // 更新 _step，确保 PersistSetting() 写入正确的步骤
            _step = step;

            // 从新步骤反序列化并重新加载 UI
            // Setting 赋新对象 → 触发 OnSettingChanged → LoadFromSettingAsync
            Setting = DeserializeStepSetting();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SubscribeToViewModel();

            // 从 Step.StepSetting.Setting 反序列化并加载
            var setting = DeserializeStepSetting();
            Setting = setting;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromViewModel();

            if (Setting != null)
                Setting.PropertyChanged -= OnSettingPropertyChanged;

            _persistCts?.Cancel();
            _persistCts?.Dispose();
            _persistCts = null;
        }

        // ════════════════════════════════════════════════════════════════
        #region LvPanelViewModel 同步

        private void SubscribeToViewModel()
        {
            if (LvPanel?.ViewModel != null)
            {
                LvPanel.ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
                LvPanel.ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void UnsubscribeFromViewModel()
        {
            if (LvPanel?.ViewModel != null)
                LvPanel.ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        /// <summary>
        /// ViewModel 的 ControlJson / IndicatorJson 变化时，同步回 Setting。
        /// </summary>
        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoadingFromStep || Setting == null) return;

            if (e.PropertyName == nameof(LvPanelViewModel.ControlJson))
            {
                Setting.InputParameters = LvPanel.ViewModel.ControlJson;
                SchedulePersist();
            }
            else if (e.PropertyName == nameof(LvPanelViewModel.IndicatorJson))
            {
                Setting.OutputParameters = LvPanel.ViewModel.IndicatorJson;
                SchedulePersist();
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        #region Setting 属性变化（ShowPanel / ClosePanel / ViFilePath）

        private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoadingFromStep) return;

            // ShowPanel / ClosePanel 变化时立即持久化
            if (e.PropertyName is nameof(LabVIEWCallSetting.ShowPanel)
                               or nameof(LabVIEWCallSetting.ClosePanel)
                               or nameof(LabVIEWCallSetting.ViFilePath))
            {
                SchedulePersist();
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        #region 加载数据

        /// <summary>
        /// 从 Setting 加载数据到 UI 和 LvPanelView。
        /// </summary>
        private async Task LoadFromSettingAsync(LabVIEWCallSetting setting)
        {
            if (setting == null) return;

            _isLoadingFromStep = true;
            try
            {
                // 更新路径文本框
                ViPathTextBox.Text = setting.ViFilePath;
                UpdateViPathTextStyle(File.Exists(setting.ViFilePath));

                // 加载 LvPanelView 参数面板
                if (LvPanel?.ViewModel != null)
                {
                    var parameter = new LoadPanelParameter
                    {
                        ViFilePath    = setting.ViFilePath,
                        ControlJson   = setting.InputParameters,
                        IndicatorJson = setting.OutputParameters
                    };
                    await LvPanel.ViewModel.LoadPanelCommand.ExecuteAsync(parameter);
                }
            }
            finally
            {
                _isLoadingFromStep = false;
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        #region 按钮事件

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "选择 VI 文件",
                Filter = "LabVIEW VI (*.vi)|*.vi|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dlg.ShowDialog() == true)
            {
                string path = dlg.FileName;
                ViPathTextBox.Text = path;

                if (Setting != null)
                    Setting.ViFilePath = path;

                System.Diagnostics.Debug.WriteLine($"[LabVIEWCallEditorView] 选中 VI: {path}");
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            string viPath = ViPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(viPath)) return;

            // 先检查 LabVIEW 是否已连接
            if (!LvLibHelper.ConnectLabVIEW())
            {
                MessageBox.Show(
                    "LabVIEW 开发环境未启动或未连接，无法打开 VI 编辑器。\n请先启动 LabVIEW，再重试。",
                    "LabVIEW 未连接",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                LvLibHelper.EditVI(viPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开 VI 编辑器失败：{ex.Message}", "错误",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ReLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LvLibHelper.ConnectLabVIEW())
            {
                MessageBox.Show(
                    "LabVIEW 开发环境未启动或未连接，无法刷新面板。\n请先启动 LabVIEW，再重试。",
                    "LabVIEW 未连接",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            await ReloadViPanelAsync();
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        #region 路径文本框事件

        private async void ViPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string viPath = ViPathTextBox.Text;
            UpdateViPathTextStyle(File.Exists(viPath));

            if (string.IsNullOrWhiteSpace(viPath)) return;

            string ext = Path.GetExtension(viPath);
            if (!File.Exists(viPath) ||
                !string.Equals(ext, ".vi", StringComparison.OrdinalIgnoreCase))
                return;

            var parameter = new LoadPanelParameter
            {
                ViFilePath    = viPath,
                ControlJson   = Setting?.InputParameters  ?? string.Empty,
                IndicatorJson = Setting?.OutputParameters ?? string.Empty
            };

            if (LvPanel?.ViewModel != null)
                await LvPanel.ViewModel.LoadPanelCommand.ExecuteAsync(parameter);

            if (Setting != null && Setting.ViFilePath != viPath)
                Setting.ViFilePath = viPath;
        }

        private async Task ReloadViPanelAsync()
        {
            string viPath = ViPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(viPath)) return;

            bool exists = File.Exists(viPath);
            UpdateViPathTextStyle(exists);
            if (!exists) return;

            string ext = Path.GetExtension(viPath);
            if (!string.Equals(ext, ".vi", StringComparison.OrdinalIgnoreCase)) return;

            var parameter = new LoadPanelParameter
            {
                ViFilePath    = viPath,
                ControlJson   = Setting?.InputParameters  ?? string.Empty,
                IndicatorJson = Setting?.OutputParameters ?? string.Empty
            };

            if (LvPanel?.ViewModel != null)
                await LvPanel.ViewModel.LoadPanelCommand.ExecuteAsync(parameter);

            if (Setting != null)
                Setting.ViFilePath = viPath;
        }

        private void UpdateViPathTextStyle(bool exists)
        {
            if (exists)
            {
                ViPathTextBox.FontStyle     = FontStyles.Normal;
                ViPathTextBox.Foreground    = SystemColors.ControlTextBrush;
                ViPathTextBox.TextDecorations = null;
            }
            else
            {
                ViPathTextBox.FontStyle     = FontStyles.Italic;
                ViPathTextBox.Foreground    = Brushes.Red;
                ViPathTextBox.TextDecorations = TextDecorations.Strikethrough;
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════
        #region 持久化到 Step.StepSetting.Setting

        /// <summary>
        /// 防抖 200ms 后将 Setting 序列化写入 Step，
        /// 通过 ExecuteCommand 触发快照以支持撤销/重做。
        /// </summary>
        private void SchedulePersist()
        {
            _persistCts?.Cancel();
            _persistCts?.Dispose();
            _persistCts = new CancellationTokenSource();
            var token = _persistCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200, token);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (Setting == null) return;

                        void SaveAction()
                        {
                            try
                            {
                                _step.StepSetting.Setting =
                                    MessagePackSerializer.Serialize(Setting, _msgPackOptions);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[LabVIEWCallEditorView] 持久化失败: {ex.Message}");
                            }
                        }

                        if (ExecuteCommand != null)
                            ExecuteCommand("更新 LabVIEWCall 设置", SaveAction);
                        else
                            SaveAction();
                    },
                    System.Windows.Threading.DispatcherPriority.Normal, token);
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private LabVIEWCallSetting DeserializeStepSetting()
        {
            try
            {
                var data = _step.StepSetting?.Setting;
                if (data is { Length: > 0 })
                    return MessagePackSerializer.Deserialize<LabVIEWCallSetting>(data, _msgPackOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[LabVIEWCallEditorView] 反序列化失败: {ex.Message}");
            }
            return new LabVIEWCallSetting();
        }

        #endregion
    }
}