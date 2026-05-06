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
4. **Auto-shutting down** after a timeout period

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
- `ClientConnection.cs` - Manages individual client connections
- `BuildGrant.cs` - Represents a node allocation to a build
- `Program.cs` - Server launcher and singleton instance management

**Client-Side** ([src/Build/BackEnd/BuildManager/](../src/Build/BackEnd/BuildManager/))
- `CoordinatorClient.cs` - Client connection handler integrated into BuildManager
- `BuildManager.cs` - Requests nodes from coordinator and sets build parallelism

**Protocol** ([src/Framework/Coordinator/](../src/Framework/Coordinator/))
- Message types: `RequestNodesMessage`, `HeartbeatMessage`, `ReleaseNodesMessage`, `NodeGrantMessage`, `WaitMessage`, `ErrorMessage`
- `CoordinatorSettings.cs` - Configuration management
- `Protocol.cs` - Protocol versioning

---

## Communication Protocol

### Message Types

The coordinator uses a binary protocol with six message types:

**Client → Server:**
- `RequestNodesMessage` - Sent when a build starts, requests a node grant
- `HeartbeatMessage` - Periodic keep-alive message (default: every 5 seconds)
- `ReleaseNodesMessage` - Sent when build completes, releases allocated nodes

**Server → Client:**
- `NodeGrantMessage` - Grants nodes to a build
- `WaitMessage` - Indicates build is queued, no nodes immediately available
- `ErrorMessage` - Indicates an error condition (e.g., protocol version mismatch)

Each message includes a protocol version for compatibility verification.

**Source:** [src/Framework/Coordinator/](../src/Framework/Coordinator/)

### Message Flow Example

```
Successful Grant:
  Build → RequestNodesMessage(4)
  Build ← NodeGrantMessage(4)
  Build → Heartbeat (every 5s)
  Build → ReleaseNodesMessage (on completion)

Build Queued:
  Build → RequestNodesMessage(4)
  Build ← WaitMessage
  Build → Heartbeat (every 5s while waiting)
  Eventually: Build ← NodeGrantMessage(N) [N is fair-share computed from available nodes and contenders, capped by requested nodes (N <= 4 here)]
```

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
3. Sends `RequestNodesMessage` with desired node count
4. Receives either `NodeGrantMessage` (nodes granted) or `WaitMessage` (queued)
   - *Note*: If `WaitMessage` is received, `CoordinatorClient` starts sending periodic heartbeats while waiting for the deferred `NodeGrantMessage`, so the coordinator doesn't consider it stale during the queue wait.
5. Updates build's maximum node count based on grant

> *Note*: The number of nodes granted to a build is fixed at initialization and does not change during the build's lifetime. The grant persists as long as the build is running (indicated by heartbeats) and is released only when the build completes.

**During build execution:**

6. BuildManager spawns build nodes up to the maximum node count, which may have been limited by the number of nodes granted by the coordinator
7. `CoordinatorClient` continues sending periodic heartbeats to indicate the build is still active

**On build completion:**

8. Sends `ReleaseNodesMessage` to free nodes for other waiting builds

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

*Note*: `MSBUILDCOORDINATORNODEBUDGET` is the primary knob for throughput experiments, including testing moderate oversubscription factors above 1x processor count.

---

## Lifecycle and Operation

### Coordinator Startup

1. When first MSBuild process needs coordination, it attempts to start the coordinator
2. Coordinator uses a system-wide mutex to ensure only one instance runs
3. If an instance already exists, the new process connects as a client instead
4. Coordinator listens on a named pipe for client connections

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
- **Protocol mismatch** → Graceful fallback to unlimited nodes
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
