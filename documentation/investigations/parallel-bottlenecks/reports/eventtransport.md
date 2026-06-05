# Build event transport

## Why shared

`BuildEventArgTransportSink` is the sink used by out-of-proc node forwarding loggers to ship build events back to the scheduler. `LogMessagePacket` is the transport envelope for those forwarded events.

- the logging wiki describes `BuildEventArgTransportSink` as the single sink injected into OOP-node logging, bundling `BuildEventArgs` plus `SinkId` and sending them through node-to-node communication  
  - `documentation\wiki\Logging-Internals.md:50-53,98-101`
- `OutOfProcNode` creates `BuildEventArgTransportSink sink = new BuildEventArgTransportSink(SendPacket)` and passes it to `InitializeNodeLoggers(...)`  
  - `src\Build\BackEnd\Node\OutOfProcNode.cs:775-817`
- scheduler-side `BuildManager` registers `LogMessagePacket` handling on the logging service  
  - `src\Build\BackEnd\BuildManager\BuildManager.cs:692-700`

So, whenever MSBuild is using out-of-proc worker nodes, this is the shared return path for forwarded logging events.

Important scope distinction: this is **not** the normal pure in-proc path. It is shared across out-of-proc worker activity, not across all one-process builds.

## Why it might bottleneck

The architecture is per-event and transport-oriented:

- each forwarded event is packaged into its own `LogMessagePacket`
- packet serialization includes event-type framing, sink id, versioning, and event payload serialization
- the packet is then written over the out-of-proc communication channel
- after arriving at the scheduler, the event is still fed into central `LoggingService`

So this path can bottleneck by adding:

1. **per-event serialization overhead** on worker nodes
2. **per-event IPC / transport overhead** across the node boundary
3. **additive scheduler-side reinjection cost** before central loggers consume the event

This is especially relevant if event volume is high and/or forwarded events have rich payloads.

## Evidence

### Out-of-proc-only wiring

- `BuildEventArgTransportSink` is created in `OutOfProcNode` during node configuration  
  - `src\Build\BackEnd\Node\OutOfProcNode.cs:775-817`
- the wiki describes it specifically in the OOP-node flow  
  - `documentation\wiki\Logging-Internals.md:50-53,98-101`

This is the strongest evidence that the candidate is mostly irrelevant for pure one-process builds.

### No batching in `BuildEventArgTransportSink`

`Consume(BuildEventArgs buildEvent, int sinkId)`:

- special-cases `BuildStartedEventArgs` and `BuildFinishedEventArgs` only to set flags and skip transport
- otherwise creates one `LogMessagePacket`
- immediately calls `_sendDataDelegate(logPacket)`  
  - `src\Build\BackEnd\Components\Logging\BuildEventArgTransportSink.cs:133-149`

This is a strict packet-per-event policy. There is no grouping of multiple build events into a larger batch at this layer.

### `LogMessagePacketBase` serialization work

Each packet serializes:

- `_eventType`
- `_sinkId`
- packet version
- a flag describing whether the event serializes itself
- the event payload  
  - `src\Shared\LogMessagePacketBase.cs:365-420`

The write path also:

- uses a shared `s_writeMethodCache`
- locks that cache on lookup / population
- discovers `WriteToStream` via reflection
- creates a closed delegate using `CreateDelegateRobust(...)`
- invokes event-specific serialization  
  - `src\Shared\LogMessagePacketBase.cs:268-270,388-415,479-500`

That means the path is not “just copy bytes already prepared elsewhere”; packet translation itself performs real work.

### Richer event types can be expensive

`LogMessagePacket` overrides serialization for selected event types instead of using the default event `WriteToStream` path:

- `ProjectEvaluationStartedEvent`
- `ProjectEvaluationFinishedEvent`
- `ResponseFileUsedEvent`  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:57-97`

For `ProjectEvaluationFinishedEvent`, the packet writes:

- evaluation event basics
- global properties
- properties
- items
- profiler result  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:102-109`

And helper methods flatten these structures:

- `WriteProperties(...)` enumerates properties into a reusable list and writes each pair  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:184-212`
- `WriteItems(...)` enumerates items into a thread-static reusable list and writes them  
  - `src\Build\BackEnd\Components\Communications\LogMessagePacket.cs:214-240`

So transport cost varies materially by event kind; evaluation-heavy payloads are notably more expensive than simple messages.

### Transport is still packet-at-a-time below this layer

Node communication serializes each packet into a write stream and writes it to the pipe, chunking only if the packet is large:

- `MaxPacketWriteSize = 1048576`  
  - `src\Build\BackEnd\Components\Communications\NodeProviderOutOfProcBase.cs:40`
- packet serialization and pipe writes occur in a loop over the serialized packet bytes  
  - `src\Build\BackEnd\Components\Communications\NodeProviderOutOfProcBase.cs:1219-1234`

This chunking is not event batching; it is fragmentation of one packet for transport.

### Scheduler-side reinjection

The build manager registers `LogMessagePacket` handling on the logging service:

- `src\Build\BackEnd\BuildManager\BuildManager.cs:692-700`

When a packet arrives:

- `LoggingService.PacketReceived(...)` casts it to `LogMessagePacket`
- calls `InjectNonSerializedData(...)`
- then calls `ProcessLoggingEvent(loggingPacket.NodeBuildEvent)`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1000-1014,1367-1387`

