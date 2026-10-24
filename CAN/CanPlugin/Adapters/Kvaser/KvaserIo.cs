namespace CAN.Adapters.Kvaser;

// 仅隔离已存在的 CANlib 调用，便于无设备验证部分初始化与释放失败。
internal class KvaserIo
{
    public virtual void Initialize() => KvaserApi.InitializeLibrary();
    public virtual int Open(int channel, int flags) => KvaserApi.OpenChannel(channel, flags);
    public virtual int SetParameters(int handle, int rate, uint t1, uint t2, uint sjw) => KvaserApi.SetBusParams(handle, rate, t1, t2, sjw, 1, 0);
    public virtual int SetFdParameters(int handle, int rate, uint t1, uint t2, uint sjw) => KvaserApi.SetBusParamsFd(handle, rate, t1, t2, sjw);
    public virtual int BusOn(int handle) => KvaserApi.BusOn(handle);
    public virtual int BusOff(int handle) => KvaserApi.BusOff(handle);
    public virtual int Close(int handle) => KvaserApi.Close(handle);
    public virtual void Check(int status) => KvaserApi.CheckStatus(status);
}
