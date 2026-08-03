$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = "D:\xTestPlatform-PluginDev\VISA\VisaPlugin.UI\Views"
New-Item -Path $root -ItemType Directory -Force | Out-Null

$iconUri = "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png"
$nsHeader = @"
<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d"
			 d:DesignHeight="400" d:DesignWidth="800"
"@

# Helper function for simple views with ConnectionName + extra fields
function Make-View($name, $header, $fields) {
	$xaml = "$nsHeader             x:Class=`"VISA.UI.Views.${name}`">`n"
	$xaml += "    <syncfusion:TabControlExt CloseButtonType=`"Hide`" AllowDragDrop=`"False`" EnableLabelEdit=`"False`">`n"
	$xaml += "        <syncfusion:TabItemExt Header=`"$header`"`n"
	$xaml += "                               Image=`"$iconUri`"`n"
	$xaml += "                               ImageHeight=`"20`" ImageWidth=`"20`">`n"
	$xaml += "            <Grid Margin=`"16`">`n"
	$xaml += "                <Grid.ColumnDefinitions>`n"
	$xaml += "                    <ColumnDefinition Width=`"140`"/>`n"
	$xaml += "                    <ColumnDefinition Width=`"*`"/>`n"
	$xaml += "                </Grid.ColumnDefinitions>`n"
	$xaml += "                <Grid.RowDefinitions>`n"
	$row = 0
	foreach ($f in $fields) { $xaml += "                    <RowDefinition Height=`"Auto`"/>`n"; $row++ }
	$xaml += "                </Grid.RowDefinitions>`n"
	$row = 0
	foreach ($f in $fields) {
		$label = $f[0]; $binding = $f[1]; $type = $f[2]
		$xaml += "                <TextBlock Grid.Row=`"$row`" Grid.Column=`"0`" Text=`"$label`" VerticalAlignment=`"Center`" Margin=`"0,6`"/>`n"
		if ($type -eq "expr") {
			$xaml += "                <expr:ExpressionTextBox Grid.Row=`"$row`" Grid.Column=`"1`" Margin=`"0,6`"`n"
			$xaml += "                    ScriptText=`"{Binding $binding, Mode=TwoWay}`"`n"
			$xaml += "                    SequenceFile=`"{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}`"`n"
			$xaml += "                    EditPosition=`"{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}`" />`n"
		} elseif ($type -eq "int") {
			$xaml += "                <TextBox Grid.Row=`"$row`" Grid.Column=`"1`" Margin=`"0,6`" Text=`"{Binding $binding, UpdateSourceTrigger=PropertyChanged}`" />`n"
		} elseif ($type -eq "text") {
			$xaml += "                <TextBox Grid.Row=`"$row`" Grid.Column=`"1`" Margin=`"0,6`" Text=`"{Binding $binding, UpdateSourceTrigger=PropertyChanged}`" />`n"
		} elseif ($type -eq "bool") {
			$xaml += "                <CheckBox Grid.Row=`"$row`" Grid.Column=`"1`" Margin=`"0,6`" IsChecked=`"{Binding $binding}`" />`n"
		}
		$row++
	}
	$xaml += "            </Grid>`n"
	$xaml += "        </syncfusion:TabItemExt>`n"
	$xaml += "    </syncfusion:TabControlExt>`n"
	$xaml += "</UserControl>"
	return $xaml
}

function Make-CodeBehind($name, $vmType, $pluginType) {
	return @"
using System.Windows.Controls;
using VISA.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Views;

/// <summary>
/// $name 编辑器视图
/// </summary>
public partial class $name : UserControl, IRefreshableEditor
{
	public $vmType ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ${name}()
	{
		InitializeComponent();
		ViewModel = new $vmType();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ${pluginType}().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
"@
}

# VisaOpenEditorView
$fields = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("资源字符串:", "ResourceString", "expr"),
	@("打开超时(ms):", "OpenTimeoutMs", "int"),
	@("IO超时(ms):", "IoTimeoutMs", "int"),
	@("终止符:", "Terminator", "text")
)
$xaml = Make-View "VisaOpenEditorView" "VISA Open" $fields
[IO.File]::WriteAllText("$root\VisaOpenEditorView.xaml", $xaml, $utf8)
$cs = Make-CodeBehind "VisaOpenEditorView" "VisaOpenViewModel" "VisaOpenPlugin"
[IO.File]::WriteAllText("$root\VisaOpenEditorView.xaml.cs", $cs, $utf8)

# VisaCloseEditorView
$fields = @(
	@("连接名称:", "ConnectionName", "expr")
)
$xaml = Make-View "VisaCloseEditorView" "VISA Close" $fields
[IO.File]::WriteAllText("$root\VisaCloseEditorView.xaml", $xaml, $utf8)
$cs = Make-CodeBehind "VisaCloseEditorView" "VisaCloseViewModel" "VisaClosePlugin"
[IO.File]::WriteAllText("$root\VisaCloseEditorView.xaml.cs", $cs, $utf8)

# VisaWriteEditorView
$fields = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("SCPI 命令:", "Command", "expr")
)
$xaml = Make-View "VisaWriteEditorView" "VISA Write" $fields
[IO.File]::WriteAllText("$root\VisaWriteEditorView.xaml", $xaml, $utf8)
$cs = Make-CodeBehind "VisaWriteEditorView" "VisaWriteViewModel" "VisaWritePlugin"
[IO.File]::WriteAllText("$root\VisaWriteEditorView.xaml.cs", $cs, $utf8)

# VisaQueryEditorView
$fields = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("SCPI 命令:", "Command", "expr"),
	@("结果变量:", "ResultVariable", "expr"),
	@("去除空白:", "TrimResponse", "bool")
)
$xaml = Make-View "VisaQueryEditorView" "VISA Query" $fields
[IO.File]::WriteAllText("$root\VisaQueryEditorView.xaml", $xaml, $utf8)
$cs = Make-CodeBehind "VisaQueryEditorView" "VisaQueryViewModel" "VisaQueryPlugin"
[IO.File]::WriteAllText("$root\VisaQueryEditorView.xaml.cs", $cs, $utf8)

# VisaReadEditorView
$fields = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("结果变量:", "ResultVariable", "expr"),
	@("去除空白:", "TrimResponse", "bool")
)
$xaml = Make-View "VisaReadEditorView" "VISA Read" $fields
[IO.File]::WriteAllText("$root\VisaReadEditorView.xaml", $xaml, $utf8)
$cs = Make-CodeBehind "VisaReadEditorView" "VisaReadViewModel" "VisaReadPlugin"
[IO.File]::WriteAllText("$root\VisaReadEditorView.xaml.cs", $cs, $utf8)

# VisaWaitOpcEditorView
$fields = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("超时(ms):", "TimeoutMs", "int")
)
$xaml = Make-View "VisaWaitOpcEditorView" "VISA WaitOPC" $fields
[IO.File]::WriteAllText("$root\VisaWaitOpcEditorView.xaml", $xaml, $utf8)
$cs = Make-CodeBehind "VisaWaitOpcEditorView" "VisaWaitOpcViewModel" "VisaWaitOpcPlugin"
[IO.File]::WriteAllText("$root\VisaWaitOpcEditorView.xaml.cs", $cs, $utf8)

Write-Host "Views created."
