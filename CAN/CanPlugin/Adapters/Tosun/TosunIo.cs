namespace CAN.Adapters.Tosun;

/// <summary>可注入的库生命周期边界；测试替代原生调用，不需要打开设备。</summary>
internal class TosunIo
{
    public virtual void Initialize(bool fifo, bool receiveErrors, bool useConnectionTime) => TosunApi.InitializeLib(fifo, receiveErrors, useConnectionTime);
    public virtual void FinalizeLibrary() => TosunApi.FinalizeLib();
    public virtual uint Connect(string serial, ref nuint handle) => TosunApi.Connect(serial, ref handle);
    public virtual uint Disconnect(nuint handle) => TosunApi.Disconnect(handle);
    public virtual uint ConfigureCan(nuint handle, int channel, double baudKbps, uint termination)
        => TosunApi.ConfigCanByBaudrate(handle, channel, baudKbps, termination);
    public virtual uint ConfigureFd(nuint handle, int channel, double baudKbps, double dataKbps, uint termination)
        => TosunApi.ConfigCanFdByBaudrate(handle, channel, baudKbps, dataKbps, 1, 0, termination);
}
