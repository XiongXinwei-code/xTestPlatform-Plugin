namespace LabVIEWCallPlugin.BridgeProtocol;

/// <summary>
/// A serial worker owns one live VI ticket. The opaque ticket is independent of
/// NI's process-local LVRefNum and is invalidated before the next native Load.
/// </summary>
internal sealed class BridgeReferenceSlot(uint initialSequence = 0)
{
    private uint _sequence = initialSequence;
    private uint _ticket;
    private uint _native;
    private int _port;

    internal uint Load(int port, Func<uint> load)
    {
        _ = new BridgeRequest { Port = port }.ValidatePort();
        _ticket = 0;
        _native = 0;
        _port = port;
        // Never wrap tickets: a wrapped value could grant access through an old reference.
        if (_sequence == uint.MaxValue)
            throw new InvalidOperationException("VI 引用票据已用尽，未调用原生加载；请关闭平台后重新启动。");
        uint ticket = ++_sequence;
        uint native = load();
        if (native == 0) throw new InvalidOperationException("原生 VI 加载未返回有效引用。");
        _native = native;
        _ticket = ticket;
        return ticket;
    }

    internal uint Resolve(uint ticket, int port)
    {
        _ = new BridgeRequest { Port = port }.ValidatePort();
        if (ticket == 0 || ticket != _ticket || port != _port || _native == 0)
            throw new InvalidOperationException($"VI 引用已失效或不属于端口 {port}，禁止跨端口或跨加载调用，请重新加载目标 VI。");
        return _native;
    }

    internal uint Run(uint ticket, int port, Func<uint, uint> run)
    {
        uint native = Resolve(ticket, port);
        // A failed Run can change the native cookie; do not allow a follow-up through it.
        _ticket = 0;
        _native = 0;
        uint updated = run(native);
        if (updated == 0) throw new InvalidOperationException("原生 VI 运行未返回有效引用。");
        _native = updated;
        _ticket = ticket;
        return ticket;
    }
}
