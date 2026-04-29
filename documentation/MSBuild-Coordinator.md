# MSBuild Build Coordinator: Architecture and Flow

> **Important Note:** This document describes the architecture and design of the MSBuild Build Coordinator at a high level. 
> For current implementation details, class structures, method signatures, or specific code patterns, always consult the source code directly.
> This ensures you're working with accurate, up-to-date information.

## Overview

The **MSBuild Build Coordinator** is a resource management system that orchestrates and enforces fair-share allocation of build nodes across multiple simultaneous MSBuild processes. It prevents system resource exhaustion by maintaining a global node budget and dynamically distributing available nodes among competing builds.

### Purpose

When multiple MSBuild processes run concurrently (common in CI/CD environments, distributed builds, or user multi-tasking), each process could independently attempt to spawn the maximum number of nodes, leading to:
- System resource exhaustion
- Excessive memory consumption
- CPU contention and slowdown
- Reduced overall build throughput

The coordinator solves this by:
1. **Enforcing a global node budget** (defaults to processor count)
2. **Implementing fair-share allocation** to distribute available nodes fairly
3. **Monitoring build health** via periodic heartbeats
4. **Auto-shutting down** after a timeout period

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                    System with Multiple Builds                   │
└──────────────────────────────────────────────────────────────────┘

  Build 1               Build 2               Build 3
    │                     │                     │
    │ RequestNodes(4)     │ RequestNodes(4)     │ RequestNodes(4)
    │                     │                     │
    └─────────────────────┼─────────────────────┘
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
            ↓                      ↓
    Grant(nodes=2)        Wait(queued)
```

---

## Component Architecture

### Key Components

**Coordinator Server** ([src/MSBuild.Coordinator/](src/MSBuild.Coordinator/))
- `CoordinatorServer.cs` - Main server that listens for client connections via named pipe
- `NodeBudgetManager.cs` - Implements node allocation and fair-share logic
- `ClientConnection.cs` - Manages individual client connections
- `BuildGrant.cs` - Represents a node allocation to a build
- `Program.cs` - Server launcher and singleton instance management

**Client-Side** ([src/Build/BackEnd/BuildManager/](src/Build/BackEnd/BuildManager/))
- `CoordinatorClient.cs` - Client connection handler integrated into BuildManager
- `BuildManager.cs` - Requests nodes from coordinator and sets build parallelism

**Protocol** ([src/Framework/Coordinator/](src/Framework/Coordinator/))
- Message types: `RequestNodesMessage`, `HeartbeatMessage`, `ReleaseNodesMessage`, `NodeGrantMessage`, `WaitMessage`, `ErrorMessage`
- `CoordinatorSettings.cs` - Configuration management
- `Protocol.cs` - Protocol versioning

### Directory Structure

```
src/
├── MSBuild.Coordinator/                           # Coordinator server executable
│   ├── CoordinatorServer.cs
│   ├── NodeBudgetManager.cs
│   ├── ClientConnection.cs
│   ├── BuildGrant.cs
│   ├── Program.cs
│   └── ...
├── Framework/Coordinator/                         # Protocol and interfaces
│   ├── RequestNodesMessage.cs
│   ├── HeartbeatMessage.cs
│   ├── ReleaseNodesMessage.cs
│   ├── NodeGrantMessage.cs
│   ├── WaitMessage.cs
│   ├── ErrorMessage.cs
│   ├── CoordinatorSettings.cs
│   ├── Protocol.cs
│   └── ...
├── Build/BackEnd/BuildManager/
│   ├── BuildManager.cs
│   ├── CoordinatorClient.cs
│   └── ...
└── MSBuild.Coordinator.UnitTests/
    ├── CoordinatorServerTests.cs
    ├── NodeBudgetManagerTests.cs
    └── ...
```

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

**Source:** [src/Framework/Coordinator/](src/Framework/Coordinator/)

### Message Flow Example

```
Successful Grant:
  Build → RequestNodesMessage(4)
  Build ← NodeGrantMessage(4)
  Build → Heartbeat (every 5s)
  Build → ReleaseNodesMessage (on completion)

Build Queued:
  Build → RequestNodesMessage(4)
  Build ← WaitMessage (queue position 1)
  Build → Heartbeat (every 5s while waiting)
  Eventually: Build ← NodeGrantMessage(2) [after fair-share calculation]
