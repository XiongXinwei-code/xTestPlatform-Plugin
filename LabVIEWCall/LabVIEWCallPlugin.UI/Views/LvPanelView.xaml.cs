using LabVIEWCallPlugin.UI.Models;
using LabVIEWCallPlugin.UI.ViewModels;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI.Views
{
    /// <summary>
    /// Interaction logic for LvPanelView.xaml
    /// </summary>
    public partial class LvPanelView : UserControl
    {
        public LvPanelViewModel ViewModel { get; }
        private bool? _compactLayout;
        private GridLength _wideBindingsWidth = new(2, GridUnitType.Star);
        private GridLength _widePreviewWidth = new(1, GridUnitType.Star);
        private GridLength _compactBindingsHeight = new(2, GridUnitType.Star);
        private GridLength _compactPreviewHeight = new(1, GridUnitType.Star);

        public LvPanelView()
        {
            InitializeComponent();
            ViewModel = new LvPanelViewModel();
            DataContext = ViewModel;
        }

        private void PanelLayout_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0) return;
            bool compact = e.NewSize.Width < 900;
            if (_compactLayout == compact) return;
            if (_compactLayout == false)
            {
                _wideBindingsWidth = BindingsColumn.Width;
                _widePreviewWidth = PreviewColumn.Width;
            }
            else if (_compactLayout == true)
            {
                _compactBindingsHeight = BindingsRow.Height;
                _compactPreviewHeight = PreviewRow.Height;
            }
            _compactLayout = compact;
            BindingsColumn.MinWidth = compact ? 0 : 320;
            PreviewColumn.MinWidth = compact ? 0 : 220;
            BindingsColumn.Width = compact ? new(1, GridUnitType.Star) : _wideBindingsWidth;
            SplitterColumn.Width = new(compact ? 0 : 5);
            PreviewColumn.Width = compact ? new(0) : _widePreviewWidth;
            BindingsRow.Height = compact ? _compactBindingsHeight : new(1, GridUnitType.Star);
            SplitterRow.Height = new(compact ? 5 : 0);
            PreviewRow.Height = compact ? _compactPreviewHeight : new(0);
            Grid.SetColumn(PaneSplitter, compact ? 0 : 1);
            Grid.SetRow(PaneSplitter, compact ? 1 : 0);
            Grid.SetColumn(PreviewPane, compact ? 0 : 2);
            Grid.SetRow(PreviewPane, compact ? 2 : 0);
            PaneSplitter.ResizeDirection = compact ? GridResizeDirection.Rows : GridResizeDirection.Columns;
            PaneSplitter.Cursor = compact ? Cursors.SizeNS : Cursors.SizeWE;
        }
        #region SequenceFile Dependency Property

        public static readonly DependencyProperty SequenceFileProperty =
            DependencyProperty.Register(
                nameof(SequenceFile),
                typeof(SequenceFile),
                typeof(LvPanelView),
                new PropertyMetadata(null, OnSequenceFileChanged));

        /// <summary>
        /// 序列文件引用，用于访问变量等数据（如 C# 脚本检查）
        /// </summary>
        public SequenceFile? SequenceFile
        {
            get => (SequenceFile?)GetValue(SequenceFileProperty);
            set => SetValue(SequenceFileProperty, value);
        }
        private static void OnSequenceFileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LvPanelView view)
            {
                view.ViewModel.SequenceFile = e.NewValue as SequenceFile;
            }
        }
        #endregion

        #region EditPosition Dependency Property

        public static readonly DependencyProperty EditPositionProperty =
            DependencyProperty.Register(
                nameof(EditPosition),
                typeof(EditPosition),
                typeof(LvPanelView),
                new PropertyMetadata(null, OnEditPositionChanged));

        /// <summary>
        /// 编辑位置引用，用于获取当前序列上下文信息
        /// </summary>
        public EditPosition? EditPosition
        {
            get => (EditPosition?)GetValue(EditPositionProperty);
            set => SetValue(EditPositionProperty, value);
        }
        private static void OnEditPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LvPanelView view)
            {
                view.ViewModel.EditPosition = e.NewValue as EditPosition;
            }
        }
        #endregion
        private void OnVariableDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnVariableDrop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: LvPanelNode node } ||
                e.Data.GetData(DataFormats.StringFormat) is not string path || string.IsNullOrWhiteSpace(path)) return;
            var root = node;
            while (root.Parent != null) root = root.Parent;
            if (ViewModel.ControlNodes.Contains(root)) node.ValueSourceTypeEnum = ValueSourceType.Variable;
            node.Variable = path;
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        private void EnumComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.Tag is LvPanelNode node &&
                comboBox.SelectedItem is string selectedValue)
            {
                // 重构完整的枚举 JSON 字符串
                string originalValue = node.Value;
                if (!string.IsNullOrEmpty(originalValue))
                {
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(originalValue))
                        {
                            JsonElement root = doc.RootElement;
                            if (root.TryGetProperty("Enum Strings", out JsonElement enumStrings))
                            {
                                // 保留原始的枚举列表，更新选中的值
                                var updatedJson = JsonSerializer.Serialize(new
                                {
                                    StringValue = selectedValue,
                                    EnumStrings = JsonSerializer.Deserialize<string[]>(enumStrings.GetRawText())
                                }, new JsonSerializerOptions { PropertyNamingPolicy = new SpacedPropertyNamingPolicy() });

                                node.Value = updatedJson;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // JSON 解析失败，直接设置选中的值
                        node.Value = selectedValue;
                    }
                }
                else
                {
                    node.Value = selectedValue;
                }
            }
        }
    }

    /// <summary>
    /// 自定义 JSON 属性命名策略，支持带空格的属性名
    /// </summary>
    internal class SpacedPropertyNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            // 将 PascalCase 转换为 "String Value" 格式
            return name switch
            {
                "StringValue" => "String Value",
                "EnumStrings" => "Enum Strings",
                _ => name
            };
        }
    }


}
