# UDP Communication Plugin Design

## Scope

Create a new xTestPlatform UDP communication plugin using only the supplied
SDK package, development guide, and README.  The plugin exposes two independent
steps, following the guide's single-responsibility rule:

| Step type ID | Display name | Responsibility |
| --- | --- | --- |
| `Network.UDP_Send` | `UDP_Send` | Send one UDP datagram. |
| `Network.UDP_SendAndReceive` | `UDP_SendAndReceive` | Send one UDP datagram, receive one reply, and validate it. |

The project targets `net8.0-windows7.0`, uses WPF, references
`xTestPlatform.StepEditor.SDK` version `1.0.14` from the local package source,
and produces a DLL whose name ends in `.StepPlugin.dll`.

## Architecture

Each step has an independent settings model, `StepPluginBase<TSetting>` plugin,
`IStepExecutor` executor, and `IStepEditorPlugin` WPF editor.  The editor and
runtime plugin share the same `StepTypeId`.  The editor validates user-entered
settings before execution; the executor repeats essential validation so invalid
serialized data cannot cause an unhandled runtime error.

No operation/action selector is used.  Sending only and sending with reply
validation are separate steps.

## Settings

Both steps expose these editable settings:

| Setting | Default | Rules |
| --- | --- | --- |
| Remote address | none | Required IPv4 address. |
| Remote port | none | Required, 1 through 65535. |
| Local bind address | `127.0.0.1` | Required IPv4 address. |
| Local bind port | `0` | 0 lets the OS select a port; otherwise 1 through 65535. |
| Request data | empty | UTF-8 text or a hexadecimal byte sequence. |
| Request format | UTF-8 text | Text or hexadecimal. |

`UDP_SendAndReceive` additionally exposes:

| Setting | Default | Rules |
| --- | --- | --- |
| Receive timeout (ms) | 3000 | Required positive integer. |
| Reply format | UTF-8 text | Text or hexadecimal. |
| Expected reply | empty | Optional.  Empty accepts any received reply. |
| Match mode | Exact | Exact whole-string comparison or Contains substring comparison. |
| Response variable | empty | Optional variable name to receive the decoded reply. |

Hexadecimal input ignores whitespace and must contain an even number of valid
hexadecimal characters.  Text is encoded and decoded as UTF-8.

## Runtime behavior

Every execution creates and disposes an independent `UdpClient`, bound to the
configured local address and port.  The receive step uses this same socket to
send its request and await a single reply, so replies sent to a chosen fixed
local port are supported.

On success, the executor returns `StepResult.Status = Passed`.  The send step's
value reports its sent data; the send-and-receive step's value reports the
decoded reply.  If a response variable is configured, the decoded reply is
saved to that execution variable.

The receive step fails (`TestStatus.Failed`) when its receive operation times
out or the received reply does not match a configured expected reply.  Invalid
addresses, ports, request data, and timeout values also return a user-readable
failure result.  Socket or other unexpected network errors return `Error` with
a concise Chinese message.  Cancellation returns `Aborted`, and all asynchronous
I/O observes the supplied cancellation token.

## Editor validation and tests

The WPF editors validate required fields, IPv4 addresses, port ranges, timeout,
and hexadecimal syntax.  Expected reply matching is applied only when an
expected reply is supplied.

Automated tests run against a local UDP endpoint and cover: send-only delivery,
successful exact matching, successful contains matching, timeout, mismatch,
UTF-8 and hexadecimal payloads, and configured local bind address/port.
