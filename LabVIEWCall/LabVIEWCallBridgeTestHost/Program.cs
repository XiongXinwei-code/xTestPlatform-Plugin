using LabVIEWCallPlugin.BridgeProtocol;
using LabVIEWCallPlugin.LVadapter;

// This executable is a test fixture only. It never loads a native library or a VI.
if (args.Length != 4) return 2;
string mode = args[0];
string directory = args[1];
using var entered = EventWaitHandle.OpenExisting(args[2]);
using var finish = EventWaitHandle.OpenExisting(args[3]);
using var input = Console.OpenStandardInput();
using var output = Console.OpenStandardOutput();
File.WriteAllText(Path.Combine(directory, "pid.txt"), Environment.ProcessId.ToString());
var references = new BridgeReferenceSlot();
BridgeWire.Write(output, new BridgeResponse
{
    Kind = mode == "badready" ? "Unknown" : "Ready",
    ProcessId = Environment.ProcessId
});
while (BridgeWire.Read<BridgeRequest>(input) is { } request)
{
    File.AppendAllText(Path.Combine(directory, "requests.txt"), $"{request.Id}|{request.Operation}|{request.Path}|{request.Port}\n");
    BridgeWire.Write(output, new BridgeResponse
    {
        Id = request.Id, Kind = "Phase", Phase = "TestNativeBoundary", ProcessId = Environment.ProcessId
    });
    if (mode == "crash") Environment.Exit(unchecked((int)0xC0000005));
    if ((mode is "block" or "ports-block") && request.Path == "block")
    {
        entered.Set();
        if (!finish.WaitOne(TimeSpan.FromSeconds(15))) return 3;
    }
    var response = new BridgeResponse
    {
        Id = mode == "wrongid" ? request.Id + 1 : request.Id,
        Kind = mode == "wrongkind" ? "Unexpected" : "Result",
        ProcessId = Environment.ProcessId,
        Reference = checked((uint)request.Id),
        Type = request.Type,
        Data = request.Data,
        Panel = new() { ControlOut = "[]", IndicatorOut = "[]", VIState = VIState.Idle },
        Error = mode == "managederror" ? "模拟端子读取错误" : ""
    };
    if (mode is "ports" or "ports-block")
    {
        try
        {
            _ = request.ValidatePort();
            // The fake always reuses the same native cookie, including across ports.
            if (request.Operation == BridgeOperation.Load)
                response.Reference = references.Load(request.Port, () => 0x777);
            else if (request.Operation == BridgeOperation.Run)
                response.Reference = references.Run(request.Reference, request.Port, native => native + 1);
            else if (request.Operation is BridgeOperation.Get or BridgeOperation.Set)
                _ = references.Resolve(request.Reference, request.Port);
        }
        catch (Exception error) { response.Error = error.ToString(); }
    }
    BridgeWire.Write(output, response);
}
return 0;
