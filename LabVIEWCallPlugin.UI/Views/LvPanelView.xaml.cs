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

        public LvPanelView()
        {
            InitializeComponent();
            ViewModel = new LvPanelViewModel();
            DataContext = ViewModel;
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
            if (e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string data = (string)e.Data.GetData(DataFormats.StringFormat);
                if (sender is TextBlock textBlock)
                {
                    textBlock.Text = data;
                    // 显式更新绑定源
                    textBlock.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                }
            }
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