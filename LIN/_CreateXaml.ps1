$utf8 = New-Object System.Text.UTF8Encoding($false)
$base = "D:\xTestPlatform-PluginDev\LIN\LinPlugin.UI\Views"
New-Item -ItemType Directory -Force -Path $base | Out-Null

# ---------- LinOpenEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinOpenEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 xmlns:local="clr-namespace:LIN.UI.Converters"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="450" d:DesignWidth="800">
	<UserControl.Resources>
		<local:EnumToIntConverter x:Key="EnumToIntConverter"/>
	</UserControl.Resources>
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN Open"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}"
					ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="Channel:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding Channel, Mode=TwoWay}"
					ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="2" Grid.Column="0" Text="AdapterType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="2" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding AdapterType, Converter={StaticResource EnumToIntConverter}}">
					<ComboBoxItem Content="NI"/>
					<ComboBoxItem Content="PEAK"/>
					<ComboBoxItem Content="Vector"/>
					<ComboBoxItem Content="IXXAT"/>
				</ComboBox>

				<TextBlock Grid.Row="3" Grid.Column="0" Text="BaudRate:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="3" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  IsEditable="True"
						  Text="{Binding BaudRate, Mode=TwoWay}">
					<ComboBoxItem Content="2400"/>
					<ComboBoxItem Content="9600"/>
					<ComboBoxItem Content="10400"/>
					<ComboBoxItem Content="19200"/>
				</ComboBox>

				<TextBlock Grid.Row="4" Grid.Column="0" Text="LinVersion:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="4" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding LinVersion, Converter={StaticResource EnumToIntConverter}}">
					<ComboBoxItem Content="LIN 1.x"/>
					<ComboBoxItem Content="LIN 2.x"/>
				</ComboBox>

				<TextBlock Grid.Row="5" Grid.Column="0" Text="IsMaster:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<CheckBox Grid.Row="5" Grid.Column="1" IsChecked="{Binding IsMaster}" Margin="0,0,0,8" Content="主节点模式"/>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinOpenEditorView.xaml", $content, $utf8)

# ---------- LinCloseEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinCloseEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="200" d:DesignWidth="800">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN Close"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>
				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}"
					ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinCloseEditorView.xaml", $content, $utf8)

# ---------- LinWriteEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinWriteEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 xmlns:local="clr-namespace:LIN.UI.Converters"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="450" d:DesignWidth="800">
	<UserControl.Resources>
		<local:EnumToIntConverter x:Key="EnumToIntConverter"/>
	</UserControl.Resources>
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN Write"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="FrameId (0-63):" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding FrameId, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="2" Grid.Column="0" Text="Data (Hex):" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="2" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding Data, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="3" Grid.Column="0" Text="ChecksumType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="3" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding ChecksumType, Converter={StaticResource EnumToIntConverter}}">
					<ComboBoxItem Content="Classic"/>
					<ComboBoxItem Content="Enhanced"/>
				</ComboBox>

				<TextBlock Grid.Row="4" Grid.Column="0" Text="EnableLog:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<CheckBox Grid.Row="4" Grid.Column="1" IsChecked="{Binding EnableLog}" Margin="0,0,0,8"/>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinWriteEditorView.xaml", $content, $utf8)

# ---------- LinReadEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinReadEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="450" d:DesignWidth="800">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN Read"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="FilterFrameId:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding FilterFrameId, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="2" Grid.Column="0" Text="ReadTimeoutMs:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<syncfusion:IntegerTextBox Grid.Row="2" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
					Value="{Binding ReadTimeoutMs, Mode=TwoWay}" MinValue="1"/>

				<TextBlock Grid.Row="3" Grid.Column="0" Text="ResultVariable:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<TextBox Grid.Row="3" Grid.Column="1" Margin="0,0,0,8"
						 Text="{Binding ResultVariable, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

				<TextBlock Grid.Row="4" Grid.Column="0" Text="IdVariable:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<TextBox Grid.Row="4" Grid.Column="1" Margin="0,0,0,8"
						 Text="{Binding IdVariable, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

				<TextBlock Grid.Row="5" Grid.Column="0" Text="EnableLog:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<CheckBox Grid.Row="5" Grid.Column="1" IsChecked="{Binding EnableLog}" Margin="0,0,0,8"/>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinReadEditorView.xaml", $content, $utf8)

