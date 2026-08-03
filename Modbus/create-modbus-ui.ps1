# Create Modbus Plugin UI Views and EditorPlugins
$modbusRoot = $PSScriptRoot

# ============================================================
# Views/ModbusConnectEditorView.xaml
# ============================================================
@'
<UserControl x:Class="Modbus.UI.Views.ModbusConnectEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 d:DesignHeight="500" d:DesignWidth="800"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d">
	<UserControl.Resources>
		<BooleanToVisibilityConverter x:Key="BooleanToVisibilityConverter"/>
	</UserControl.Resources>
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="Modbus Connect">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="130"/>
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
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}"
					ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="TransportType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="1" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding TransportType}">
					<ComboBoxItem Content="TCP"/>
					<ComboBoxItem Content="RTU (Serial)"/>
				</ComboBox>

				<!-- TCP Settings -->
				<TextBlock Grid.Row="2" Grid.Column="0" Text="IP Address:" VerticalAlignment="Center" Margin="0,0,8,8"
						   Visibility="{Binding IsTcp, Converter={StaticResource BooleanToVisibilityConverter}}"/>
				<expr:ExpressionTextBox Grid.Row="2" Grid.Column="1" Margin="0,0,0,8"
					Visibility="{Binding IsTcp, Converter={StaticResource BooleanToVisibilityConverter}}"
					ScriptText="{Binding IpAddress, Mode=TwoWay}"
					ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="3" Grid.Column="0" Text="TCP Port:" VerticalAlignment="Center" Margin="0,0,8,8"
						   Visibility="{Binding IsTcp, Converter={StaticResource BooleanToVisibilityConverter}}"/>
				<TextBox Grid.Row="3" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						 Visibility="{Binding IsTcp, Converter={StaticResource BooleanToVisibilityConverter}}"
						 Text="{Binding TcpPort, UpdateSourceTrigger=PropertyChanged}"/>

				<!-- RTU Settings -->
				<TextBlock Grid.Row="4" Grid.Column="0" Text="PortName:" VerticalAlignment="Center" Margin="0,0,8,8"
						   Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"/>
				<expr:ExpressionTextBox Grid.Row="4" Grid.Column="1" Margin="0,0,0,8"
					Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"
					ScriptText="{Binding PortName, Mode=TwoWay}"
					ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="5" Grid.Column="0" Text="BaudRate:" VerticalAlignment="Center" Margin="0,0,8,8"
						   Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"/>
				<ComboBox Grid.Row="5" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"
						  IsEditable="True" Text="{Binding BaudRate, UpdateSourceTrigger=PropertyChanged}">
					<ComboBoxItem Content="9600"/>
					<ComboBoxItem Content="19200"/>
					<ComboBoxItem Content="38400"/>
					<ComboBoxItem Content="57600"/>
					<ComboBoxItem Content="115200"/>
				</ComboBox>

				<TextBlock Grid.Row="6" Grid.Column="0" Text="Parity:" VerticalAlignment="Center" Margin="0,0,8,8"
						   Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"/>
				<ComboBox Grid.Row="6" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"
						  SelectedIndex="{Binding Parity}">
					<ComboBoxItem Content="None"/>
					<ComboBoxItem Content="Odd"/>
					<ComboBoxItem Content="Even"/>
				</ComboBox>

				<TextBlock Grid.Row="7" Grid.Column="0" Text="StopBits:" VerticalAlignment="Center" Margin="0,0,8,8"
						   Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"/>
				<ComboBox Grid.Row="7" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  Visibility="{Binding IsRtu, Converter={StaticResource BooleanToVisibilityConverter}}"
						  SelectedIndex="{Binding StopBits}">
					<ComboBoxItem Content="None"/>
					<ComboBoxItem Content="One"/>
					<ComboBoxItem Content="Two"/>
				</ComboBox>

				<TextBlock Grid.Row="8" Grid.Column="0" Text="Timeout (ms):" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<TextBox Grid.Row="8" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						 Text="{Binding TimeoutMs, UpdateSourceTrigger=PropertyChanged}"/>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusConnectEditorView.xaml" -Encoding UTF8

