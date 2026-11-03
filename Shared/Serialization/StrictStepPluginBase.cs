using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using PluginCommon.Serialization;

// Each independently deployed plugin exports its own base type. A distinct
// namespace prevents collisions when one plugin references another (CiA402/CAN).
#if PLUGIN_CAN
namespace CAN.PluginInfrastructure;
#elif PLUGIN_CiA402
namespace CiA402.PluginInfrastructure;
#elif PLUGIN_Ethernet
namespace Ethernet.PluginInfrastructure;
#elif PLUGIN_Http
namespace Http.PluginInfrastructure;
#elif PLUGIN_LIN
namespace LIN.PluginInfrastructure;
#elif PLUGIN_Modbus
namespace Modbus.PluginInfrastructure;
#elif PLUGIN_NiDaq
namespace NiDaq.PluginInfrastructure;
#elif PLUGIN_NiGrpc
namespace NiGrpc.PluginInfrastructure;
#elif PLUGIN_OpcUa
namespace OpcUa.PluginInfrastructure;
#elif PLUGIN_SerialPort
namespace SerialPort.PluginInfrastructure;
#elif PLUGIN_VISA
namespace VISA.PluginInfrastructure;
#else
#error Strict settings base requires a plugin-specific compile symbol.
#endif

/// <summary>Validate detached settings before the host publishes the SDK bytes.</summary>
public abstract class StrictStepPluginBase<T> : StepPluginBase<T>, IStepPlugin where T : class, new()
{
    // SDK CreateSerializer is final. Reimplement the interface so both descriptor
    // and IStepPlugin callers reach the same guard; protected SDK read helpers stay unchanged.
    public new IStepSettingSerializer CreateSerializer() => new GuardedSerializer(base.CreateSerializer());
    IStepSettingSerializer IStepPlugin.CreateSerializer() => CreateSerializer();

    private sealed class GuardedSerializer(IStepSettingSerializer inner) : IStepSettingSerializer
    {
        public int SettingVersion => inner.SettingVersion;
        public object CreateDefault() => inner.CreateDefault();
        public object Deserialize(byte[] data, int dataVersion) => inner.Deserialize(data, dataVersion);
        public byte[] Serialize(object setting)
        {
            StrictSettingContract.Validate(setting, typeof(T));
            return inner.Serialize(setting);
        }
    }
}
