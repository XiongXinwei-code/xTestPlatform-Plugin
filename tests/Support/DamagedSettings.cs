using System.Reflection;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace PluginTestCommon;

/// <summary>Use only in tests for previously stored invalid input. Normal saves use the plugin's public serializer.</summary>
internal static class DamagedSettings
{
    internal static byte[] Serialize(IStepPlugin plugin, object setting)
    {
        // Use the SDK's format and options for this concrete plugin without invoking the write-only guard.
        Type? sdkType = plugin.GetType();
        while (sdkType != null && !(sdkType.IsGenericType && sdkType.GetGenericTypeDefinition() == typeof(StepPluginBase<>)))
            sdkType = sdkType.BaseType;
        var create = sdkType?.GetMethod(nameof(IStepPlugin.CreateSerializer), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (create?.Invoke(plugin, null) is not IStepSettingSerializer serializer)
            throw new InvalidOperationException($"Cannot construct stored settings for {plugin.StepTypeId}.");
        return serializer.Serialize(setting);
    }
}
