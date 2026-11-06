using System.Runtime.InteropServices;
using System.Text;
using LabVIEWCallPlugin.BridgeProtocol;
using LabVIEWCallPlugin.LVadapter;

namespace LabVIEWCallBridgeHost;

internal static class Program
{
    private static readonly BridgeReferenceSlot References = new();
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint SetErrorMode(uint mode);

    [MTAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 0) return 64;
        // Apply only to this child. A native fault must not open a Windows error dialog.
        SetErrorMode(0x0001 | 0x0002 | 0x8000);
        Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
        try
        {
            using var input = Console.OpenStandardInput();
            using var output = Console.OpenStandardOutput();
            BridgeWire.Write(output, new BridgeResponse { Kind = "Ready", ProcessId = Environment.ProcessId });
            long lastId = 0;
            while (BridgeWire.Read<BridgeRequest>(input) is { } request)
            {
                if (request.Id <= lastId || !Enum.IsDefined(request.Operation))
                    throw new InvalidDataException("LabVIEW 桥请求序号或操作无效。");
                lastId = request.Id;
                BridgeWire.Write(output, new BridgeResponse
                {
                    Id = request.Id, Kind = "Phase", Phase = request.Operation.ToString(), ProcessId = Environment.ProcessId
                });
                var response = new BridgeResponse
                {
                    Id = request.Id, Phase = request.Operation.ToString(), ProcessId = Environment.ProcessId
                };
                try { Execute(request, response); }
                catch (Exception error) { response.Error = error.ToString(); }
                BridgeWire.Write(output, response);
            }
            return 0;
        }
        catch (Exception error)
        {
            // Diagnostic channel is independent of the framed protocol and never prints VI data.
            Console.Error.WriteLine($"LabVIEW 桥通信终止，{error.GetType().Name}，{error.Message}");
            return 70;
        }
    }

    private static void Execute(BridgeRequest request, BridgeResponse response)
    {
        ushort port = request.ValidatePort();
        switch (request.Operation)
        {
            case BridgeOperation.Connect:
                if (!LvLibHelper.ConnectLabVIEW(out var diagnostic, port))
                    throw new InvalidOperationException(diagnostic);
                break;
            case BridgeOperation.Panel:
                response.Panel = LvLibHelper.GetConnectPanel(request.Path, port,
                    controlIn: request.ControlIn, indicatorIn: request.IndicatorIn);
                break;
            case BridgeOperation.Metadata:
                response.Panel = LvLibHelper.GetTerminalMetadata(request.Path, port);
                break;
            case BridgeOperation.Edit:
                LvLibHelper.EditVI(request.Path, port);
                break;
            case BridgeOperation.Load:
                response.Reference = References.Load(port,
                    () => LvLibWrapper.ToNativeRefNum(LvLibHelper.LoadVI(request.Path, port)));
                break;
            case BridgeOperation.Get:
                (response.Type, response.Data) = LvLibHelper.GetVIParameterRaw(
                    LvLibWrapper.FromNativeRefNum(References.Resolve(request.Reference, port)), request.ParameterName);
                break;
            case BridgeOperation.Set:
                LvLibHelper.SetVIParameter(LvLibWrapper.FromNativeRefNum(References.Resolve(request.Reference, port)),
                    request.ParameterName, request.Type, request.Data);
                response.Reference = request.Reference;
                break;
            case BridgeOperation.Run:
                response.Reference = References.Run(request.Reference, port, native =>
                    LvLibWrapper.ToNativeRefNum(LvLibHelper.RunVI(
                        LvLibWrapper.FromNativeRefNum(native), request.ShowPanel, request.ClosePanel)));
                break;
            default:
                throw new InvalidDataException("LabVIEW 桥不支持该操作。");
        }
    }
}
