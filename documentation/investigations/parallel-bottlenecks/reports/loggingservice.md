# LoggingService

## Why shared

`Microsoft.Build.BackEnd.Logging.LoggingService` is the central event-ingestion and dispatch component for the build manager / in-proc node path.

- `BuildManager` creates one `LoggingService` for the build manager and the in-proc node and installs it as the logging component  
  - `src\Build\BackEnd\BuildManager\BuildManager.cs:3162-3181`
- `ProjectCollection` also creates one `LoggingService` for API-driven evaluation/build work in-process  
  - `src\Build\Definition\ProjectCollection.cs:1825-1830`
- engine logging methods for comments, errors, build lifecycle, evaluation, project, target, and task events all feed into `ProcessLoggingEvent(...)`  
  - `src\Build\BackEnd\Components\Logging\LoggingServiceLogMethods.cs:72-79,132-143,345-363,379-401,494-537,584-586,670-680,740-756,769-782,836-850`
- remoted log packets also converge into the same method via `PacketReceived(...)`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:991-1014`

So for the scoped logging/eventing area, this is the shared lane through which events pass before central loggers consume them.

## Why it might bottleneck

The architecture guarantees serialized delivery.

- in synchronous mode, producer threads take `lock (_lockObject)` and run the routing path inline  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1336-1341`
- in asynchronous mode, producers enqueue into a single queue that one background thread drains  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:253-297,1302-1328,1412-1455,1541-1560`
- the logging wiki explicitly states delivery is isolated / sequential and that a slow logger blocks the others  
  - `documentation\wiki\Logging-Internals.md:55-61,125-134`

This means the path can bottleneck in two ways:

1. **sync mode:** build threads directly pay logger routing + logger callback cost.
2. **async mode:** logger work is offloaded, but throughput is still capped by one consumer thread; if that thread falls behind, the bounded queue creates backpressure and producers block.

## Evidence

### Mode selection

The current code does not use one blanket default.

- `BuildManager` chooses **synchronous** only when `MaxNodeCount == 1 && UseSynchronousLogging`; otherwise it chooses **asynchronous**  
  - `src\Build\BackEnd\BuildManager\BuildManager.cs:3171-3178`
- CLI sets `UseSynchronousLogging = true` unless `MSBUILDLOGASYNC=1`  
  - `src\MSBuild\XMake.cs:1503-1510`
- `ProjectCollection` uses the constructor’s `useAsynchronousLogging` argument; many overloads default that to false  
  - `src\Build\Definition\ProjectCollection.cs:303-304,327-330,362-363`

### Async queue / backpressure

- queue capacity defaults to `200000`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:84,289`
- capacity is configurable via `MSBUILDLOGGINGQUEUECAPACITY`  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:323-331`
- on overflow, producers wait for dequeue progress before they can enqueue  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1319-1327`
- the queue is observable via `EventQueueCount`, explicitly for diagnosing backed-up logging  
  - `src\Build\BackEnd\Components\Logging\ILoggingService.cs:145-149`
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:517-521`

### Serial work on the shared lane

Before dispatch even reaches logger code, the path can:

- rewrite warnings into messages or errors  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1598-1689`
- track error state and error telemetry  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1692-1707`
- update per-project warning configuration maps  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1709-1723`

### Dispatch topology

Even in-proc central loggers are not called directly from producers.

- `RegisterLogger(...)` creates / reuses a `CentralForwardingLogger` and a sink  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1024-1075`
- `RegisterDistributedLogger(...)` creates the per-logger sink and local forwarding logger  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:1113-1165`
- central forwarding logger subscribes to `AnyEventRaised` and forwards every event to the redirector  
  - `src\Build\BackEnd\Components\Logging\CentralForwardingLogger.cs:78-121`
- the redirector immediately passes to a sink  
  - `src\Build\BackEnd\Components\Logging\EventRedirectorToSink.cs:51-55`
- `EventSourceSink.Consume(...)` invokes event handlers synchronously and then `AnyEventRaised`  
  - `src\Build\BackEnd\Components\Logging\EventSourceSink.cs:234-355,403-410`

This creates a multi-step but still serialized event path:

`ProcessLoggingEvent -> RouteBuildEvent -> _filterEventSource.Consume -> CentralForwardingLogger -> EventRedirectorToSink -> EventSourceSink -> actual logger callbacks`

### Queue flushing / synchronization points

Some lifecycle events explicitly flush the queue:

- build started waits for logging to drain before moving on  
  - `src\Build\BackEnd\Components\Logging\LoggingServiceLogMethods.cs:345-363`
- build finished also waits for drain  
  - `src\Build\BackEnd\Components\Logging\LoggingServiceLogMethods.cs:379-401`

That makes logging latency visible at startup / teardown even in async mode.

