namespace CAN.Adapters.Zlg;

/// <summary>驱动边界；测试替换此层，不加载 DLL 或访问板卡。</summary>
internal class ZlgIo
{
    public virtual IntPtr OpenDevice(uint type, uint index) => ZlgApi.OpenDevice(type, index, 0);
    public virtual IntPtr InitCan(IntPtr device, uint channel, ref ZlgApi.ZCAN_CHANNEL_INIT_CONFIG config) => ZlgApi.InitCAN(device, channel, ref config);
    public virtual uint StartCan(IntPtr channel) => ZlgApi.StartCAN(channel);
    public virtual uint ClearBuffer(IntPtr channel) => ZlgApi.ClearBuffer(channel);
    public virtual bool SetProperty(IntPtr device, string path, string value) => ZlgApi.TrySetProperty(device, path, value);
    public virtual uint Transmit(IntPtr channel, ref ZlgApi.ZCAN_Transmit_Data data) => ZlgApi.Transmit(channel, ref data, 1);
    public virtual uint TransmitFd(IntPtr channel, ref ZlgApi.ZCAN_TransmitFD_Data data) => ZlgApi.TransmitFD(channel, ref data, 1);
    public virtual uint Receive(IntPtr channel, ZlgApi.ZCAN_Receive_Data[] output, int waitMs) => ZlgApi.ReceiveBatch(channel, output, (uint)output.Length, waitMs);
    public virtual uint ReceiveFd(IntPtr channel, ZlgApi.ZCAN_ReceiveFD_Data[] output, int waitMs) => ZlgApi.ReceiveFdBatch(channel, output, (uint)output.Length, waitMs);
    public virtual uint ResetCan(IntPtr channel) => ZlgApi.ResetCAN(channel);
    public virtual uint CloseDevice(IntPtr device) => ZlgApi.CloseDevice(device);
}