# ---------- LinWriteReadEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinWriteReadEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 xmlns:local="clr-namespace:LIN.UI.Converters"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="450" d:DesignWidth="800">
	<UserControl.Resources>
		<local:EnumToIntConverter x:Key="EnumToIntConverter"/>
	</UserControl.Resources>
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN WriteRead"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="FrameId (0-63):" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding FrameId, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="2" Grid.Column="0" Text="Data (Hex):" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="2" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding Data, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="3" Grid.Column="0" Text="ChecksumType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="3" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding ChecksumType, Converter={StaticResource EnumToIntConverter}}">
					<ComboBoxItem Content="Classic"/>
					<ComboBoxItem Content="Enhanced"/>
				</ComboBox>

				<TextBlock Grid.Row="4" Grid.Column="0" Text="ResponseTimeoutMs:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<syncfusion:IntegerTextBox Grid.Row="4" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
					Value="{Binding ResponseTimeoutMs, Mode=TwoWay}" MinValue="1"/>

				<TextBlock Grid.Row="5" Grid.Column="0" Text="ResultVariable:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<TextBox Grid.Row="5" Grid.Column="1" Margin="0,0,0,8"
						 Text="{Binding ResultVariable, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

				<TextBlock Grid.Row="6" Grid.Column="0" Text="EnableLog:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<CheckBox Grid.Row="6" Grid.Column="1" IsChecked="{Binding EnableLog}" Margin="0,0,0,8"/>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinWriteReadEditorView.xaml", $content, $utf8)

# ---------- LinCyclicSendStartEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinCyclicSendStartEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 xmlns:local="clr-namespace:LIN.UI.Converters"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="500" d:DesignWidth="800">
	<UserControl.Resources>
		<local:EnumToIntConverter x:Key="EnumToIntConverter"/>
	</UserControl.Resources>
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN CyclicSendStart"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="*"/>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="TaskName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding TaskName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="2" Grid.Column="0" Text="EnableLog:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<CheckBox Grid.Row="2" Grid.Column="1" IsChecked="{Binding EnableLog}" Margin="0,0,0,8"/>

				<DataGrid Grid.Row="3" Grid.Column="0" Grid.ColumnSpan="2"
						  ItemsSource="{Binding Frames}"
						  AutoGenerateColumns="False" CanUserAddRows="False"
						  Margin="0,8,0,8" MinHeight="120">
					<DataGrid.Columns>
						<DataGridCheckBoxColumn Header="启用" Binding="{Binding Enabled, UpdateSourceTrigger=PropertyChanged}" Width="50"/>
						<DataGridTextColumn Header="FrameId" Binding="{Binding FrameId, UpdateSourceTrigger=PropertyChanged}" Width="80"/>
						<DataGridTextColumn Header="Data (Hex)" Binding="{Binding Data, UpdateSourceTrigger=PropertyChanged}" Width="*"/>
						<DataGridTextColumn Header="周期(ms)" Binding="{Binding CycleTimeMs, UpdateSourceTrigger=PropertyChanged}" Width="80"/>
						<DataGridComboBoxColumn Header="校验" Width="90"
							SelectedIndexBinding="{Binding ChecksumType, Converter={StaticResource EnumToIntConverter}, UpdateSourceTrigger=PropertyChanged}">
							<DataGridComboBoxColumn.ItemsSource>
								<x:Array Type="sys:String" xmlns:sys="clr-namespace:System;assembly=mscorlib">
									<sys:String>Classic</sys:String>
									<sys:String>Enhanced</sys:String>
								</x:Array>
							</DataGridComboBoxColumn.ItemsSource>
						</DataGridComboBoxColumn>
					</DataGrid.Columns>
				</DataGrid>

				<StackPanel Grid.Row="4" Grid.Column="0" Grid.ColumnSpan="2" Orientation="Horizontal">
					<Button Content="添加帧" Width="80" Margin="0,0,8,0"
							Command="{Binding AddFrameCommand}"/>
					<Button Content="删除选中" Width="80"
							Command="{Binding RemoveFrameCommand}"
							CommandParameter="{Binding SelectedFrame}"/>
				</StackPanel>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinCyclicSendStartEditorView.xaml", $content, $utf8)

# ---------- LinCyclicSendStopEditorView.xaml ----------
$content = @'
<UserControl x:Class="LIN.UI.Views.LinCyclicSendStopEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 mc:Ignorable="d" d:DesignHeight="200" d:DesignWidth="800">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="LIN CyclicSendStop"
							   Image="pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png"
							   ImageHeight="20" ImageWidth="20">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="150"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>
				<TextBlock Grid.Row="0" Grid.Column="0" Text="TaskName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding TaskName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$base\LinCyclicSendStopEditorView.xaml", $content, $utf8)

Write-Host "All XAML files created successfully"
