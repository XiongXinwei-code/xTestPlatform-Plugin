$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = "D:\xTestPlatform-PluginDev\VISA\VisaPlugin.UI\Views"

$iconUri = "pack://application:,,,/VISA.StepPlugin.UI;component/Resources/Icons/visa.png"

function Make-Xaml($className, $header, $rows) {
	$xaml = @"
<UserControl xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d"
			 d:DesignHeight="400" d:DesignWidth="800"
			 x:Class="VISA.UI.Views.$className">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="$header"
							   Image="$iconUri"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="140"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>

"@
	foreach ($r in $rows) { $xaml += "                    <RowDefinition Height=`"Auto`"/>`r`n" }
	$xaml += "                </Grid.RowDefinitions>`r`n"

	for ($i = 0; $i -lt $rows.Count; $i++) {
		$label = $rows[$i][0]
		$binding = $rows[$i][1]
		$type = $rows[$i][2]
		$xaml += "                <TextBlock Grid.Row=`"$i`" Grid.Column=`"0`" Text=`"$label`" VerticalAlignment=`"Center`" Margin=`"0,6`"/>`r`n"
		switch ($type) {
			"expr" {
				$xaml += @"
				<expr:ExpressionTextBox Grid.Row="$i" Grid.Column="1" Margin="0,6"
					ScriptText="{Binding $binding, Mode=TwoWay}"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

"@
			}
			"text" {
				$xaml += "                <TextBox Grid.Row=`"$i`" Grid.Column=`"1`" Margin=`"0,6`" Text=`"{Binding $binding, UpdateSourceTrigger=PropertyChanged}`" />`r`n"
			}
			"int" {
				$xaml += "                <TextBox Grid.Row=`"$i`" Grid.Column=`"1`" Margin=`"0,6`" Text=`"{Binding $binding, UpdateSourceTrigger=PropertyChanged}`" />`r`n"
			}
			"bool" {
				$xaml += "                <CheckBox Grid.Row=`"$i`" Grid.Column=`"1`" Margin=`"0,6`" IsChecked=`"{Binding $binding}`" />`r`n"
			}
		}
	}
	$xaml += @"
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
"@
	return $xaml
}

# VisaOpenEditorView
$rows = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("资源字符串:", "ResourceString", "expr"),
	@("打开超时(ms):", "OpenTimeoutMs", "int"),
	@("IO超时(ms):", "IoTimeoutMs", "int"),
	@("终止符:", "Terminator", "text")
)
[IO.File]::WriteAllText("$root\VisaOpenEditorView.xaml", (Make-Xaml "VisaOpenEditorView" "VISA Open" $rows), $utf8)

# VisaCloseEditorView
$rows = @(,@("连接名称:", "ConnectionName", "expr"))
[IO.File]::WriteAllText("$root\VisaCloseEditorView.xaml", (Make-Xaml "VisaCloseEditorView" "VISA Close" $rows), $utf8)

# VisaWriteEditorView
$rows = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("SCPI 命令:", "Command", "expr")
)
[IO.File]::WriteAllText("$root\VisaWriteEditorView.xaml", (Make-Xaml "VisaWriteEditorView" "VISA Write" $rows), $utf8)

# VisaQueryEditorView
$rows = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("SCPI 命令:", "Command", "expr"),
	@("结果变量:", "ResultVariable", "expr"),
	@("去除空白:", "TrimResponse", "bool")
)
[IO.File]::WriteAllText("$root\VisaQueryEditorView.xaml", (Make-Xaml "VisaQueryEditorView" "VISA Query" $rows), $utf8)

# VisaReadEditorView
$rows = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("结果变量:", "ResultVariable", "expr"),
	@("去除空白:", "TrimResponse", "bool")
)
[IO.File]::WriteAllText("$root\VisaReadEditorView.xaml", (Make-Xaml "VisaReadEditorView" "VISA Read" $rows), $utf8)

# VisaWaitOpcEditorView
$rows = @(
	@("连接名称:", "ConnectionName", "expr"),
	@("超时(ms):", "TimeoutMs", "int")
)
[IO.File]::WriteAllText("$root\VisaWaitOpcEditorView.xaml", (Make-Xaml "VisaWaitOpcEditorView" "VISA WaitOPC" $rows), $utf8)

Write-Host "All VISA views regenerated with correct Chinese labels."
