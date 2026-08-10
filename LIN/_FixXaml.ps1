$utf8 = New-Object System.Text.UTF8Encoding($false)
$root = "D:\xTestPlatform-PluginDev\LIN\LinPlugin.UI\Views"

# ===== LinOpenEditorView.xaml =====
$linOpen = @'
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

				<TextBlock Grid.Row="0" Grid.Column="0" Text="AdapterType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="0" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding AdapterType, Converter={StaticResource EnumToIntConverter}}">
					<ComboBoxItem Content="NI XNET"/>
					<ComboBoxItem Content="PEAK PLIN"/>
					<ComboBoxItem Content="Vector LIN"/>
					<ComboBoxItem Content="IXXAT"/>
				</ComboBox>

				<TextBlock Grid.Row="1" Grid.Column="0" Text="Channel:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="1" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding Channel, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="2" Grid.Column="0" Text="BaudRate:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<syncfusion:IntegerTextBox Grid.Row="2" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
					Value="{Binding BaudRate, Mode=TwoWay}" MinValue="1"/>

				<TextBlock Grid.Row="3" Grid.Column="0" Text="LinVersion:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="3" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding LinVersion, Converter={StaticResource EnumToIntConverter}}">
					<ComboBoxItem Content="LIN 1.x"/>
					<ComboBoxItem Content="LIN 2.x"/>
				</ComboBox>

				<TextBlock Grid.Row="4" Grid.Column="0" Text="IsMaster:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<CheckBox Grid.Row="4" Grid.Column="1" IsChecked="{Binding IsMaster}" Margin="0,0,0,8" Content="主节点"/>

				<TextBlock Grid.Row="5" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="5" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$root\LinOpenEditorView.xaml", $linOpen, $utf8)
Write-Host "LinOpenEditorView.xaml done"

# ===== LinCyclicSendStartEditorView.xaml =====
$cyclicStart = @'
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
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="*"/>
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

				<TextBlock Grid.Row="3" Grid.Column="0" Text="Frames:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<StackPanel Grid.Row="3" Grid.Column="1" Orientation="Horizontal" Margin="0,0,0,8">
					<Button Content="Add" Width="60" Margin="0,0,8,0" Click="OnAddFrame"/>
					<Button Content="Remove" Width="60" Click="OnRemoveFrame"/>
				</StackPanel>

				<DataGrid Grid.Row="4" Grid.Column="0" Grid.ColumnSpan="2"
						  ItemsSource="{Binding Frames}"
						  AutoGenerateColumns="False" CanUserAddRows="False" CanUserDeleteRows="False"
						  Margin="0,0,0,8" MinHeight="120">
					<DataGrid.Columns>
						<DataGridCheckBoxColumn Header="Enabled" Binding="{Binding Enabled, UpdateSourceTrigger=PropertyChanged}" Width="60"/>
						<DataGridTextColumn Header="FrameId" Binding="{Binding FrameId, UpdateSourceTrigger=PropertyChanged}" Width="80"/>
						<DataGridTextColumn Header="Data (Hex)" Binding="{Binding Data, UpdateSourceTrigger=PropertyChanged}" Width="*"/>
						<DataGridTextColumn Header="Cycle (ms)" Binding="{Binding CycleTimeMs, UpdateSourceTrigger=PropertyChanged}" Width="80"/>
						<DataGridComboBoxColumn Header="Checksum" Width="90"
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
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@
[IO.File]::WriteAllText("$root\LinCyclicSendStartEditorView.xaml", $cyclicStart, $utf8)
Write-Host "LinCyclicSendStartEditorView.xaml done"

Write-Host "All fixed."