So the remoting path is additive: after paying transport cost, the event still enters the scheduler’s serialized logging path.

## Likelihood

### One-process multi-project parallel builds

**Low.**

Reason: this path is primarily out-of-proc. If work stays in one process, `BuildEventArgTransportSink` and `LogMessagePacket` are mostly out of the picture.

### Multi-node / out-of-proc scenarios

**Medium overall.**

Why not high by default:

- many events are small
- pipe transport and translation may still be cheap relative to task / compilation work

Why not low:

- there is no batching
- each forwarded event becomes its own packet
- serialization work can be substantial for richer events
- the scheduler pays both transport decode and normal central logging afterward

I would rate it:

- **low** for pure one-process builds
- **medium** for ordinary multi-node builds
- **medium-high** for chatty multi-node builds with richer evaluation/event payloads

## Expected contention mode

### In the worker node

- repeated per-event packet creation and serialization
- possible cache-lock traffic on `s_writeMethodCache`, although mainly on first uses / lookups rather than long critical sections
- CPU overhead rather than classic coarse lock contention

### In transport

- packet-at-a-time pipe traffic
- no batching amortization across events
- possible overhead from many small packets or a few very large packets

### In the scheduler

- per-packet deserialization plus reinjection into `LoggingService`
- additive fan-in cost, not a replacement for central logging cost

Overall this looks more like a **serialization / IPC throughput tax** than a lock-heavy bottleneck.

## Where it is used

- OOP worker node logging setup  
  - `src\Build\BackEnd\Node\OutOfProcNode.cs:775-817`
- OOP forwarding-logger remoting path described in logging docs  
  - `documentation\wiki\Logging-Internals.md:50-53,98-115`
- scheduler packet handling registration  
  - `src\Build\BackEnd\BuildManager\BuildManager.cs:692-700`
- scheduler receipt and reinjection into `LoggingService`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1000-1014`

It is therefore primarily a **worker-node-to-scheduler** path.

## Why it may or may not matter in practice

### Why it may matter

- multi-node builds can generate a high volume of forwarded events
- every forwarded event is serialized separately
- richer event types can carry substantial payloads
- there is no batching to amortize per-event transport overhead
- scheduler-side central logging still happens after transport

### Why it may not matter

- pure one-process builds largely bypass this path
- many builds are dominated by project evaluation, task execution, compiler work, or the central logging path itself
- transport overhead may be modest for common small event types
- some out-of-proc scenarios of interest (for example task-host isolation) are not necessarily the dominant cost center in normal builds

Net: this is a plausible and real **multi-node logging transport** cost center, but it is not a top candidate for the narrower “one-process multi-project parallel build” question.

## How to validate

1. **Compare one-process vs multi-node builds**
   - same project graph
   - compare with in-proc-only execution vs out-of-proc nodes enabled
   - if this candidate matters, the delta should grow with forwarded event volume

2. **Trace packet counts and sizes**
   - instrument how many `LogMessagePacket`s are sent
   - measure bytes per packet and event-type distribution
   - especially separate tiny message/status packets from evaluation-finished packets

3. **Profile worker-node CPU**
   - sample:
     - `BuildEventArgTransportSink.Consume`
     - `LogMessagePacketBase.Translate`
     - `LogMessagePacketBase.WriteToStream`
     - `LogMessagePacket.WriteProperties`
     - `LogMessagePacket.WriteItems`

4. **Profile scheduler CPU**
   - sample:
     - `LoggingService.PacketReceived`
     - `LogMessagePacket` deserialization
     - `LoggingService.ProcessLoggingEvent`
   - this shows whether transport cost is material or drowned by central dispatch cost

5. **Check sensitivity to event volume**
   - compare normal builds vs evaluation-heavy or diagnostic-heavy builds
   - especially builds that forward many evaluation/property/item payloads

6. **Experiment with batching prototypes or synthetic aggregation**
   - even a synthetic prototype that groups multiple log events before send could show whether lack of batching is materially hurting throughput

The strongest confirmation would be: multi-node-only regression, significant sampled time in packet serialization/deserialization, and high packet counts with low amortization per packet.
