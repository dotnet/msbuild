# MSBuild Build Coordinator: Architecture and Flow

> **Important Note:** This document describes the architecture and design of the MSBuild Build Coordinator at a high level. 
> For current implementation details, class structures, method signatures, or specific code patterns, always consult the source code directly.
> This ensures you're working with accurate, up-to-date information.

## Overview

The **MSBuild Build Coordinator** is a resource management system that orchestrates and enforces fair-share allocation of build nodes across multiple simultaneous MSBuild processes. It prevents system resource exhaustion by maintaining a global node budget and dynamically distributing available nodes among competing builds.

The coordinator runs as a **separate process** (`MSBuild.Coordinator`), not inside `MSBuild.exe`. MSBuild clients connect to it over named pipes, request grants, and continue building with the granted node count.

### Purpose

When multiple MSBuild processes run concurrently (common in user multi-tasking), each process could independently attempt to spawn the maximum number of nodes, leading to:
- System resource exhaustion
- Excessive memory consumption
- CPU contention and slowdown
- Reduced overall build throughput

The coordinator solves this by:
1. **Enforcing a global node budget** (defaults to processor count)
2. **Implementing fair-share allocation** to distribute available nodes fairly
3. **Monitoring build health** via periodic heartbeats
4. **Allowing nested build processes to share root grants** without consuming additional global budget
5. **Auto-shutting down** after a timeout period

*Note*: The current default budget is intentionally conservative for V1. As we gather real-world usage data, we should experiment with alternative defaults (including moderate oversubscription above 1x processor count) and tune this value for better throughput without destabilizing interactive machine workloads.

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                    System with Multiple Builds                   │
└──────────────────────────────────────────────────────────────────┘

  Build 1                 Build 2                 Build 3
    │                       │                       │
    │ RequestNodes(4)       │ RequestNodes(4)       │ RequestNodes(4)
    │                       │                       │
    │ ◄── NodeGrant(4)      │ ◄── NodeGrant(4)      │ ◄── Wait(queued)
    │                       │                       │
    └───────────────────────┼───────────────────────┘
              (via Named Pipes - IPC)
                      ↓
        ┌────────────────────────────────────┐
        │   MSBuild Build Coordinator        │
        │                                    │
        │  ┌──────────────────────────────┐  │
        │  │  Node Budget Manager         │  │
        │  │  • Total Budget: 8 nodes     │  │
        │  │  • Allocated: 8              │  │
        │  │  • Available: 0              │  │
        │  └──────────────────────────────┘  │
        │                                    │
        │  ┌──────────────────────────────┐  │
        │  │  Active Builds               │  │
        │  │  • Build 1: 4 nodes          │  │
        │  │  • Build 2: 4 nodes          │  │
        │  └──────────────────────────────┘  │
        │                                    │
        │  ┌──────────────────────────────┐  │
        │  │  Waiting Builds Queue        │  │
        │  │  • Build 3: waiting          │  │
        │  └──────────────────────────────┘  │
        └────────────────────────────────────┘

  Later, when one 4-node build releases:
    Build 3 ◄── NodeGrant(4)
