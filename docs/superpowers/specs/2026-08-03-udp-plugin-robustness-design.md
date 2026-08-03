# UDP Plugin Robustness Design

## Goal

Make UDP failures deterministic and recoverable without automatic retransmission. A UDP send or send-and-receive attempt remains at-most-once: the plugin never repeats a packet after a transport, timeout, or cancellation failure.

## Scope

1. Validate local and remote addresses before opening a socket. The plugin accepts literal IPv4 and IPv6 addresses, rejects invalid literals, and rejects address-family mismatches between local and remote endpoints. DNS names remain out of scope.
2. Strengthen the transport boundary. `UdpTransport` rejects a non-positive timeout, observes an already-cancelled token before allocating a socket, preserves configured-peer filtering, and continues to distinguish caller cancellation from a receive timeout.
3. Preserve editor changes when optional host integrations fail. A `generateDescription` exception falls back to a fixed command label. If `ExecuteCommand` throws before accepting the action, the editor writes the serialized setting directly so the user does not silently lose changes.
4. Add focused regression tests for every new contract plus a full solution regression run.

## Architecture

`UdpSettingsValidator` remains the executor-facing preflight boundary: it reports malformed or incompatible endpoint settings as a configuration error before `UdpTransport` receives them. `UdpTransport` separately validates the timeout and cancellation preconditions because it is a public injectable adapter and may be called outside an executor.

The transport retains one socket per operation and one send attempt. It receives until the configured peer sends a datagram or the linked timeout token expires. A caller cancellation is propagated as `OperationCanceledException`; a timeout caused only by the linked timer is converted to `TimeoutException`.

`UdpEditorViewModel` keeps host integrations optional. It serializes the setting once, builds a host command label from the injected description generator when possible, and invokes the injected command. If either optional integration throws synchronously, it traces the failure and directly applies the same serialized bytes to the captured step. This fallback is deliberately limited to failure before the host command accepts work; a successfully accepted host command remains responsible for undo/redo integration.

## Error Handling and Observable Results

| Condition | Layer | Result |
| --- | --- | --- |
| Invalid address, unsupported literal, or mixed IPv4/IPv6 endpoints | Executor preflight | `TestStatus.Error` configuration result; no socket created |
| Receive timeout | Transport then executor | `TimeoutException`, then `TestStatus.Failed` |
| Caller cancellation before or during operation | Transport then executor | `OperationCanceledException`, then `TestStatus.Aborted` |
| Socket or codec error | Executor | `TestStatus.Error` with the original message |
| Description generator failure | Editor | Fixed `更新 UDP 步骤配置` label; host command still attempted |
| Host command throws | Editor | Serialized setting is directly written to the captured step |

## Tests

- Validator tests cover invalid literals, valid IPv4, valid IPv6, and mixed-family rejection.
- Transport tests cover already-cancelled operations and invalid timeout arguments without requiring a live peer.
- Executor tests verify invalid endpoint configuration returns an error without invoking an injected transport, while timeout and caller cancellation retain their existing distinct statuses.
- Editor tests verify a throwing description generator uses the fixed label and a throwing host command still persists the exact serialized setting.
- Run `dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore` after the focused tests.

## Non-goals

- No packet retransmission, backoff, reconnect loop, or duplicate-send mitigation protocol.
- No hostname lookup, DNS retry, multicast membership, broadcast configuration, or packet-format changes.
- No change to the configured-peer filter, response matching semantics, response variable semantics, or plugin deployment layout.
