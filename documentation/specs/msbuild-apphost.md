# MSBuild App Host Support

## Purpose

Enable MSBuild to be invoked directly as a native executable (`MSBuild.exe` / `MSBuild`) instead of through `dotnet MSBuild.dll`, providing:

- Better process identification (processes show as "MSBuild" not "dotnet")
- Win32 manifest embedding support (**COM interop**)
- Consistency with Roslyn compilers (`csc`, `vbc`) which already use app hosts
- Simplified invocation model

### Important consideration
The .NET SDK currently invokes MSBuild in multiple ways:

| Mode | Current Behavior | After App Host |
|------|------------------|----------------|
| **In-proc** | SDK loads `MSBuild.dll` directly | No change |
| **Out-of-proc (exec)** | SDK launches `dotnet exec MSBuild.dll` | SDK *can* launch `MSBuild.exe` |
| **Out-of-proc (direct)** | SDK launches `dotnet MSBuild.dll` (no `exec`) | SDK *can* launch `MSBuild.exe` |

The AppHost introduction does not break SDK integration since we are not modifying the in-proc flow. The SDK will continue to load `MSBuild.dll` directly for in-proc scenarios.

**SDK out-of-proc consideration**: The SDK can be configured to run MSBuild out-of-proc today via `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC`, and this pattern will likely become more common as AOT work progresses for CLI commands that wrap MSBuild invocations. When the SDK does launch MSBuild out-of-proc, it *can* opt to use the new app host (`MSBuild.exe`) when available, but this is **not required**—the existing `dotnet MSBuild.dll` invocation pattern continues to work. Switching to the app host in the SDK is a simplification/cleanup opportunity enabled by this work, not a forced change.

### Critical: COM Manifest for Out-of-Proc Host Objects

A key driver for this work is enabling **registration-free COM** for out-of-proc task host objects. Currently, when running via `dotnet.exe`, we cannot embed the required manifest declarations - and even if we could, it would be the wrong level of abstraction for `dotnet.exe` to contain MSBuild-specific COM interface definitions.