```

---

## Component Architecture

### Key Components

**Coordinator Server** ([src/MSBuild.Coordinator/](../src/MSBuild.Coordinator/))
- `CoordinatorServer.cs` - Main coordinator server that listens for client connections via named pipe
- `NodeBudgetManager.cs` - Implements node allocation and fair-share logic
- `CoordinatorServer.Connection.cs` - Handles initial server-side handshake and request negotiation
- `CoordinatorServer.ConnectedClient.cs` - Manages accepted client connections
- `BuildGrant.cs` - Represents a node allocation to a build
- `Program.cs` - Server launcher and singleton instance management

**Client-Side** ([src/Build/BackEnd/BuildManager/](../src/Build/BackEnd/BuildManager/))
- `CoordinatorClient.cs` - Client connection handler integrated into BuildManager
- `BuildManager.cs` - Requests nodes from coordinator and sets build parallelism

**Protocol** ([src/Framework/Coordinator/](../src/Framework/Coordinator/))
- Handshake messages: `ClientHandshakeMessage`, `ServerHandshakeMessage`
- Client messages: `RequestNodesMessage`, `JoinGrantMessage`, `HeartbeatMessage`, `ReleaseNodesMessage`
- Server messages: `NodeGrantMessage`, `WaitMessage`, `ErrorMessage`
- `Capabilities.cs` - Capability constants for feature negotiation
- `CoordinatorSettings.cs` - Configuration management

---

## Communication Protocol

### Handshake

Every connection begins with a capabilities handshake:

1. Client sends `ClientHandshakeMessage` (ConnectionId, ProcessId, capabilities)
2. Server responds with `ServerHandshakeMessage` (capabilities)

Both sides advertise the features they support; unknown capabilities are ignored, allowing older clients to work with newer servers.

### Versioning

The coordinator does not use a protocol version number. Instead, it uses a **capabilities-based** versioning model:

- Each side sends a list of capability strings during the handshake.
- A capability represents a discrete feature or behavior that both sides must agree on to use.
- Unknown capabilities received from the other side are silently ignored.
- Required behavior is gated on whether the peer advertised the corresponding capability.

This design avoids the "version bump" problem where a single version number forces all-or-nothing upgrades. New features can be added incrementally — a newer coordinator can offer capabilities that older clients simply don't use, and vice versa. Both sides degrade gracefully when a capability is absent.

### Message Types

After the handshake, the coordinator uses a binary protocol with these message types:

**Client → Server:**
- `RequestNodesMessage` - Requests a node grant (contains requested node count)
- `JoinGrantMessage` - Requests to join an existing root grant (requires `nested-grants` capability)
- `HeartbeatMessage` - Periodic keep-alive message (default: every 5 seconds)
- `ReleaseNodesMessage` - Sent when build completes, releases allocated nodes

**Server → Client:**
- `NodeGrantMessage` - Grants nodes to a build and optionally includes a root grant token (when both sides support `nested-grants`)
- `WaitMessage` - Indicates build is queued, no nodes immediately available
- `ErrorMessage` - Indicates an error condition

**Source:** [src/Framework/Coordinator/](../src/Framework/Coordinator/)

### Message Flow Example

```
Successful Grant:
  Build → ClientHandshakeMessage(ConnectionId, PID, capabilities)
  Build ← ServerHandshakeMessage(capabilities)
  Build → RequestNodesMessage(4)
  Build ← NodeGrantMessage(4)
  Build → Heartbeat (every 5s)
  Build → ReleaseNodesMessage (on completion)

Build Queued:
  Build → ClientHandshakeMessage(ConnectionId, PID, capabilities)
  Build ← ServerHandshakeMessage(capabilities)
  Build → RequestNodesMessage(4)
  Build ← WaitMessage
  Build → Heartbeat (every 5s while waiting)
  Eventually: Build ← NodeGrantMessage(N) [N is fair-share computed from available nodes and contenders, capped by requested nodes (N <= 4 here)]

Nested Build:
  Root Build → ClientHandshakeMessage(..., capabilities: nested-grants)
  Root Build ← ServerHandshakeMessage(..., capabilities: nested-grants)
  Root Build → RequestNodesMessage(4)
  Root Build ← NodeGrantMessage(grantId, 4)
  Nested Build inherits grantId from the root build process environment
  Nested Build → ClientHandshakeMessage(..., capabilities: nested-grants)
  Nested Build ← ServerHandshakeMessage(..., capabilities: nested-grants)
  Nested Build → JoinGrantMessage(grantId, 4)
  Nested Build ← NodeGrantMessage(grantId, 4)
