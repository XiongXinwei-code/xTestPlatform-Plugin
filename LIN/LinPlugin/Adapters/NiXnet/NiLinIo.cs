[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("LIN.ContractTests")]

namespace LIN.Adapters.NiXnet;

internal class NiLinIo
{
    public virtual int Create(string db, string cluster, string list, string channel, uint mode, out uint session) => NiXnetLinApi.nxCreateSession(db, cluster, list, channel, mode, out session);
    public virtual int Clear(uint session) => NiXnetLinApi.nxClear(session);
    public virtual int Start(uint session, uint scope) => NiXnetLinApi.nxStart(session, scope);
    public virtual int Stop(uint session, uint scope) => NiXnetLinApi.nxStop(session, scope);
    public virtual int Set(uint session, uint property, uint size, ref uint value) => NiXnetLinApi.nxSetProperty(session, property, size, ref value);
    public virtual int Read(uint session, byte[] data, uint size, double timeout, out uint count) => NiXnetLinApi.nxReadFrame(session, data, size, timeout, out count);
    public virtual int Write(uint session, byte[] data, uint size, double timeout) => NiXnetLinApi.nxWriteFrame(session, data, size, timeout);
    public virtual void Check(int status, string context = "") => NiXnetLinApi.CheckStatus(status, context);
}