# ============================================================
# Views/ModbusConnectEditorView.xaml.cs
# ============================================================
@'
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusConnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusConnectViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusConnectEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusConnectViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusConnectEditorView.xaml.cs" -Encoding UTF8

# ============================================================
# Views/ModbusDisconnectEditorView.xaml
# ============================================================
@'
<UserControl x:Class="Modbus.UI.Views.ModbusDisconnectEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 d:DesignHeight="200" d:DesignWidth="800"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="Modbus Disconnect">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="130"/>
					<ColumnDefinition Width="*"/>
				</Grid.ColumnDefinitions>
				<Grid.RowDefinitions>
					<RowDefinition Height="Auto"/>
				</Grid.RowDefinitions>

				<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ConnectionName, Mode=TwoWay}"
					ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusDisconnectEditorView.xaml" -Encoding UTF8

@'
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusDisconnectEditorView : UserControl, IRefreshableEditor
{
	public ModbusDisconnectViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusDisconnectEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusDisconnectViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusDisconnectEditorView.xaml.cs" -Encoding UTF8

# ============================================================
# Views/ModbusReadEditorView.xaml
# ============================================================
@'
<UserControl x:Class="Modbus.UI.Views.ModbusReadEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 d:DesignHeight="400" d:DesignWidth="800"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="Modbus Read">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="130"/>
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
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="SlaveAddress:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<TextBox Grid.Row="1" Grid.Column="1" Width="100" HorizontalAlignment="Left" Margin="0,0,0,8"
						 Text="{Binding SlaveAddress, UpdateSourceTrigger=PropertyChanged}"/>

				<TextBlock Grid.Row="2" Grid.Column="0" Text="RegisterType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="2" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding RegisterType}">
					<ComboBoxItem Content="Coil (0x)"/>
					<ComboBoxItem Content="Discrete Input (1x)"/>
					<ComboBoxItem Content="Holding Register (4x)"/>
					<ComboBoxItem Content="Input Register (3x)"/>
				</ComboBox>

				<TextBlock Grid.Row="3" Grid.Column="0" Text="StartAddress:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="3" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding StartAddress, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="4" Grid.Column="0" Text="Quantity:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="4" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding Quantity, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="5" Grid.Column="0" Text="DataFormat:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="5" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding DataFormat}">
					<ComboBoxItem Content="UInt16"/>
					<ComboBoxItem Content="Int16"/>
					<ComboBoxItem Content="UInt32 (AB CD)"/>
					<ComboBoxItem Content="Int32 (AB CD)"/>
					<ComboBoxItem Content="Float (AB CD)"/>
					<ComboBoxItem Content="UInt32 (CD AB)"/>
					<ComboBoxItem Content="Int32 (CD AB)"/>
					<ComboBoxItem Content="Float (CD AB)"/>
				</ComboBox>

				<TextBlock Grid.Row="6" Grid.Column="0" Text="ResultVariable:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="6" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding ResultVariable, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusReadEditorView.xaml" -Encoding UTF8

@'
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusReadEditorView : UserControl, IRefreshableEditor
{
	public ModbusReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusReadEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusReadEditorView.xaml.cs" -Encoding UTF8

# ============================================================
# Views/ModbusWriteEditorView.xaml
# ============================================================
@'
<UserControl x:Class="Modbus.UI.Views.ModbusWriteEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 d:DesignHeight="400" d:DesignWidth="800"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="Modbus Write">
			<Grid Margin="16">
				<Grid.ColumnDefinitions>
					<ColumnDefinition Width="130"/>
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
					ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="1" Grid.Column="0" Text="SlaveAddress:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<TextBox Grid.Row="1" Grid.Column="1" Width="100" HorizontalAlignment="Left" Margin="0,0,0,8"
						 Text="{Binding SlaveAddress, UpdateSourceTrigger=PropertyChanged}"/>

				<TextBlock Grid.Row="2" Grid.Column="0" Text="RegisterType:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="2" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding RegisterType}">
					<ComboBoxItem Content="Coil (0x)"/>
					<ComboBoxItem Content="Holding Register (4x)"/>
				</ComboBox>

				<TextBlock Grid.Row="3" Grid.Column="0" Text="StartAddress:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="3" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding StartAddress, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="4" Grid.Column="0" Text="Values:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<expr:ExpressionTextBox Grid.Row="4" Grid.Column="1" Margin="0,0,0,8"
					ScriptText="{Binding Values, Mode=TwoWay}" ExpectedResultType="System.String"
					SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
					EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />

				<TextBlock Grid.Row="5" Grid.Column="0" Text="DataFormat:" VerticalAlignment="Center" Margin="0,0,8,8"/>
				<ComboBox Grid.Row="5" Grid.Column="1" Width="200" HorizontalAlignment="Left" Margin="0,0,0,8"
						  SelectedIndex="{Binding DataFormat}">
					<ComboBoxItem Content="UInt16"/>
					<ComboBoxItem Content="Int16"/>
					<ComboBoxItem Content="UInt32 (AB CD)"/>
					<ComboBoxItem Content="Int32 (AB CD)"/>
					<ComboBoxItem Content="Float (AB CD)"/>
					<ComboBoxItem Content="UInt32 (CD AB)"/>
					<ComboBoxItem Content="Int32 (CD AB)"/>
					<ComboBoxItem Content="Float (CD AB)"/>
				</ComboBox>
			</Grid>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusWriteEditorView.xaml" -Encoding UTF8

@'
using System.Windows.Controls;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusWriteEditorView.xaml.cs" -Encoding UTF8

# ============================================================
# Views/ModbusBatchReadEditorView.xaml
# ============================================================
@'
<UserControl x:Class="Modbus.UI.Views.ModbusBatchReadEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 d:DesignHeight="500" d:DesignWidth="800"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="Modbus BatchRead">
			<DockPanel Margin="16">
				<Grid DockPanel.Dock="Top" Margin="0,0,0,8">
					<Grid.ColumnDefinitions>
						<ColumnDefinition Width="130"/>
						<ColumnDefinition Width="*"/>
					</Grid.ColumnDefinitions>
					<Grid.RowDefinitions>
						<RowDefinition Height="Auto"/>
						<RowDefinition Height="Auto"/>
					</Grid.RowDefinitions>
					<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
					<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
						ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="System.String"
						SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
						EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
					<TextBlock Grid.Row="1" Grid.Column="0" Text="Interval (ms):" VerticalAlignment="Center" Margin="0,0,8,8"/>
					<TextBox Grid.Row="1" Grid.Column="1" Width="100" HorizontalAlignment="Left" Margin="0,0,0,8"
							 Text="{Binding IntervalMs, UpdateSourceTrigger=PropertyChanged}"/>
				</Grid>
				<StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,8,0,0">
					<Button Content="Add Item" Padding="12,4" Click="AddItem_Click"/>
					<Button Content="Remove Selected" Padding="12,4" Margin="8,0,0,0" Click="RemoveItem_Click"/>
				</StackPanel>
				<DataGrid x:Name="ItemsGrid" ItemsSource="{Binding Items}" AutoGenerateColumns="False"
						  CanUserAddRows="False" CanUserDeleteRows="False">
					<DataGrid.Columns>
						<DataGridTextColumn Header="Slave" Binding="{Binding SlaveAddress}" Width="60"/>
						<DataGridComboBoxColumn Header="RegisterType" SelectedValueBinding="{Binding RegisterType}" Width="130">
							<DataGridComboBoxColumn.ItemsSource>
								<x:Array Type="sys:String" xmlns:sys="clr-namespace:System;assembly=mscorlib">
									<sys:String>Coil</sys:String>
									<sys:String>DiscreteInput</sys:String>
									<sys:String>HoldingRegister</sys:String>
									<sys:String>InputRegister</sys:String>
								</x:Array>
							</DataGridComboBoxColumn.ItemsSource>
						</DataGridComboBoxColumn>
						<DataGridTextColumn Header="StartAddr" Binding="{Binding StartAddress}" Width="80"/>
						<DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}" Width="70"/>
						<DataGridTextColumn Header="Variable" Binding="{Binding ResultVariable}" Width="*"/>
					</DataGrid.Columns>
				</DataGrid>
			</DockPanel>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusBatchReadEditorView.xaml" -Encoding UTF8

@'
using System.Windows;
using System.Windows.Controls;
using Modbus.Models;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusBatchReadEditorView : UserControl, IRefreshableEditor
{
	public ModbusBatchReadViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusBatchReadEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusBatchReadViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}

	private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel.AddItem();
	private void RemoveItem_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsGrid.SelectedItem is ModbusBatchItem item) ViewModel.RemoveItem(item);
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusBatchReadEditorView.xaml.cs" -Encoding UTF8