```

---

## Nested Grants

Some build operations launch child MSBuild processes while the parent build still owns its coordinator grant. A common example is NuGet static-graph restore, which can invoke a nested process that uses the MSBuild APIs as part of an already-running coordinated build.

Without nested grants, the child process could request a new root grant while the parent is still holding the full budget. If no budget is available, the child waits for resources that the parent cannot release until the child completes, causing a deadlock.

Nested grants avoid this by treating child processes as participants in the parent grant:

1. The root build receives a grant token in `NodeGrantMessage`.
2. `BuildManager` records that token in the build process environment as `MSBUILDCOORDINATORGRANTID`, so task-launched child processes inherit it.
3. A child process that sees the token and a server that supports `nested-grants` sends `JoinGrantMessage`.
4. The coordinator validates that the root grant is still active.
5. If valid, the child receives a grant capped by the root grant's node count without consuming additional global budget.
6. Releasing a nested grant does not release global budget or invalidate the root grant token.

Nested grants do not implement a scheduler within the root grant. Each nested process is capped by the root grant's node count, but the coordinator does not track combined concurrency across the root process and all nested participants. The root build is expected to coordinate its own nested work so it does not oversubscribe the resources it was granted.

Nested grant validation happens when the nested process joins the root grant. If the root grant is released later, the coordinator rejects new joins for that grant ID, but it does not revoke nested grants that were already issued. Those nested builds continue until they release or disconnect.

Nested grants are capability-gated. Older peers that do not advertise `nested-grants` continue to exchange `NodeGrantMessage` using the legacy int-only wire shape (`GrantId` is `Guid.Empty` and never sent on the wire), and clients only send `JoinGrantMessage` when the server advertises the capability.

---

## Fair-Share Allocation Algorithm

### Core Concept

When multiple builds compete for limited nodes, the coordinator computes a fair share per grant. The contender count depends on the phase:

Initial request path (`TryGrant`):

```
fair_share = max(1, available_nodes / (waiting_builds + 1))
granted_nodes = min(fair_share, requested_nodes)
```

Wait-queue drain path (`DrainWaitQueue`):

```
fair_share = max(1, available_nodes / waiting_builds)
granted_nodes = min(fair_share, requested_nodes)
```

This ensures:
- Each grant is capped by what the build requested
- Available nodes are divided across contenders in the current phase
- Wait-queue entries are processed FIFO as nodes become available

### Example Scenarios

**First Build Requests Full Budget** (8 total nodes)
- No builds are active and no builds are waiting
- Build A requests 8 nodes
- Fair share on initial request path: max(1, 8 / (0 + 1)) = 8 nodes
- Build A granted min(8, 8) = 8 nodes

**Three Full-Budget Requests Launched Together** (8 total nodes)
- Builds A, B, and C are launched at roughly the same time, each requesting 8 nodes
  - *Note*: This is the default when `MaxNodeCount` is not specified: each build requests `Environment.ProcessorCount` (the full default budget)
- First processed request (A): available 8, waiting 0 → fair_share = max(1, 8 / (0 + 1)) = 8 → A granted 8
- Next processed requests (B, then C): available 0, so both receive `WaitMessage` and enter the wait queue
- When A releases, drain begins with available 8 and 2 waiters:
- B: fair_share = max(1, 8 / 2) = 4 → granted 4
- C: fair_share = max(1, 4 / 1) = 4 → granted 4

**Queued Mixed-Demand Scenario** (8 total nodes)
- Build A and Build X are active with 4 nodes each (budget fully allocated)
- Build B requests 6 and Build C requests 8 while available is 0, so both are queued
- When Build A releases, drain begins with available 4 and 2 waiters
- Build B: fair_share = max(1, 4 / 2) = 2 → granted min(2, 6) = 2
- Build C: fair_share = max(1, 2 / 1) = 2 → granted min(2, 8) = 2

---

## Integration with BuildManager

### How Coordination Works

**During build initialization:**

1. BuildManager checks if `MSBUILDUSECOORDINATOR` environment variable is set
2. If enabled, `CoordinatorClient` attempts to connect to the coordinator
3. Sends `RequestNodesMessage` with desired node count (the value of `/maxcpucount` passed to MSBuild — defaults to 1 if omitted, or the logical processor count if `/m` is passed without a value)
4. Receives either `NodeGrantMessage` (nodes granted) or `WaitMessage` (queued)
   - *Note*: If `WaitMessage` is received, `CoordinatorClient` starts sending periodic heartbeats while waiting for the deferred `NodeGrantMessage`, so the coordinator doesn't consider it stale during the queue wait.
5. Updates build's maximum node count based on grant
6. If the grant includes a grant ID, records it in the build process environment so nested child processes can join the root grant

> *V1 Behavior*: The number of nodes granted to a build is fixed at initialization and does not change during the build's lifetime. The grant persists as long as the build is running (indicated by heartbeats) and is released only when the build completes.

**During build execution:**

7. BuildManager spawns build nodes up to the maximum node count, which may have been limited by the number of nodes granted by the coordinator
8. `CoordinatorClient` continues sending periodic heartbeats to indicate the build is still active

**On build completion:**

9. Sends `ReleaseNodesMessage` to free nodes for other waiting builds

**Key Principle:** The coordinator is entirely optional. If it's unavailable or disabled, the build uses its requested node count without coordination.

**Sources:**
- [src/Build/BackEnd/BuildManager/BuildManager.cs](../src/Build/BackEnd/BuildManager/BuildManager.cs)
- [src/Build/BackEnd/BuildManager/CoordinatorClient.cs](../src/Build/BackEnd/BuildManager/CoordinatorClient.cs)
- [src/Framework/Traits.cs](../src/Framework/Traits.cs) - Enablement logic

---

## Configuration and Environment Variables

### Environment Variables

| Variable | Default | Purpose |
|----------|---------|---------|
| `MSBUILDUSECOORDINATOR` | (empty) | Enable coordinator (set to any value to enable) |
| `MSBUILDCOORDINATORPIPENAME` | `msbuild-coordinator-{UserName}` | Override default pipe name |
| `MSBUILDCOORDINATORNODEBUDGET` | Processor count | Override total node budget |
| `MSBUILDCOORDINATORHEARTBEAT` | 5000 | Override heartbeat interval (ms) |
| `MSBUILDCOORDINATORSHUTDOWNTIMEOUT` | 60000 | Override shutdown timeout (ms) |
| `MSBUILDCOORDINATORGRANTID` | (empty) | Internal token used by child processes to join an active root grant |

*Note*: `MSBUILDCOORDINATORNODEBUDGET` is the primary knob for throughput experiments, including testing moderate oversubscription factors above 1x processor count.

---

## Lifecycle and Operation

### Coordinator Startup

1. When first MSBuild process needs coordination, it performs a fast pipe probe (~200ms)
2. If no coordinator is running, it acquires a launch mutex to serialize launch attempts
3. Checks the coordinator's server mutex to determine if another client already launched one
4. If not, launches the coordinator process and polls for its server mutex to appear
5. Releases the launch mutex so concurrent clients can wait for the pipe in parallel
6. Connects to the coordinator's named pipe once it's ready
7. Coordinator uses a system-wide mutex to ensure only one instance runs

**Source:** [src/MSBuild.Coordinator/Program.cs](../src/MSBuild.Coordinator/Program.cs)

### Heartbeat Monitoring

The coordinator detects stalled or crashed clients through periodic heartbeats:

- Clients send heartbeat messages at configured intervals (default: 5 seconds)
- Coordinator tracks missed heartbeats
- After threshold is reached (default: 3 misses = 15 seconds), client is considered stalled
- Coordinator automatically releases nodes allocated to stalled client
- Waiting builds can then be granted those nodes

**Source:** [src/MSBuild.Coordinator/CoordinatorServer.cs](../src/MSBuild.Coordinator/CoordinatorServer.cs)

### Graceful Shutdown

When a build completes normally:

1. Client sends `ReleaseNodesMessage` with its grant ID
2. Coordinator frees those nodes
3. Processes waiting queue to allocate freed nodes to waiting builds
4. If no active or waiting clients remain, coordinator enters timeout mode
5. After 60 seconds of inactivity, coordinator exits

**Source:** [src/MSBuild.Coordinator/CoordinatorServer.cs](../src/MSBuild.Coordinator/CoordinatorServer.cs)

---

## Error Handling

### Resilient Design

The coordinator system is designed to be fully optional:

- **Unavailable coordinator** → Build proceeds without coordination using full node count
- **Connection failure** → Build proceeds independently
- **Unsupported capabilities** → Unknown capabilities are ignored; both sides degrade gracefully
- **Crashed client** → Detected via heartbeat timeout, resources cleaned up
- **Coordinator crash** → Next build can launch new instance

This means coordinator failures never block or degrade build execution—they only disable coordination.

**Sources:**
- [src/Build/BackEnd/BuildManager/CoordinatorClient.cs](../src/Build/BackEnd/BuildManager/CoordinatorClient.cs)
- [src/MSBuild.Coordinator/CoordinatorServer.cs](../src/MSBuild.Coordinator/CoordinatorServer.cs)

---

## Testing

### Unit Tests

Comprehensive test coverage in [src/MSBuild.Coordinator.UnitTests/](../src/MSBuild.Coordinator.UnitTests/):

- Protocol serialization/deserialization
- Node budget manager allocation logic
- Fair-share algorithm correctness
- Heartbeat monitoring
- Multi-build coordination scenarios
- Error conditions and edge cases

---

## Source Code References

For detailed implementation information, refer to:

- **Server Implementation:** [src/MSBuild.Coordinator/](../src/MSBuild.Coordinator/)
- **Protocol Definitions:** [src/Framework/Coordinator/](../src/Framework/Coordinator/)
- **Client Integration:** [src/Build/BackEnd/BuildManager/](../src/Build/BackEnd/BuildManager/)
- **Configuration:** [src/Framework/Traits.cs](../src/Framework/Traits.cs), [src/Framework/Coordinator/CoordinatorSettings.cs](../src/Framework/Coordinator/CoordinatorSettings.cs)
- **Tests:** [src/MSBuild.Coordinator.UnitTests/](../src/MSBuild.Coordinator.UnitTests/)