```

---

## Fair-Share Allocation Algorithm

### Core Concept

When multiple builds compete for limited nodes, the coordinator distributes them fairly using:

```
fair_share = max(1, available_nodes / (waiting_builds + 1))
```

This ensures:
- Every waiting build gets at least 1 node
- Available nodes are divided equally among contenders
- Nodes are processed from the wait queue as they become available

### Example Scenarios

**Two Competing Builds** (8 total nodes)
- Build A gets 4 nodes (active)
- Build B requests 4 nodes → Available: 4, Waiting: 1
- Fair share: max(1, 4 / 2) = 2 nodes
- Build B granted 2 nodes

**Multiple Builds in Queue** (8 total nodes)
- Build A uses 4 nodes
- Build B waiting (wants 6) and Build C waiting (wants 8)
- When A completes: 4 nodes available, 2 waiting
- Build B: fair_share = max(1, 4 / 2) = 2 nodes
- Build C: fair_share = max(1, 2 / 1) = 2 nodes

---

## Integration with BuildManager

### How Coordination Works

During build initialization:

1. BuildManager checks if `MSBUILDUSECOORDINATOR` environment variable is set
2. If enabled, `CoordinatorClient` attempts to connect to the coordinator
3. Sends `RequestNodesMessage` with desired node count
4. Receives either `NodeGrantMessage` (nodes granted) or `WaitMessage` (queued)
5. Updates build's maximum node count based on grant
6. During execution, spawns build nodes limited by this capped value
7. On completion, sends `ReleaseNodesMessage` to free nodes for other builds

**Key Principle:** The coordinator is entirely optional. If it's unavailable or disabled, the build uses its requested node count without coordination.

**Sources:**
- [src/Build/BackEnd/BuildManager/BuildManager.cs](src/Build/BackEnd/BuildManager/BuildManager.cs)
- [src/Build/BackEnd/BuildManager/CoordinatorClient.cs](src/Build/BackEnd/BuildManager/CoordinatorClient.cs)
- [src/Framework/Traits.cs](src/Framework/Traits.cs) - Enablement logic

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

---

## Lifecycle and Operation

### Coordinator Startup

1. When first MSBuild process needs coordination, it attempts to start the coordinator
2. Coordinator uses a system-wide mechanism to ensure only one instance runs
3. If an instance already exists, the new process connects as a client instead
4. Coordinator listens on a named pipe for client connections

**Source:** [src/MSBuild.Coordinator/Program.cs](src/MSBuild.Coordinator/Program.cs)

### Heartbeat Monitoring

The coordinator detects stalled or crashed clients through periodic heartbeats:

- Clients send heartbeat messages at configured intervals (default: 5 seconds)
- Coordinator tracks missed heartbeats
- After threshold is reached (default: 3 misses = 15 seconds), client is considered stalled
- Coordinator automatically releases nodes allocated to stalled client
- Waiting builds can then be granted those nodes

**Source:** [src/MSBuild.Coordinator/CoordinatorServer.cs](src/MSBuild.Coordinator/CoordinatorServer.cs)

### Graceful Shutdown

When a build completes normally:

1. Client sends `ReleaseNodesMessage` with its grant ID
2. Coordinator frees those nodes
3. Processes waiting queue to allocate freed nodes to waiting builds
4. If no active or waiting clients remain, coordinator enters timeout mode
5. After 60 seconds of inactivity, coordinator exits

**Source:** [src/MSBuild.Coordinator/CoordinatorServer.cs](src/MSBuild.Coordinator/CoordinatorServer.cs)

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
- [src/Build/BackEnd/BuildManager/CoordinatorClient.cs](src/Build/BackEnd/BuildManager/CoordinatorClient.cs)
- [src/MSBuild.Coordinator/CoordinatorServer.cs](src/MSBuild.Coordinator/CoordinatorServer.cs)

---

## Testing

### Unit Tests

Comprehensive test coverage in [src/MSBuild.Coordinator.UnitTests/](src/MSBuild.Coordinator.UnitTests/):

- Protocol serialization/deserialization
- Node budget manager allocation logic
- Fair-share algorithm correctness
- Heartbeat monitoring
- Multi-build coordination scenarios
- Error conditions and edge cases

---

## Source Code References

For detailed implementation information, refer to:

- **Server Implementation:** `src/MSBuild.Coordinator/`
- **Protocol Definitions:** `src/Framework/Coordinator/`
- **Client Integration:** `src/Build/BackEnd/BuildManager/` 
- **Configuration:** `src/Framework/Traits.cs`, `src/Framework/Coordinator/CoordinatorSettings.cs`
- **Tests:** `src/MSBuild.Coordinator.UnitTests/`
