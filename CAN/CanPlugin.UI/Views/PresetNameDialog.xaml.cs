using System.Windows;

namespace CAN.UI.Views;

/// <summary>输入预设名称与备注的小对话框</summary>
public partial class PresetNameDialog : Window
{
    public PresetNameDialog()
    {
        InitializeComponent();
    }

    /// <summary>预设名称</summary>
    public string PresetName
    {
        get => NameBox.Text;
        set => NameBox.Text = value;
    }

    /// <summary>备注说明</summary>
    public string Remark
    {
        get => RemarkBox.Text;
        set => RemarkBox.Text = value;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("请输入预设名称", "保存为预设", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