**Background**: Remote host objects (e.g., for accessing unsaved file changes from VS) must be registered in the [Running Object Table (ROT)](https://docs.microsoft.com/windows/desktop/api/objidl/nn-objidl-irunningobjecttable). The `ITaskHost` interface requires registration-free COM configuration in the MSBuild executable manifest.

**Required manifest additions for `MSBuild.exe.manifest`:**

```xml
<!-- Location of the tlb, must be in same directory as MSBuild.exe -->
<file name="Microsoft.Build.Framework.tlb">
    <typelib
        tlbid="{D8A9BA71-4724-481D-9CA7-0DA23A1D615C}"
        version="15.1"
        helpdir=""/>
</file>

<!-- Registration-free COM for ITaskHost -->
<comInterfaceExternalProxyStub
    iid="{9049A481-D0E9-414f-8F92-D4F67A0359A6}"
    name="ITaskHost"
    tlbid="{D8A9BA71-4724-481D-9CA7-0DA23A1D615C}"
    proxyStubClsid32="{00020424-0000-0000-C000-000000000046}" />
```

**Related interfaces:**
- `ITaskHost` - **must be configured via MSBuild's manifest** (registration-free)
This is part of the work for [allowing out-of-proc tasks to access unsaved changes](https://github.com/dotnet/project-system/issues/4406).

## Background

An **app host** is a small native executable that:
1. Finds the .NET runtime
2. Loads the CLR
3. Calls the managed entry point (e.g., `MSBuild.dll`)

It is functionally equivalent to `dotnet.exe MSBuild.dll`, but as a standalone executable.

**Note**: The app host does NOT include .NET CLI functionality. (e.g. `MSBuild.exe nuget add` wouldn't work — those are CLI features, not app host features).

### Reference Implementation

Roslyn added app host support in [PR #80026](https://github.com/dotnet/roslyn/pull/80026).

## Changes Required

### 1. MSBuild Repository

**Remove `UseAppHost=false` from `src/MSBuild/MSBuild.csproj`:**

```xml
<!-- REMOVE THIS LINE -->
<UseAppHost>false</UseAppHost>
```

The SDK will then produce both `MSBuild.dll` and `MSBuild.exe` (Windows) / `MSBuild` (Unix).

### 2. VMR Changes (dotnet/dotnet - SDK component)
The app host creation happens in the SDK layout targets within the VMR. Changes are made in the `sdk` component of `dotnet/dotnet` to simplify integration and avoid coordinated arcade SDK changes. Similar to how Roslyn app hosts are created (PR https://github.com/dotnet/dotnet/pull/3180).

### 3. Node Launching Logic

Update node provider to launch `MSBuild.exe` instead of `dotnet MSBuild.dll`:
The path resolution logic remains the same, since MSBuild.exe will be shipped in every SDK version.

### 4. Backward Compatibility (Critical)

Because VS supports older SDKs, node launching must handle both scenarios:

```csharp
var appHostPath = Path.Combine(sdkPath, $"MSBuild{RuntimeHostInfo.ExeExtension}");

if (File.Exists(appHostPath))
{
    // New: Use app host directly
    return (appHostPath, arguments);
}
else
{
    // Fallback: Use dotnet (older SDKs)
    return (dotnetPath, $"\"{msbuildDllPath}\" {arguments}");
}
```

**Handshake consideration**: The packet version can be bumped to negotiate between old/new node launching during handshake.
MSBuild knows how to handle it starting from https://github.com/dotnet/msbuild/pull/12753

## Runtime Discovery (the problem is solved in Roslyn app host this way)

### The Problem

App hosts find the runtime by checking (in order):
1. `DOTNET_ROOT_X64` / `DOTNET_ROOT_X86` / `DOTNET_ROOT_ARM64`
2. `DOTNET_ROOT`
3. Well-known locations (`C:\Program Files\dotnet`, etc.)

When running under the SDK, the runtime may be in a non-standard location. The SDK sets `DOTNET_HOST_PATH` to indicate which `dotnet` it's using.

### Solution

Before launching an app host process, set `DOTNET_ROOT` in the `ProcessStartInfo.Environment`.

**Note**: This solution applies to MSBuild's internal node launching (worker nodes, task hosts). The SDK's entry-point invocation (`dotnet MSBuild.dll`, `dotnet exec MSBuild.dll`, or eventually `MSBuild.exe`) is a separate concern—if the SDK continues using `dotnet.exe MSBuild.dll` with `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC`, that path still works and doesn't require `DOTNET_ROOT` handling (since `dotnet.exe` handles runtime discovery itself).
```csharp
// Derive DOTNET_ROOT from DOTNET_HOST_PATH
var dotnetHostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

if (string.IsNullOrEmpty(dotnetHostPath))
{
    // DOTNET_HOST_PATH should always be set when running under the SDK.
    // If not set, fail fast rather than guessing - this indicates an unexpected environment.
    throw new InvalidOperationException("DOTNET_HOST_PATH is not set. Cannot determine runtime location.");
}

var dotnetRoot = Path.GetDirectoryName(dotnetHostPath);

var startInfo = new ProcessStartInfo(appHostPath, arguments);

// Set DOTNET_ROOT for the app host to find the runtime
startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;

// Clear architecture-specific overrides that would take precedence over DOTNET_ROOT
startInfo.Environment.Remove("DOTNET_ROOT_X64");
startInfo.Environment.Remove("DOTNET_ROOT_X86");
startInfo.Environment.Remove("DOTNET_ROOT_ARM64");
```

**Note**: Using `ProcessStartInfo.Environment` is thread-safe and scoped to the child process only, avoiding any need for locking or save/restore patterns on the parent process environment.

### DOTNET_ROOT Propagation to Child Processes

**Concern**: When MSBuild sets `DOTNET_ROOT` to launch a worker node, that environment variable propagates to any tools the worker node executes. This could change tool behavior if the tool relies on `DOTNET_ROOT` to find its runtime.

**Solution**: The worker node (and out-of-proc task host nodes) should explicitly clear `DOTNET_ROOT` (and architecture-specific variants) after startup, restoring the original entry-point environment.

**Applies to**:
- Worker nodes (`OutOfProcNode`)
- Out-of-proc task host nodes (`OutOfProcTaskHostNode`)

```csharp
// In OutOfProcNode.HandleNodeConfiguration (and similar location in OutOfProcTaskHostNode),
// after setting BuildProcessEnvironment:

// Clear DOTNET_ROOT variants that were set only for app host bootstrap.
// These should not leak to tools executed by this worker node.
// Only clear if NOT present in the original build process environment.
string[] dotnetRootVars = ["DOTNET_ROOT", "DOTNET_ROOT_X64", "DOTNET_ROOT_X86", "DOTNET_ROOT_ARM64"];
foreach (string varName in dotnetRootVars)
{
    if (!_buildParameters.BuildProcessEnvironment.ContainsKey(varName))
    {
        Environment.SetEnvironmentVariable(varName, null);
    }
}
```

**Why this works**:

1. `BuildProcessEnvironment` captures the environment from the **entry-point process** (e.g. VS).
2. If the entry-point had `DOTNET_ROOT` set, the worker should also have it (passed via `BuildProcessEnvironment`).
3. If the entry-point did NOT have `DOTNET_ROOT`, it was only added for app host bootstrap and should be cleared.

**Alternative considered**: We could modify `NodeLauncher` to not inherit the parent environment and explicitly pass only `BuildProcessEnvironment` + `DOTNET_ROOT`. However, this is a larger change and may break other scenarios where environment inheritance is expected.

**Implementation note**: Add a comment in the node-launching code explaining why `DOTNET_ROOT` is set and that the worker will clear it:

```csharp
// Set DOTNET_ROOT for app host bootstrap only.
// The worker node will clear this after startup if it wasn't in the original BuildProcessEnvironment.
// See OutOfProcNode.HandleNodeConfiguration.
startInfo.Environment["DOTNET_ROOT"] = dotnetRoot;
```

### Edge Cases

| Issue | Solution |
|-------|----------|
| `DOTNET_HOST_PATH` not set | Fail with clear error. This should always be set by the SDK; if missing, it indicates an unexpected/unsupported environment. |
| Architecture-specific vars override `DOTNET_ROOT` | Clear `DOTNET_ROOT_X64`, `DOTNET_ROOT_X86`, `DOTNET_ROOT_ARM64` in `ProcessStartInfo.Environment` (see code above) |
| App host doesn't exist | Fall back to `dotnet MSBuild.dll` and **log a message** indicating fallback (e.g., for debugging older SDK scenarios) |