# ============================================================
# Views/ModbusBatchWriteEditorView.xaml
# ============================================================
@'
<UserControl x:Class="Modbus.UI.Views.ModbusBatchWriteEditorView"
			 xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
			 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
			 xmlns:syncfusion="http://schemas.syncfusion.com/wpf"
			 xmlns:syncfusionskin="clr-namespace:Syncfusion.SfSkinManager;assembly=Syncfusion.SfSkinManager.WPF"
			 xmlns:expr="clr-namespace:ExpressionTextBox;assembly=ExpressionTextBox"
			 syncfusionskin:SfSkinManager.Theme="{syncfusionskin:SkinManagerExtension ThemeName=Windows11Light}"
			 d:DesignHeight="500" d:DesignWidth="800"
			 xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
			 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" mc:Ignorable="d">
	<syncfusion:TabControlExt CloseButtonType="Hide" AllowDragDrop="False" EnableLabelEdit="False">
		<syncfusion:TabItemExt Header="Modbus BatchWrite">
			<DockPanel Margin="16">
				<Grid DockPanel.Dock="Top" Margin="0,0,0,8">
					<Grid.ColumnDefinitions>
						<ColumnDefinition Width="130"/>
						<ColumnDefinition Width="*"/>
					</Grid.ColumnDefinitions>
					<Grid.RowDefinitions>
						<RowDefinition Height="Auto"/>
						<RowDefinition Height="Auto"/>
					</Grid.RowDefinitions>
					<TextBlock Grid.Row="0" Grid.Column="0" Text="ConnectionName:" VerticalAlignment="Center" Margin="0,0,8,8"/>
					<expr:ExpressionTextBox Grid.Row="0" Grid.Column="1" Margin="0,0,0,8"
						ScriptText="{Binding ConnectionName, Mode=TwoWay}" ExpectedResultType="System.String"
						SequenceFile="{Binding SequenceFile, RelativeSource={RelativeSource AncestorType=UserControl}}"
						EditPosition="{Binding EditPosition, RelativeSource={RelativeSource AncestorType=UserControl}}" />
					<TextBlock Grid.Row="1" Grid.Column="0" Text="Interval (ms):" VerticalAlignment="Center" Margin="0,0,8,8"/>
					<TextBox Grid.Row="1" Grid.Column="1" Width="100" HorizontalAlignment="Left" Margin="0,0,0,8"
							 Text="{Binding IntervalMs, UpdateSourceTrigger=PropertyChanged}"/>
				</Grid>
				<StackPanel DockPanel.Dock="Bottom" Orientation="Horizontal" Margin="0,8,0,0">
					<Button Content="Add Item" Padding="12,4" Click="AddItem_Click"/>
					<Button Content="Remove Selected" Padding="12,4" Margin="8,0,0,0" Click="RemoveItem_Click"/>
				</StackPanel>
				<DataGrid x:Name="ItemsGrid" ItemsSource="{Binding Items}" AutoGenerateColumns="False"
						  CanUserAddRows="False" CanUserDeleteRows="False">
					<DataGrid.Columns>
						<DataGridTextColumn Header="Slave" Binding="{Binding SlaveAddress}" Width="60"/>
						<DataGridTextColumn Header="StartAddr" Binding="{Binding StartAddress}" Width="80"/>
						<DataGridTextColumn Header="Values" Binding="{Binding Values}" Width="*"/>
					</DataGrid.Columns>
				</DataGrid>
			</DockPanel>
		</syncfusion:TabItemExt>
	</syncfusion:TabControlExt>