### Upstream pressure reduction

The service tracks `MinimumRequiredMessageImportance` based on known logger types:

- `src\Build\BackEnd\Components\Logging\LoggingService.cs:856-862,1858-1923`

And several execution paths use that to skip low-importance message generation:

- `src\Build\BackEnd\Components\RequestBuilder\TaskBuilder.cs:638-648`
- `src\Build\BackEnd\Components\RequestBuilder\TargetEntry.cs:371-379`
- `src\Build\BackEnd\Components\RequestBuilder\TaskHost.cs:937-938`

This is important counter-evidence: the system does have some built-in volume reduction.

## Likelihood

**Medium overall** for one-process multi-project parallel builds.

Refined view:

- **high** if the build emits lots of events and logger work is heavy (diagnostic verbosity, terminal logger, binary logger, custom slow loggers)
- **medium** for realistic shared-path concern under normal parallelism, because the architecture is unmistakably single-lane
- **low-medium** when event volume is modest and logger work is cheap

I would not call this an automatic top bottleneck for all one-process parallel builds, but it is a credible throughput governor whenever the logger side becomes non-trivial.

## Expected contention mode

### Synchronous mode

- contention point: `_lockObject`
- blocked party: producer / build threads
- shape: repeated per-event serialization
- consequence: logger cost is directly added to build-thread latency

### Asynchronous mode

- contention point: one queue + one consumer thread
- blocked party: initially nobody but the logging thread; later producers once queue fills
- shape: throughput governor with delayed backpressure
- consequence: queue absorbs bursts, but sustained logger slowness eventually stalls producers at enqueue

### Dispatch-level contention

- all logger callbacks for a given event are serialized
- one slow logger delays all other loggers for that event
- the cost is additive across registered central loggers / forwarding path

## Where it is used

- build manager / in-proc build execution  
  - `src\Build\BackEnd\BuildManager\BuildManager.cs:3162-3189`
- API / object-model `ProjectCollection` logging  
  - `src\Build\Definition\ProjectCollection.cs:362-363,1825-1830`
- engine logging helpers across build lifecycle  
  - `src\Build\BackEnd\Components\Logging\LoggingServiceLogMethods.cs:72-79,132-143,345-363,379-401,494-537,584-586,670-680,740-756,769-782,836-850`
- remoted worker-node log packets on the scheduler side  
  - `src\Build\BackEnd\Components\Logging\LoggingService.cs:991-1014`

For the “one-process” question specifically, the first two bullets matter most: this same service is the in-proc/eventing choke point even without considering out-of-proc packet transport.

## Why it may or may not matter in practice

### Why it may matter

- all relevant producers converge here
- delivery is intentionally sequential
- async mode does not remove serialization; it only relocates it
- queue backpressure can turn logger slowness back into producer blocking
- central loggers in-proc still pay a multi-step forwarding / sink path

### Why it may not matter

- in pure one-process scenarios, there is no extra cross-process serialization/deserialization cost on top of this path
- low-importance message suppression can significantly reduce traffic
- many builds spend far more time in evaluation, task execution, file I/O, or compiler work than in logger dispatch
- if queue depth stays low and logger CPU is small, this path remains architecturally serial without being practically dominant

Net: this is a **real shared serialization point**, but whether it becomes the bottleneck depends heavily on event volume and logger mix.

## How to validate

1. **Measure queue buildup during representative one-process parallel builds**
   - capture `ILoggingService.EventQueueCount` over time
   - look for sustained growth rather than short bursts

2. **Compare sync vs async in a controlled harness**
   - for a one-process host, run identical parallel builds with synchronous vs asynchronous logging
   - compare wall-clock time, CPU samples, and producer blocking

3. **Vary logger mix**
   - no-op / minimal logger
   - console logger
   - terminal logger
   - binary logger
   - custom logger if relevant
   - if only “heavy logger” runs regress badly, the service may be the conduit rather than the root cost

4. **Profile the shared path directly**
   - sample / trace for:
     - `LoggingService.ProcessLoggingEvent`
     - `LoggingService.RouteBuildEvent`
     - `EventSourceSink.Consume`
     - `CentralForwardingLogger.EventSource_AnyEventRaised`
   - check whether these accumulate meaningful exclusive or inclusive CPU

5. **Force earlier backpressure for experiments**
   - lower `MSBUILDLOGGINGQUEUECAPACITY`
   - if throughput or blocking changes sharply, the queue / consumer rate is material

6. **Check message-volume sensitivity**
   - compare normal vs detailed/diagnostic verbosity
   - compare builds with and without loggers that consume low-importance messages

The strongest confirmation would be: queue growth plus significant CPU in `LoggingService` / `EventSourceSink` plus observable producer stalls or build-time regression when logger work is increased.
