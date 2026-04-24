# MSBuild Build Coordinator

## Overview

The Build Coordinator is an external process that manages node budgets across concurrent MSBuild instances. It prevents resource contention by limiting the total number of MSBuild worker nodes, admitting builds up to a concurrency limit, and queuing excess builds until capacity opens.

Builds communicate with the coordinator over a **named pipe** using a line-based text protocol (one command per connection, one response per connection). Each command opens a new pipe connection — there are no persistent sessions.

## Design

### Layered Architecture

```
┌─────────────────────────────────────────────────────┐
│  BuildManager.Coordinator.cs                        │  Integration layer
│  (partial class — opt-in via MSBUILDCOORDINATORENABLED) │
│  Sets MaxNodeCount from coordinator grant           │
└──────────────────────┬──────────────────────────────┘
                       │ uses
┌──────────────────────▼──────────────────────────────┐
│  NamedPipeCoordinatorClient                        │  Client transport
│  Named pipe commands, wait-pipe blocking,           │
│  heartbeat timer, retry logic                       │
└─────────────────────────────────────────────────────┘

                    named pipe

┌─────────────────────────────────────────────────────┐
│  NamedPipeCoordinatorHost                           │  Server transport
│  Pipe listener, protocol parsing, wait-pipe         │
│  notifications, console logging                     │
│  Creates and owns the BuildCoordinator              │
└──────────────────────┬──────────────────────────────┘
                       │ owns
┌──────────────────────▼──────────────────────────────┐
│  BuildCoordinator                                   │  Pure domain logic
│  FIFO queue, capacity gating, staleness reaping,    │
│  promotion callbacks — zero I/O                     │
└──────────────────────┬──────────────────────────────┘
                       │ delegates to
┌──────────────────────▼──────────────────────────────┐
│  INodeBudgetPolicy / FairShareBudgetPolicy          │  Budget strategy
│  Pluggable grant calculation                        │
└─────────────────────────────────────────────────────┘
```

### Separation of Concerns

| Layer | File(s) | Responsibility | I/O? |
|-------|---------|----------------|------|
| Domain | `BuildCoordinator.cs` | Registration, queuing, promotion, reaping | None |
| Policy | `INodeBudgetPolicy.cs`, `FairShareBudgetPolicy.cs` | Budget grant calculation | None |
| Server transport | `NamedPipeCoordinatorHost.cs` | Pipe listener, protocol parsing, promotion notifications | Yes |
| Client transport | `NamedPipeCoordinatorClient.cs` | Pipe commands, wait-pipe blocking, heartbeats | Yes |
| Integration | `BuildManager.Coordinator.cs` | Opt-in glue into `BuildManager` | Minimal |

**Key design decisions:**
- **`BuildCoordinator` has zero I/O.** It can be fully tested in-memory with no pipes, no timers, no threads.
- **The host owns the coordinator.** `NamedPipeCoordinatorHost` constructs the `BuildCoordinator` internally, passing its own `NotifyPromotedBuild` method as the promotion callback via constructor injection. This eliminates mutable callback properties and makes the ownership/lifecycle explicit.
- **No transport interface.** There is one transport implementation (named pipes). An `ICoordinatorHost` abstraction would be premature — extract one if a second transport is ever needed.
- **Wait pipe naming is a transport concern.** The `GetWaitPipeName` convention lives on `NamedPipeCoordinatorHost`, not on the domain coordinator.

### Relationship to MSBuild's Internal Scheduler

The coordinator sits **above** MSBuild's existing `Scheduler` (in `BackEnd/Components/Scheduler/`):

- **Coordinator** controls how many worker **nodes** each MSBuild **process** gets (`MaxNodeCount`)
- **Scheduler** controls how build **requests** are dispatched to **nodes** within a single process

They are complementary layers. The coordinator sets the budget; the Scheduler operates within it.

## Protocol

### Architecture

```
┌──────────────────┐     named pipe      ┌────────────────────┐
│  MSBuild Build 1 │ ──── REGISTER ────→ │                    │
│  (BuildManager)  │ ←─── OK 4 ────────- │                    │
│                  │ ──── HEARTBEAT ───→ │  Build Coordinator │
│                  │ ←─── OK ──────────- │  (standalone proc) │
│                  │ ──── UNREGISTER ──→ │                    │
├──────────────────┤                     │                    │
│  MSBuild Build 2 │ ──── REGISTER ────→ │                    │
│  (queued)        │ ←─── QUEUED 1 1 ──- │                    │
│                  │                     │                    │
│  ┌─wait pipe───┐ │ ←─── OK 4 ────────- │                    │
│  │ blocks here │ │   (on promotion)    │                    │
│  └─────────────┘ │                     └────────────────────┘
└──────────────────┘
```

### Pipe Names

#### Coordinator Pipe (main command pipe)

Both sides derive the same well-known name:

| Platform | Pipe Name |
|----------|-----------|
| Unix     | `/tmp/MSBuild-Coordinator-{username}` |
| Windows  | `MSBuild-Coordinator-{username}` |

#### Wait Pipe (per-build, for queued builds)

When a build is queued, it creates a pipe derived by convention:

```
{coordinator-pipe}-{buildId}
```

For example: `/tmp/MSBuild-Coordinator-jakerad-12345-638765432100000000`

Neither side transmits the wait pipe name — both derive it from the coordinator pipe name and the build ID that was established during `REGISTER`.

### Build ID Format

```
{PID}-{DateTime.UtcNow.Ticks}
```

Example: `12345-638765432100000000`

The PID prefix enables the coordinator to detect dead builds via `Process.GetProcessById()`.

## Configuration

### Enabling the Coordinator

The client-side integration is gated by an environment variable:

```
MSBUILDCOORDINATORENABLED=1
```

When unset or `!= "1"`, `BuildManager` skips coordinator registration entirely and the build runs with its default `MaxNodeCount`.

### Coordinator Parameters

The coordinator accepts an `INodeBudgetPolicy` that controls budget allocation. The built-in `FairShareBudgetPolicy` provides opinionated defaults:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `totalBudget` | 90% of cores | Total node budget shared across all active builds |
| `maxConcurrentBuilds` | totalBudget / 4 | Max number of builds active simultaneously |
| `startupDelayMs` | 0 | Optional delay before first admission (batches initial registrations) |

Command-line: `msbuild --coordinator [--budget N] [--max-builds N] [--startup-delay N]`

With no arguments, the coordinator auto-detects from the machine's core count.

### Wire Format

All communication is **line-based text** over named pipes. Each command is a single pipe connection: client connects, writes one line, reads the response, and disconnects.

#### REGISTER

Registers a new build and requests a node budget.

**Request:**
```
REGISTER {buildId} {requestedNodes}
```

**Response (activated — has capacity):**
```
OK {grantedNodes}
```

**Response (at capacity — queued):**
```
QUEUED {position} {totalQueued}
```

**Behavior:**
1. If `activeCount < maxConcurrentBuilds` → admit immediately, respond `OK {granted}`.
2. If a startup delay is active → queue unconditionally (batching).
3. If there are already queued builds → queue (strict FIFO).
4. Otherwise → queue.

