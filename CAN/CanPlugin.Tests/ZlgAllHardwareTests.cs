using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CAN.Adapters;
using CAN.Adapters.Zlg;
using CAN.Models;
using CAN.UI.Views;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class ZlgAllHardwareTests
{
    private sealed class Driver : ZlgIo
    {
        public readonly List<string> Calls = [];
        public bool Connected, FailClose, FailLogout, FailInit, LoginLeavesDisconnected, ThrowSecret, RemainsConnectedAfterLogout;
        public uint LoginStatus;
        public int Logins, Logouts;
        public override IntPtr OpenDevice(uint type, uint index) { Calls.Add($"Open:{type}/{index}"); return new(10); }
        public override bool SetProperty(IntPtr device, string path, string value) { Calls.Add($"Set:{path}={value}"); return true; }
        public override IntPtr InitCan(IntPtr device, uint channel, ref ZlgApi.ZCAN_CHANNEL_INIT_CONFIG config) { Calls.Add($"Init:{channel}/{config.can_type}"); return FailInit ? IntPtr.Zero : new(11); }
        public override uint StartCan(IntPtr channel) { Calls.Add("Start"); return 1; }
        public override uint ResetCan(IntPtr channel) { Calls.Add("Reset"); return 1; }
        public override uint CloseDevice(IntPtr device) { Calls.Add("Close"); return FailClose ? 0u : 1u; }
        public override uint Receive(IntPtr channel, ZlgApi.ZCAN_Receive_Data[] output, int waitMs) => 0;
        public override uint ReceiveFd(IntPtr channel, ZlgApi.ZCAN_ReceiveFD_Data[] output, int waitMs) => 0;
        public override uint CloudIsConnected() => Connected ? 1u : 0u;
        public override void CloudSetServerInfo(string http, ushort hp, string auth, ushort ap) => Calls.Add($"Server:{http}:{hp}/{auth}:{ap}");
        public override uint CloudConnect(string user, string password) { Logins++; Connected = !LoginLeavesDisconnected; if (ThrowSecret) throw new Exception(password); return LoginStatus; }
        public override uint CloudDisconnect() { Logouts++; if (FailLogout) return 1; Connected = RemainsConnectedAfterLogout; return 0; }
        public override void ValidateCloudChannel(uint device, uint channel, bool fd) => Calls.Add($"CloudChannel:{device}/{channel}/{fd}");
    }

    private static CanAdapterConfig Config(string type, bool fd = true) => new()
    {
        Channel = $"{type}/0/0", Protocol = fd ? CanProtocolType.FD : CanProtocolType.Classic,
        Zlg = new() { RemoteAddress = "192.168.1.100", RemotePort = 4001, LocalPort = 4002 }
    };
    private static CanAdapterConfig CloudConfig(int index = 7) => new()
    {
        Channel = $"CLOUD/{index}/0", Protocol = CanProtocolType.FD,
        Zlg = new() { CloudUserName = "operator", CloudPassword = "test-secret-not-for-logs", CloudHttpServer = "http.test.invalid", CloudAuthServer = "auth.test.invalid" }
    };

    [Theory]
    [InlineData("CANFDNET_200U_TCP", false)]
    [InlineData("CANFDWIFI_100U_UDP", true)]
    [InlineData("CANFDDTU_800EWGR_TCP", false)]
    public void Network_configures_connection_before_init_and_does_not_claim_USB_bus_settings(string model, bool udp)
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io); var config = Config(model);
        adapter.Open(config);
        int init = io.Calls.FindIndex(c => c.StartsWith("Init:"));
        Assert.InRange(io.Calls.IndexOf("Set:0/ip=192.168.1.100"), 0, init - 1);
        Assert.Contains("Set:0/work_port=4001", io.Calls);
        Assert.Equal(udp, io.Calls.Contains("Set:0/local_port=4002"));
        Assert.Equal(!udp, io.Calls.Contains("Set:0/work_mode=0"));
        Assert.DoesNotContain(io.Calls, c => c.Contains("baud_rate") || c.Contains("resistance") || c.Contains("canfd_standard"));
        Assert.Null(config.AppliedArbitrationBitRate); Assert.Contains("未修改", config.AppliedConfigurationDescription);
    }

    [Fact]
    public void TCP_server_only_sets_local_listener_and_external_termination_cannot_be_falsely_enabled()
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io); var config = Config("CANETTCP", false);
        config.Zlg = new() { NetworkWorkMode = 1, LocalPort = 5001 };
        adapter.Open(config); Assert.Contains("Set:0/work_mode=1", io.Calls); Assert.Contains("Set:0/local_port=5001", io.Calls);
        Assert.DoesNotContain(io.Calls, c => c.StartsWith("Set:0/ip=")); adapter.Close();
        config.EnableTermination = true;
        Assert.Throws<ArgumentException>(() => adapter.Open(config)); Assert.Single(io.Calls, c => c.StartsWith("Open:"));
    }

    [Theory]
    [InlineData("CANDTU_100UR", false, "baud_rate", "internal_resistance")]
    [InlineData("CANDTU_200UR", false, "baud_rate", "initenal_resistance")]
    [InlineData("ZPSCANFD_TCP", true, "canfd_abit_baud_rate", "initenal_resistance")]
    [InlineData("ZPSCANFD_USB", true, "canfd_abit_baud_rate", "initenal_resistance")]
    public void Post_init_models_apply_their_own_bitrate_and_termination_properties(string model, bool fd, string rate, string termination)
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io); var config = Config(model, fd); config.EnableTermination = true;
        adapter.Open(config);
        int init = io.Calls.FindIndex(c => c.StartsWith("Init:")), start = io.Calls.IndexOf("Start");
        Assert.InRange(io.Calls.IndexOf($"Set:0/{rate}=500000"), init + 1, start - 1);
        Assert.InRange(io.Calls.IndexOf($"Set:0/{termination}=1"), init + 1, start - 1);
        Assert.Null(config.AppliedArbitrationSamplePoint); Assert.Equal(500000, config.AppliedArbitrationBitRate);
    }

    [Fact]
    public void Scope_uses_pre_init_properties_and_CanFdCom_uses_a_configuration_file()
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io); var scope = Config("CANSCOPE", false); scope.EnableTermination = true;
        adapter.Open(scope); int init = io.Calls.FindIndex(c => c.StartsWith("Init:"));
        Assert.InRange(io.Calls.IndexOf("Set:0/baud_rate=500000"), 0, init - 1);
        Assert.InRange(io.Calls.IndexOf("Set:0/initenal_resistance=1"), 0, init - 1); adapter.Close();
        string file = Path.GetTempFileName();
        try
        {
            io.Calls.Clear(); var config = Config("CANFDCOM_100IE"); config.Zlg = new() { ConfigurationPath = file };
            adapter.Open(config); init = io.Calls.FindIndex(c => c.StartsWith("Init:"));
            Assert.InRange(io.Calls.IndexOf($"Set:0/file_path={file}"), 0, init - 1);
            Assert.DoesNotContain(io.Calls, c => c.Contains("baud_rate"));
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData("USBCANFD_400U")]
    [InlineData("USBCANFD_800U")]
    [InlineData("CANFDDTU_300U")]
    [InlineData("BG004")]
    public void Additional_USB_FD_models_configure_standard_rates_without_inventing_sample_points(string model)
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io); var config = Config(model);
        adapter.Open(config); Assert.Contains("Set:0/canfd_abit_baud_rate=500000", io.Calls);
        Assert.Contains("Set:0/canfd_dbit_baud_rate=2000000", io.Calls);
        Assert.Null(config.AppliedDataSamplePoint); Assert.Null(config.AppliedArbitrationSamplePoint);
    }

    [Fact]
    public void Input_validation_happens_before_native_device_open()
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io); var config = Config("CANFDNET_TCP");
        config.Zlg = new() { RemoteAddress = "192.168.1.1\0untrusted" };
        Assert.Throws<ArgumentException>(() => adapter.Open(config)); Assert.Empty(io.Calls); Assert.True(adapter.HandlesReleased);
        config = Config("CANFDCOM_100IE"); config.Zlg = new() { ConfigurationPath = "x\0y" };
        Assert.Throws<ArgumentException>(() => adapter.Open(config)); Assert.Empty(io.Calls);
    }

    [Fact]
    public void Cloud_shares_owned_identity_and_only_last_handle_close_logs_out()
    {
        var io = new Driver(); using var first = new ZlgAdapter(io); using var second = new ZlgAdapter(io);
        first.Open(CloudConfig()); second.Open(CloudConfig(11));
        Assert.Equal(1, io.Logins); Assert.Contains("CloudChannel:11/0/True", io.Calls);
        first.Close(); Assert.Equal(0, io.Logouts); Assert.True(io.Connected);
        second.Close(); Assert.Equal(1, io.Logouts); Assert.False(io.Connected);
        Assert.True(first.HandlesReleased); Assert.True(second.HandlesReleased);
    }

    [Fact]
    public void Cloud_never_adopts_an_external_login_or_disconnects_it()
    {
        var io = new Driver { Connected = true }; using var adapter = new ZlgAdapter(io);
        Assert.Contains("不能接管", Assert.Throws<InvalidOperationException>(() => adapter.Open(CloudConfig())).Message);
        Assert.Equal(0, io.Logins); Assert.Equal(0, io.Logouts); Assert.True(adapter.HandlesReleased);
    }

    [Fact]
    public void Cloud_device_close_failure_keeps_login_and_logout_failure_keeps_lease_for_retry()
    {
        var io = new Driver(); var adapter = new ZlgAdapter(io);
        try
        {
            adapter.Open(CloudConfig()); io.FailClose = true;
            Assert.ThrowsAny<Exception>(adapter.Close); Assert.Equal(0, io.Logouts); Assert.False(adapter.HandlesReleased);
            io.FailClose = false; io.FailLogout = true;
            Assert.Throws<InvalidOperationException>(adapter.Close); Assert.False(adapter.HandlesReleased);
            Assert.Throws<InvalidOperationException>(() => adapter.Open(CloudConfig()));
            using (var later = new ZlgAdapter(io))
            {
                Assert.Throws<InvalidOperationException>(() => later.Open(CloudConfig(8)));
                Assert.True(later.HandlesReleased); Assert.Equal(1, io.Logins);
            }
            io.FailLogout = false; adapter.Close(); Assert.True(adapter.HandlesReleased); Assert.False(io.Connected);
        }
        finally { io.FailClose = io.FailLogout = false; adapter.Close(); }
    }

    [Fact]
    public void Failed_cloud_open_cleans_its_own_login_and_does_not_log_password()
    {
        var io = new Driver { LoginStatus = 3 }; using var adapter = new ZlgAdapter(io);
        string error = Assert.Throws<InvalidOperationException>(() => adapter.Open(CloudConfig())).ToString();
        Assert.DoesNotContain("test-secret-not-for-logs", error); Assert.Equal(1, io.Logouts); Assert.True(adapter.HandlesReleased);
        io.LoginStatus = 0; io.FailInit = true;
        Assert.Throws<InvalidOperationException>(() => adapter.Open(CloudConfig())); Assert.Equal(2, io.Logouts); Assert.True(adapter.HandlesReleased);
    }

    [Fact]
    public void Cloud_empty_endpoints_cannot_inherit_a_previous_custom_server()
    {
        var io = new Driver(); using var adapter = new ZlgAdapter(io);
        adapter.Open(CloudConfig()); adapter.Close(); var empty = CloudConfig();
        empty.Zlg = new() { CloudUserName = "other", CloudPassword = "new-secret" };
        Assert.Throws<ArgumentException>(() => adapter.Open(empty)); Assert.Equal(1, io.Logins);
    }

    [Fact]
    public void Failed_login_still_disconnects_even_when_connected_state_is_false_and_hides_native_secret_errors()
    {
        var io = new Driver { LoginLeavesDisconnected = true, LoginStatus = 3 }; using var adapter = new ZlgAdapter(io);
        Assert.Throws<InvalidOperationException>(() => adapter.Open(CloudConfig())); Assert.Equal(1, io.Logouts);
        io.ThrowSecret = true;
        string error = Assert.Throws<InvalidOperationException>(() => adapter.Open(CloudConfig())).ToString();
        Assert.DoesNotContain("test-secret-not-for-logs", error); Assert.Equal(2, io.Logouts); Assert.True(adapter.HandlesReleased);
    }

    [Fact]
    public void Successful_disconnect_is_not_repeated_when_later_state_check_requires_a_retry()
    {
        var io = new Driver { RemainsConnectedAfterLogout = true }; var adapter = new ZlgAdapter(io);
        try
        {
            adapter.Open(CloudConfig()); Assert.Throws<InvalidOperationException>(adapter.Close);
            Assert.False(adapter.HandlesReleased); Assert.Equal(1, io.Logouts);
            io.Connected = false; adapter.Close(); Assert.True(adapter.HandlesReleased); Assert.Equal(1, io.Logouts);
        }
        finally { io.Connected = false; adapter.Close(); }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 4001)]
    public async Task Dynamic_address_does_not_hide_invalid_static_network_parameters(int role, int port)
    {
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.ZLG, Channel = "\"CANFDNET_TCP/0/0\"",
            ZlgRemoteAddress = "FileGlobals.Address", ZlgNetworkWorkMode = role, ZlgRemotePort = port };
        var errors = await new CAN.UI.CanOpenEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanOpenPlugin(), setting));
        Assert.Contains(errors, e => e.Code == "CAN_ZLG_CONFIG");
    }

    [Fact]
    public async Task Existing_USB_Classic_arbitration_sample_point_is_not_forced_to_the_FD_value()
    {
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.ZLG, Channel = "\"USBCANFD_200U/0/0\"", ArbitrationSamplePoint = 75 };
        var errors = await new CAN.UI.CanOpenEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanOpenPlugin(), setting));
        Assert.DoesNotContain(errors, e => e.Code == "CAN_009");
    }

    [Fact]
    public async Task Dynamic_model_does_not_assume_USB_FD_sample_point_constraints()
    {
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.ZLG, Channel = "FileGlobals.Channel",
            Protocol = CanProtocolType.FD, DataSamplePoint = 75, DataBitRate = 2000000 };
        var errors = await new CAN.UI.CanOpenEditorPlugin().ValidateWithContextAsync(Round2XcpContractTests.Context(new CanOpenPlugin(), setting));
        Assert.DoesNotContain(errors, e => e.Code == "CAN_012");
        Assert.Contains(errors, e => e.Code == "CAN_W04");
    }

    [Fact]
    public void Cloud_native_layout_matches_bundled_x64_header_size_t_and_natural_alignment()
    {
        Assert.Equal(8, IntPtr.Size); Assert.Equal(4, Marshal.SizeOf<ZlgApi.CloudChannel>());
        Assert.Equal(492, Marshal.SizeOf<ZlgApi.CloudDevice>());
        Assert.Equal(144, Marshal.OffsetOf<ZlgApi.CloudUserData>(nameof(ZlgApi.CloudUserData.DeviceCount)).ToInt32());
        Assert.Equal(152, Marshal.OffsetOf<ZlgApi.CloudUserData>(nameof(ZlgApi.CloudUserData.Devices)).ToInt32());
        Assert.Equal(49352, Marshal.SizeOf<ZlgApi.CloudUserData>());
    }

    internal static void VerifyActualPage()
    {
        var serializer = new CanOpenPlugin().CreateSerializer(); var step = new Step();
        var setting = new CanOpenSetting { AdapterType = CanAdapterType.ZLG, Channel = "FileGlobals.ZlgChannel" };
        step.StepSetting.Setting = serializer.Serialize(setting); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new CanOpenEditorView(); view.RefreshFromStep(step);
        Assert.IsType<Syncfusion.Windows.Tools.Controls.TabControlExt>(view.Content).SelectedIndex = 1;
        view.Measure(new Size(1100, 750)); view.Arrange(new Rect(0, 0, 1100, 750)); view.UpdateLayout();
        Assert.Equal(setting.Channel, view.ViewModel.Channel); Assert.True(view.ViewModel.ShowZlgNetwork); Assert.True(view.ViewModel.ShowZlgCloud);
        var selector = Assert.IsType<ComboBox>(view.FindName("ZlgHardwareSelector")); Assert.True(selector.Items.Count > 80);
        var filter = Assert.IsType<TextBox>(view.FindName("ZlgHardwareFilterEditor"));
        filter.Text = "^CANFDNET.*TCP";
        filter.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Assert.True(selector.Items.Count < 20); Assert.Equal(setting.Channel, view.ViewModel.Channel);
        selector.SelectedItem = ZlgDeviceCatalog.Devices.First(d => d.Name == "CANFDNET_200U_TCP");
        selector.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedItemProperty)!.UpdateSource();
        view.ViewModel.ZlgDeviceIndexToApply = 3; view.ViewModel.ZlgChannelIndexToApply = 1;
        Assert.Equal("\"CANFDNET_200U_TCP/3/1\"", view.ViewModel.Channel);
        Assert.Equal(view.ViewModel.Channel, Assert.IsType<CanOpenSetting>(serializer.Deserialize(step.StepSetting.Setting, 1)).Channel);
        Assert.False(view.ViewModel.ShowZlgCloud); Assert.True(view.ViewModel.ShowZlgNetwork);
        view.UpdateLayout();
        var input = Assert.IsAssignableFrom<FrameworkElement>(view.FindName("ZlgRemoteAddressEditor"));
        Assert.True(input.ActualWidth > 0); Assert.True(input.ActualHeight > 0);
        input.GetType().GetProperty("ScriptText")!.SetValue(input, "\"10.1.2.3\"");
        var scriptProperty = (DependencyProperty)input.GetType().GetField("ScriptTextProperty")!.GetValue(null)!;
        BindingOperations.GetBindingExpression(input, scriptProperty)!.UpdateSource();
        var port = Assert.IsType<TextBox>(view.FindName("ZlgRemotePortEditor"));
        port.Text = "6001";
        port.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
        Assert.Equal("6001", port.Text);
        Assert.Equal(Visibility.Visible, Assert.IsType<GroupBox>(view.FindName("ZlgNetworkGroup")).Visibility);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<GroupBox>(view.FindName("ZlgCloudGroup")).Visibility);
        Capture(view, "zlg-network.png");
        view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var restored = Assert.IsType<CanOpenSetting>(serializer.Deserialize(step.StepSetting.Setting, 1));
        Assert.Equal("CANFDNET_200U_TCP/3/1", JsonSerializer.Deserialize<string>(restored.Channel));
        Assert.Equal("\"10.1.2.3\"", restored.ZlgRemoteAddress); Assert.Equal(6001, restored.ZlgRemotePort);
        view.ViewModel.Channel = "\"CLOUD/0/0\""; view.UpdateLayout();
        Assert.Equal(Visibility.Visible, Assert.IsType<GroupBox>(view.FindName("ZlgCloudGroup")).Visibility);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<GroupBox>(view.FindName("ZlgNetworkGroup")).Visibility);
        Capture(view, "zlg-cloud.png");
        view.ViewModel.Channel = "\"CANFDCOM_100IE/0/0\""; view.UpdateLayout();
        Assert.Equal(Visibility.Visible, Assert.IsType<GroupBox>(view.FindName("ZlgFileGroup")).Visibility);
        Assert.Equal(Visibility.Collapsed, Assert.IsType<GroupBox>(view.FindName("ZlgCloudGroup")).Visibility);
        Capture(view, "zlg-com.png");
        view.ViewModel.AdapterType = (int)CanAdapterType.NI; Assert.False(view.ViewModel.IsZlg); Assert.False(view.ViewModel.ShowZlgNetwork);
        view.ViewModel.FlushPendingSave();
    }

    private static void Capture(FrameworkElement view, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("CAN_ZLG_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(folder)) return;
        view.Measure(new Size(1100, 750)); view.Arrange(new Rect(0, 0, 1100, 750)); view.UpdateLayout();
        Directory.CreateDirectory(folder);
        var bitmap = new RenderTargetBitmap(1100, 750, 96, 96, PixelFormats.Pbgra32); bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(folder, name)); encoder.Save(output);
    }
}
