# Native engine IPC contract

Date: 8 October 2026. Design only; transport deferred. Final peer is C++20 x20ctl-engine.exe, not the superseded Python adapter.

## Transport

Prefer UI-owned Windows named pipe restricted to intended user/logon session; reject remote clients. Launch pinned bundled engine directly/hidden, never arbitrary shell/executable. Framed redirected stdin/stdout is fallback. Logs use stderr/file; no local web server or raw-packet dispatch.

Four-byte unsigned little-endian UTF-8 byte length + JSON; proposed 2 MiB maximum checked before allocation. Handle partial/truncated/oversized/malformed frames. Allowlist methods and validate every parameter/session.

## RPC

Request: protocolVersion, unique requestId, method, object params. Mutations include sessionId/generation. Response echoes version/ID, ok/result or structured error.code/message; Apply has per-category status. Handshake reports engine version, methods/streams, epoch, clock units/frequency, limits. Incompatible versions prevent hardware operations.

Drafts stay local unless domain validation is requested; WPF owns dialogs, engine owns validation/hardware/persistence. Never trust client-supplied writeSupport/evidence.

## Subscriptions

Subscribe/unsubscribe RPC; connections, gameplay input, battery, BLE state, source activity, scanner and readback changes stream asynchronously.

```json
{
  "protocolVersion": 1,
  "kind": "event",
  "stream": "input",
  "subscriptionId": "...",
  "engineEpoch": "...",
  "sequence": 1842,
  "timestampUs": 123456789,
  "sourceId": "...",
  "type": "button",
  "droppedEventCount": 0,
  "payload": { "down": true }
}
```

Sequence is monotonic per subscription/epoch; time is engine monotonic microseconds with declared origin, never UI arrival time. Epoch changes invalidate old authorization/sequences. Start with snapshot; explicit end/source-loss/unsubscribe; reconnect requires fresh snapshot.

All queues/subscriber counts/batches are bounded. Slow subscribers cannot block replies or capture. Engine records authoritative scanner events before presentation. UI may coalesce display state, never silently drop evidence. Recording overflow marks incomplete evidence with source/time/drop count in diagnostics/export. Subscription loss is separate from recording loss. Preserve both.

Use JSON batches initially; change encoding only if measured throughput justifies it. Hardware event rate does not dictate repaint rate.

## Writes/recovery

Serialize per device. Cancellation before send prevents queued execution. After send, timeout/cancel is indeterminate; reconnect with fresh identity and reread hardware before retry. Desired readback verifies current state; ACK/persistence remain separate. No automatic non-idempotent replay. Preserve successful categories, unresolved results and drafts.

Crash revokes authorization; new handshake/identity/reads precede UI truth reconstruction. Graceful close stops streams/device work and terminates only owned child if needed, without replay.

## Future tests

Framing/limits/version/IDs/allowlist; malicious direct writes/session mismatch; ordering/gaps/epoch/clock/snapshots; bounded overflow and export loss; queued cancellation; applied-with-lost-ACK; partial categories; crash/reconnect/no replay. Not executed at this milestone.
