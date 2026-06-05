# Wave 2 logbook - Build event transport

## Focus

Canonical candidate: `Microsoft.Build.BackEnd.Logging.BuildEventArgTransportSink` + `Microsoft.Build.BackEnd.LogMessagePacket`.

Deep-dive scope:

- per-event transport/remoting cost
- batching or lack thereof
- serialization work
- whether this matters in one-process multi-project parallel builds vs multi-node scenarios

## Key findings

### 1. This path is fundamentally an out-of-proc path

`BuildEventArgTransportSink` is created in `OutOfProcNode` when node configuration is applied:

- `src\Build\BackEnd\Node\OutOfProcNode.cs:775-817`

The logging wiki also places it specifically in the OOP-node flow:

- `documentation\wiki\Logging-Internals.md:50-53,98-101`

Practical consequence:

- for **pure one-process** parallel builds, this candidate is mostly absent
- for **multi-node / out-of-proc** builds, it sits directly on the event-forwarding path from worker nodes back to the scheduler

That immediately lowers its importance for the user’s target scenario relative to multi-node cases.

### 2. There is no batching at the transport-sink level

`BuildEventArgTransportSink.Consume(...)`:

- ignores `BuildStarted` / `BuildFinished` by toggling flags
- otherwise creates exactly one `LogMessagePacket`
- immediately invokes `_sendDataDelegate(logPacket)`  
  - `src\Build\BackEnd\Components\Logging\BuildEventArgTransportSink.cs:133-149`

So the shape is strictly:

`forwarded event -> allocate packet -> serialize/send packet`

There is no event aggregation, chunking-by-many-events, or “flush later” policy at this layer.

### 3. The per-event work is non-trivial

`LogMessagePacketBase.Translate(...)` writes:

- event type enum
- sink id
- packet version metadata
- event serialization mode flag
- event payload itself  
  - `src\Shared\LogMessagePacketBase.cs:365-420`

The default write path also:

- hits a shared `s_writeMethodCache` under lock on first use / cache lookup path  
  - `src\Shared\LogMessagePacketBase.cs:268-270,388-397`
- discovers or reuses `WriteToStream` reflection metadata
- creates a closed delegate with `CreateDelegateRobust(...)`
- invokes event-specific serialization  
  - `src\Shared\LogMessagePacketBase.cs:404-415,479-500`

So even before pipe transport, each forwarded event pays packet framing plus event serialization mechanics.

### 4. Some event kinds are especially expensive

`LogMessagePacket` overrides default serialization for some event types:

- `ProjectEvaluationStarted`
- `ProjectEvaluationFinished`
- `ResponseFileUsedEvent`  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:57-97`

For `ProjectEvaluationFinished`, it serializes:

- project file and timestamp
- global properties
- properties
- items
- profiler result  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:102-109`

And helper methods enumerate/flatten property and item collections:

- `WriteProperties(...)`  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:184-212`
- `WriteItems(...)` with thread-static reusable lists  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:214-240`

So the per-event cost varies a lot: ordinary status/message events are much lighter than evaluation-finished payloads.

### 5. Transport itself still remains packet-at-a-time

At the node communication layer, the packet is serialized into a write stream and then written to the pipe in chunks up to `MaxPacketWriteSize`:

- `src\Build\BackEnd\Components\Communications\NodeProviderOutOfProcBase.cs:40`
- `src\Build\BackEnd\Components\Communications\NodeProviderOutOfProcBase.cs:1219-1234`

That chunking is only for large single packets. It is **not** batching multiple log events together into one transport unit.

### 6. Scheduler side immediately re-injects the event into central logging

The build manager registers `LogMessagePacket` handling with the logging service:

- `src\Build\BackEnd\BuildManager\BuildManager.cs:692-700`

Scheduler-side `LoggingService.PacketReceived(...)`:

- casts to `LogMessagePacket`
- injects non-serialized data
- calls `ProcessLoggingEvent(loggingPacket.NodeBuildEvent)`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1000-1014,1367-1387`

Meaning this transport path is additive to the already-serialized central logging path; it does not replace it.

### 7. Most likely impact story is multi-node fan-in overhead, not one-process contention

This candidate does not look like a major one-process bottleneck because:

- it is not on the pure in-proc logging path
- its cost only appears when events cross node boundaries

It looks more relevant when:

- many worker-node events are forwarded back to the scheduler
- forwarded events include expensive payloads (evaluation data, properties/items, telemetry-rich events)
- named-pipe / translation overhead becomes material relative to build work

## Reasoned conclusion

### Is this a real bottleneck candidate?

Yes, but mainly for **multi-node / out-of-proc** builds.

### How likely is it to matter in one-process multi-project parallel builds?

**Low** for pure one-process builds.

Reason: this transport path is largely bypassed when work stays in-proc.

### How likely is it to matter in multi-node scenarios?

**Medium** overall, potentially higher in chatty builds.

Why not high by default:

- ordinary events are fairly compact
- some transport overhead may still be dwarfed by project/task work

Why not low:

- there is no batching
- every forwarded event becomes its own packet
- event serialization can be expensive for richer event types
- scheduler still must pay central logging cost after transport

## Escalation decision

Escalate to full report: **yes**.

Reason: this is a plausible multi-node throughput tax, but the deep-dive also makes clear that it is **not** a top-tier one-process bottleneck candidate, which is exactly the distinction the final report should preserve.
