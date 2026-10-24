namespace CAN.Adapters.NiXnet;

internal class NiXnetIo
{
    public virtual int Read(uint session, byte[] buffer, out uint count) =>
        NiXnetApi.nxReadFrame(session, buffer, (uint)buffer.Length, 0, out count);
    public virtual int Write(uint session, byte[] buffer) =>
        NiXnetApi.nxWriteFrame(session, buffer, (uint)buffer.Length, 1);
    public virtual int ReadTime(uint session, out ulong time, out int fault) =>
        NiXnetApi.nxReadStateTime(session, NiXnetApi.nxState_TimeCurrent, sizeof(ulong), out time, out fault);
    public virtual int TransmitComplete(uint session) =>
        NiXnetApi.nxWait(session, NiXnetApi.nxCondition_TransmitComplete, 0, 0, out _);
    public virtual int Stop(uint session) => NiXnetApi.nxStop(session, NiXnetApi.nxScope_Normal);
    public virtual int State(uint session, out uint value, out int fault) =>
        NiXnetApi.nxReadState(session, NiXnetApi.nxState_SessionInfo, sizeof(uint), out value, out fault);
    public virtual int Clear(uint session) => NiXnetApi.nxClear(session);
    public virtual void Check(int status) => NiXnetApi.CheckStatus(status);
}