</UserControl>
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusBatchWriteEditorView.xaml" -Encoding UTF8

@'
using System.Windows;
using System.Windows.Controls;
using Modbus.Models;
using Modbus.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Views;

public partial class ModbusBatchWriteEditorView : UserControl, IRefreshableEditor
{
	public ModbusBatchWriteViewModel ViewModel { get; }
	public Action<string, Action>? ExecuteCommand { get; set; }
	public SequenceFile? SequenceFile { get; set; }
	public EditPosition? EditPosition { get; set; }

	public ModbusBatchWriteEditorView()
	{
		InitializeComponent();
		ViewModel = new ModbusBatchWriteViewModel();
		DataContext = ViewModel;
	}

	public void RefreshFromStep(Step step)
	{
		ViewModel.AttachSerializer(new ModbusBatchWritePlugin().CreateSerializer());
		ViewModel.AttachStep(step);
	}

	private void AddItem_Click(object sender, RoutedEventArgs e) => ViewModel.AddItem();
	private void RemoveItem_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsGrid.SelectedItem is ModbusBatchWriteItem item) ViewModel.RemoveItem(item);
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\Views\ModbusBatchWriteEditorView.xaml.cs" -Encoding UTF8

# ============================================================
# EditorPlugins
# ============================================================
@'
using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.UI;