When queued, the client creates a per-build wait pipe and blocks (see [Queuing and Promotion](#queuing-and-promotion)).

#### HEARTBEAT

Periodic liveness signal from an active build. The granted node budget is **fixed for the lifetime of a build** — `MaxNodeCount` is set once at registration and never changes. The budget value in the heartbeat response is used only for the coordinator's internal bookkeeping (e.g. deciding when to promote queued builds).

**Request:**
```
HEARTBEAT {buildId}
```

**Response:**
```
OK
```

**Behavior:**
- Updates `LastHeartbeat` timestamp (used by staleness reaper).
- Response is a plain ack — no budget is returned since it cannot change.
- Queued builds do **not** send heartbeats (they block on the wait pipe).

#### UNREGISTER

Removes a build (active or queued) and triggers promotion of queued builds.

**Request:**
```
UNREGISTER {buildId}
```

**Response:**
```
OK
OK promoted {count}
```

**Behavior:**
1. Remove from active builds (or queue if still queued).
2. If the build was active, promote as many queued builds as capacity allows.
3. Each promotion connects to the queued build's wait pipe and sends the grant.

#### STATUS

Diagnostic query — returns current coordinator state.

**Request:**
```
STATUS
```

**Response:**
```
OK budget={totalBudget} active={count} queued={count} max={maxConcurrentBuilds}
```

#### SHUTDOWN

Gracefully stops the coordinator.

**Request:**
```
SHUTDOWN
```

**Response:**
```
OK
```

#### Error Responses

```
ERR unknown command
ERR usage: REGISTER buildId requestedNodes
ERR invalid requestedNodes
ERR usage: HEARTBEAT buildId
ERR usage: UNREGISTER buildId
```

### Queuing and Promotion

When a build receives `QUEUED`, it enters a blocking wait using a per-build named pipe:

#### Client Side (NamedPipeCoordinatorClient)

1. Derive the wait pipe name: `{coordinatorPipe}-{buildId}`
2. Create a `NamedPipeServerStream` (direction: `In`, max instances: 1, `CurrentUserOnly`)
3. Call `WaitForConnectionAsync()` — blocks until the coordinator connects
4. Read one line from the pipe
5. Expect `OK {grantedNodes}` → set `MaxNodeCount` and proceed
6. On cancellation → send `UNREGISTER` to the coordinator

#### Coordinator Side (NamedPipeCoordinatorHost)

When a slot opens (via `UNREGISTER` or staleness reaping):

1. Remove the first build from the FIFO queue
2. Add it to the active set, calculate its budget
3. Derive the wait pipe name: `{coordinatorPipe}-{buildId}`
4. Connect to the client's wait pipe as a `NamedPipeClientStream` (direction: `Out`, 5s timeout)
5. Write `OK {grantedNodes}`
6. Disconnect

This happens **outside** the queue lock to avoid blocking I/O under lock.

## Budget Calculation

Budget allocation is delegated to a pluggable `INodeBudgetPolicy`. The built-in `FairShareBudgetPolicy` divides the **remaining** budget evenly across all slots:

```
remaining   = totalBudget - allocatedNodes   (nodes already granted to other active builds)
slotsToFill = max(1, maxConcurrentBuilds - alreadyGrantedBuilds)
fairShare   = max(1, remaining / slotsToFill)
granted     = min(fairShare, requestedNodes)
```

**Key property:** Total grants never exceed `totalBudget`, regardless of arrival order. Each build gets `totalBudget / maxConcurrentBuilds` nodes when all slots fill up.

**Example** (budget=24, max=4, all builds request 12):
| Build | Remaining | Slots | Fair Share | Granted |
|-------|-----------|-------|------------|---------|
| 1st   | 24        | 4     | 6          | 6       |
| 2nd   | 18        | 3     | 6          | 6       |
| 3rd   | 12        | 2     | 6          | 6       |
| 4th   | 6         | 1     | 6          | 6       |
| **Total** | | | | **24** |

**Custom policies:** Implement `INodeBudgetPolicy` to provide alternative strategies (e.g. priority-based, weighted, or resource-aware allocation).

## Staleness Reaping

A periodic timer (default: every 5s) checks for dead builds:

**Active builds:** If `LastHeartbeat` is older than `StaleHeartbeatSeconds` (default: 10s) **and** the PID extracted from `buildId` is no longer running → remove from active set and promote queued builds.

**Queued builds:** Since queued builds don't heartbeat, the reaper checks process liveness directly. If the PID is dead → remove from queue.

## Startup Delay

When `startupDelayMs > 0`, the coordinator queues **all** arriving builds during the delay window. When the timer fires, it batch-promotes up to `maxConcurrentBuilds` at once. This ensures fair budget distribution when many builds launch simultaneously.

## Connection Resilience

The client's `SendCommand()` retries on pipe connection failures:

| Parameter | Value |
|-----------|-------|
| Connect timeout | 5000ms |
| Max attempts | 5 |
| Retry delay | 500ms |

If all attempts fail, `TryRegister()` returns `false` and the build runs with its original `MaxNodeCount`, uncoordinated.

## BuildManager Integration

The coordinator integration lives in `BuildManager.Coordinator.cs` (partial class):

1. **Before build execution:** `TryRegisterWithCoordinator()` is called.
   - If coordinator is running and responds → `MaxNodeCount` is set to `grantedNodes`. This value is **fixed for the entire build**.
   - If coordinator is not running or registration fails → build proceeds normally with its default `MaxNodeCount`.
2. **During build:** `StartHeartbeat()` sends periodic liveness signals (every 2s). The node budget does not change.
3. **After build:** `UnregisterFromCoordinator()` calls `Unregister()` and disposes the client.
4. **Node reuse:** When `IsCoordinatorManaged` is true, node reuse is disabled so worker nodes exit immediately when the build completes, freeing resources for other builds.

## Sequence Diagrams

### Normal Registration (has capacity)

```
Build                    Coordinator
  │                          │
  │── REGISTER id 12 ──────→│
  │                          │ activeCount < max, budget available
  │←── OK 4 ────────────────│
  │                          │
  │── HEARTBEAT id ─────────→│ (every 2s)
  │←── OK ──────────────────│
  │     ...                  │
  │── UNREGISTER id ────────→│
  │←── OK ──────────────────│
```

### Queued Build (at capacity)

```
Build A                  Coordinator              Build B (queued)
  │                          │                        │
  │── REGISTER A 12 ────────→│                        │
  │←── OK 4 ────────────────│                        │
  │                          │                        │
  │                          │←── REGISTER B 12 ─────│
  │                          │──── QUEUED 1 1 ───────→│
  │                          │                        │
  │                          │                  ┌─────────────┐
  │                          │                  │ create wait  │
  │                          │                  │ pipe, block  │
  │                          │                  └─────────────┘
  │                          │                        │
  │── UNREGISTER A ─────────→│                        │
  │←── OK promoted 1 ──────│                        │
  │                          │                        │
  │                          │── connect to B's ─────→│
  │                          │   wait pipe            │
  │                          │── OK 4 ───────────────→│
  │                          │                        │
  │                          │                  ┌─────────────┐
  │                          │                  │ unblocks,    │
  │                          │                  │ MaxNodeCount │
  │                          │                  │ = 4          │
  │                          │                  └─────────────┘
```
