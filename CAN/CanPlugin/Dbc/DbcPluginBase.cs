using xTestPlatform.Core.Plugins.BuiltIn;

namespace CAN.Dbc;

public abstract class DbcPluginBase<T> : CAN.PluginInfrastructure.StrictStepPluginBase<T> where T : DbcSourceSetting, new()
{
    // The host's StepFactory creates StepSetting with the SDK's default metadata.
    // This plugin has one current settings format, so use that same SDK contract.
}
