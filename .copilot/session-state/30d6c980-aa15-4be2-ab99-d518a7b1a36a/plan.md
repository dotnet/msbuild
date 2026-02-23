# AR-May's Cross-Version Compatibility — Option B Implementation Plan

## Problem
New .NET task host could send callback packets to old MSBuild node that doesn't understand them.

## Solution: Option B with Traits.cs
- `PacketVersion` stays at `2` (safe for shipping)
- Callbacks gated by: `parentPacketVersion >= CallbacksMinVersion(3) || Traits.Instance.EnableTaskHostCallbacks`
- `Traits.cs` reads `MSBUILDENABLETASKHOSTCALLBACKS` env var (follows existing pattern)
- Tests set env var → `Traits` picks it up (auto-refreshed per test via `BuildEnvironmentState.s_runningTests`)
- When all stages are done: bump PacketVersion to 3, remove `Traits` escape hatch

## Why Traits.cs
- **Established pattern**: Same approach as `EnableRarNode`, `ForceAllTasksOutOfProcToTaskHost`, etc.
- **Test-friendly**: `Traits.Instance` re-creates per test when `s_runningTests` is set, so env var changes are picked up automatically
- **Shared across assemblies**: `Traits` is in `Microsoft.Build.Framework`, accessible from both `MSBuild.exe` and `Microsoft.Build.dll`
- **Discoverable**: All MSBuild feature toggles live in one place

## Implementation Details

### 1. Traits.cs
**File**: `src/Framework/Traits.cs`

Add to `Traits` class (next to `EnableRarNode`):
```csharp
/// <summary>
/// Enable IBuildEngine callbacks in the TaskHost process.
/// Temporary escape hatch until all callback stages are complete and PacketVersion is bumped.
/// </summary>
public readonly bool EnableTaskHostCallbacks = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS"));
```

### 2. OutOfProcTaskHostNode changes
**File**: `src/MSBuild/OutOfProcTaskHostNode.cs`

Add field + store in `Run()`:
```csharp
private byte _parentPacketVersion;
private const byte CallbacksMinPacketVersion = 3;
```

Add helper:
```csharp
private bool CallbacksSupported =>
    _parentPacketVersion >= CallbacksMinPacketVersion
    || Traits.Instance.EnableTaskHostCallbacks;
```

Guard `IsRunningMultipleNodes`:
```csharp
if (!CallbacksSupported) return false;
```

### 3. Test changes
**File**: `src/Build.UnitTests/BackEnd/TaskHostCallback_Tests.cs`

Existing integration tests: Add `env.SetEnvironmentVariable("MSBUILDENABLETASKHOSTCALLBACKS", "1")`

New test: `IsRunningMultipleNodes_ReturnsFalseWhenCallbacksNotSupported` — no env var, verify `false` default

### 4. Safe defaults when callbacks disabled
| Callback | Default | Rationale |
|----------|---------|-----------|
| `IsRunningMultipleNodes` | `false` | Same as pre-callback behavior |
| `RequestCores` (future) | `1` | Single core |
| `ReleaseCores` (future) | no-op | Nothing to release |
| `BuildProjectFile` (future) | throw | Task depends on result |

## Workplan

- [ ] Add `EnableTaskHostCallbacks` to `Traits.cs`
- [ ] Add `_parentPacketVersion` field and `CallbacksMinPacketVersion` const
- [ ] Add `CallbacksSupported` property
- [ ] Guard `IsRunningMultipleNodes` with `CallbacksSupported` check
- [ ] Update existing integration tests to set env var
- [ ] Add new test for graceful fallback (no env var → returns false)
- [ ] Restore `StringArrayWithNullsDoesNotCrashTaskHost` test (done locally)
- [ ] Commit and push
- [ ] Reply to AR-May's review comment explaining the approach