public sealed class ModbusConnectEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusConnect";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusConnectEditorView();
		view.ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusConnectSetting)new ModbusConnectPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_001", "连接标识名不能为空"));
		if (s.TransportType == ModbusTransportType.TCP && string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("MB_002", "IP 地址不能为空"));
		if (s.TransportType == ModbusTransportType.RTU && string.IsNullOrWhiteSpace(s.PortName))
			errors.Add(StepSettingError.Error("MB_003", "串口名称不能为空"));
		return errors;
	}
}

public sealed class ModbusDisconnectEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusDisconnect";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusDisconnectEditorView();
		view.ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusDisconnectSetting)new ModbusDisconnectPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_010", "连接标识名不能为空"));
		return errors;
	}
}

public sealed class ModbusReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusRead";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusReadEditorView();
		view.ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusReadSetting)new ModbusReadPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_020", "连接标识名不能为空"));
		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("MB_021", "结果变量名不能为空"));
		return errors;
	}
}

public sealed class ModbusWriteEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusWrite";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusWriteEditorView();
		view.ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusWriteSetting)new ModbusWritePlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_030", "连接标识名不能为空"));
		if (string.IsNullOrWhiteSpace(s.Values))
			errors.Add(StepSettingError.Error("MB_031", "写入值不能为空"));
		return errors;
	}
}

public sealed class ModbusBatchReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusBatchRead";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusBatchReadEditorView();
		view.ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusBatchReadSetting)new ModbusBatchReadPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_040", "连接标识名不能为空"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_041", "批量读取列表为空"));
		return errors;
	}
}

public sealed class ModbusBatchWriteEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusBatchWrite";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusBatchWriteEditorView();
		view.ViewModel.AttachSerializer(new ModbusBatchWritePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusBatchWriteSetting)new ModbusBatchWritePlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_050", "连接标识名不能为空"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_051", "批量写入列表为空"));
		return errors;
	}
}
'@ | Set-Content "$modbusRoot\ModbusPlugin.UI\ModbusEditorPlugins.cs" -Encoding UTF8

Write-Host "Modbus UI layer files created successfully!" -ForegroundColor Green
