using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Data;
using CAN.Adapters.Zlg;
using CAN.Models;
using CAN.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class CanOpenHardwareSelectionTests
{
    private static readonly CanOpenPlugin Plugin = new();

    private static (Step Step, CanOpenViewModel Model) Create(string channel = "FileGlobals.ZlgChannel")
    {
        var serializer = Plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.StepType = Plugin.StepTypeId;
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        step.StepSetting.Setting = serializer.Serialize(new CanOpenSetting { AdapterType = CanAdapterType.ZLG, Channel = channel });
        var model = new CanOpenViewModel(); model.AttachSerializer(serializer); model.AttachStep(step);
        return (step, model);
    }

    private static CanOpenSetting Read(Step step) => (CanOpenSetting)Plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
    private static ZlgDeviceInfo Device(string name) => ZlgDeviceCatalog.Devices.Single(item => item.Name == name);

    [WpfFact]
    public void Hardware_and_indices_apply_without_an_apply_button_or_later_flush()
    {
        var (step, model) = Create();
        model.ZlgDeviceToApply = Device("CANFDNET_200U_TCP");
        Assert.Equal("\"CANFDNET_200U_TCP/0/0\"", Read(step).Channel);
        Assert.True(model.ShowZlgNetwork); Assert.False(model.ShowZlgCloud);
        model.ZlgDeviceIndexToApply = 3; model.ZlgChannelIndexToApply = 1;
        Assert.Equal("\"CANFDNET_200U_TCP/3/1\"", Read(step).Channel);
        model.ZlgDeviceToApply = Device("CLOUD");
        Assert.Equal("\"CLOUD/3/1\"", Read(step).Channel);
        Assert.True(model.ShowZlgCloud); Assert.False(model.ShowZlgNetwork);
    }

    [WpfTheory]
    [InlineData("\"USBCANFD-200U/2/1\"", "USBCANFD_200U", 2, 1)]
    [InlineData("\"CANFDNET_200U_TCP/3/2\"", "CANFDNET_200U_TCP", 3, 2)]
    [InlineData("\"ZCAN_USBCANII/0/1\"", "USBCAN2", 0, 1)]
    public void Loading_and_editing_channel_literals_hydrate_selection_without_rewriting_aliases(string channel, string name, int device, int bus)
    {
        var (step, model) = Create(channel); var original = step.StepSetting.Setting.ToArray();
        Assert.Equal(Device(name), model.ZlgDeviceToApply);
        Assert.Equal(device, model.ZlgDeviceIndexToApply); Assert.Equal(bus, model.ZlgChannelIndexToApply);
        model.FlushPendingSave(); Assert.Equal(original, step.StepSetting.Setting);
        model.Channel = "\"CANFDCOM_100IE/4/5\"";
        Assert.Equal(Device("CANFDCOM_100IE"), model.ZlgDeviceToApply);
        Assert.Equal(4, model.ZlgDeviceIndexToApply); Assert.Equal(5, model.ZlgChannelIndexToApply);
        Assert.True(model.ShowZlgFile); model.FlushPendingSave();
    }

    [WpfTheory]
    [InlineData("FileGlobals.ZlgChannel")]
    [InlineData("\"NotInstalledModel/0/0\"")]
    [InlineData("\"CAN1\"")]
    public void Dynamic_or_unrecognized_channel_survives_load_refresh_and_filter(string channel)
    {
        var (step, model) = Create(channel); var original = step.StepSetting.Setting.ToArray();
        Assert.Null(model.ZlgDeviceToApply);
        model.ZlgHardwareFilter = "USB|PCIE";
        model.AttachStep(step); model.FlushPendingSave();
        Assert.Equal(channel, model.Channel); Assert.Null(model.ZlgDeviceToApply);
        Assert.Equal(original, step.StepSetting.Setting);
    }

    [WpfFact]
    public void Reusing_editor_hydrates_new_step_without_saving_old_model_into_it()
    {
        var (first, model) = Create("\"CLOUD/7/2\""); var (second, _) = Create("FileGlobals.SecondChannel");
        model.ZlgCloudHttpPort = 1234;
        model.AttachStep(second);
        Assert.Equal(1234, Read(first).ZlgCloudHttpPort);
        Assert.Equal("FileGlobals.SecondChannel", model.Channel); Assert.Null(model.ZlgDeviceToApply);
        model.ZlgDeviceToApply = Device("USBCANFD_200U");
        Assert.Equal("\"USBCANFD_200U/0/0\"", Read(second).Channel);
        Assert.Equal("\"CLOUD/7/2\"", Read(first).Channel);
    }

    [WpfFact]
    public void Invalid_numeric_index_is_visible_and_not_silently_replaced_by_old_valid_configuration()
    {
        var (step, model) = Create("\"USBCANFD_200U/2/1\"");
        model.ZlgDeviceIndexToApply = -1;
        Assert.NotEmpty(model[nameof(model.ZlgDeviceIndexToApply)]);
        Assert.Equal("\"USBCANFD_200U/-1/1\"", Read(step).Channel);
        Assert.Equal(-1, model.ZlgDeviceIndexToApply);
        model.ZlgDeviceIndexToApply = 4;
        Assert.Empty(model[nameof(model.ZlgDeviceIndexToApply)]);
        Assert.Equal("\"USBCANFD_200U/4/1\"", Read(step).Channel);
    }

    [WpfFact]
    public void Regex_filters_case_insensitively_and_never_changes_existing_selection_or_channel()
    {
        var (step, model) = Create("\"CLOUD/2/1\""); var original = step.StepSetting.Setting.ToArray();
        var selector = new ComboBox { DataContext = model, DisplayMemberPath = "Display" };
        selector.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(model.FilteredZlgDevices)));
        selector.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, new Binding(nameof(model.ZlgDeviceToApply)) { Mode = BindingMode.TwoWay });
        model.ZlgHardwareFilter = "^canfd.*tcp";
        Assert.Contains(Device("CANFDNET_200U_TCP"), model.FilteredZlgDevices);
        Assert.DoesNotContain(Device("USBCANFD_200U"), model.FilteredZlgDevices);
        Assert.Equal(Device("CLOUD"), selector.SelectedItem);
        Assert.Equal(Device("CLOUD"), model.ZlgDeviceToApply);
        Assert.Equal(original, step.StepSetting.Setting);
        model.ZlgHardwareFilter = "";
        Assert.Equal(model.ZlgDevices.Count, model.FilteredZlgDevices.Count);
        Assert.Equal(Device("CLOUD"), selector.SelectedItem);
    }

    [WpfTheory]
    [InlineData("[")]
    [InlineData("(?<=")]
    public void Invalid_regex_preserves_last_results_and_reports_error(string pattern)
    {
        var (step, model) = Create(); model.ZlgHardwareFilter = "USB";
        var prior = model.FilteredZlgDevices.ToArray(); var original = step.StepSetting.Setting.ToArray();
        model.ZlgHardwareFilter = pattern;
        Assert.NotEmpty(model.ZlgHardwareFilterError); Assert.Equal(prior, model.FilteredZlgDevices);
        Assert.Equal(original, step.StepSetting.Setting);
        model.ZlgHardwareFilter = "^PCIE"; Assert.Empty(model.ZlgHardwareFilterError);
        Assert.All(model.FilteredZlgDevices, item => Assert.StartsWith("PCIE", item.Name.ToUpperInvariant()));
    }

    [WpfFact]
    public void Selecting_a_filtered_result_applies_only_that_model_and_retains_channel_indices()
    {
        var (step, model) = Create("\"CLOUD/2/1\"");
        model.ZlgHardwareFilter = "^CANFDNET.*TCP";
        model.ZlgDeviceToApply = Device("CANFDNET_200U_TCP");
        Assert.Equal("CANFDNET_200U_TCP/2/1", JsonSerializer.Deserialize<string>(Read(step).Channel));
        Assert.DoesNotContain(Device("CLOUD"), model.FilteredZlgDevices);
        model.ZlgHardwareFilter = new string('a', 257); Assert.NotEmpty(model.ZlgHardwareFilterError);
    }

    [WpfFact]
    public void Filtering_is_local_to_each_editor_instance()
    {
        var (_, first) = Create(); var (_, second) = Create();
        first.ZlgHardwareFilter = "^USB";
        Assert.True(first.FilteredZlgDevices.Count < second.FilteredZlgDevices.Count);
        Assert.Equal(second.ZlgDevices.Count, second.FilteredZlgDevices.Count);
        Assert.Equal("", second.ZlgHardwareFilter);
        Assert.Null(second.ZlgDeviceToApply);
    }
}
