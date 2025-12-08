# MSBuild App Host Support

## Purpose

Enable MSBuild to be invoked directly as a native executable (`MSBuild.exe` / `MSBuild`) instead of through `dotnet MSBuild.dll`, providing:

- Better process identification (processes show as "MSBuild" not "dotnet")
- Win32 manifest embedding support (**COM interop**)
- Consistency with Roslyn compilers (`csc`, `vbc`) which already use app hosts
- Simplified invocation model

### Important consideration
The .NET SDK currently invokes MSBuild in two modes:

| Mode | Current Behavior | After App Host |
|------|------------------|----------------|
| **In-proc** | SDK loads `MSBuild.dll` directly | No change |
| **Out-of-proc** | SDK launches `dotnet exec MSBuild.dll` | SDK will launch `MSBuild.exe` |

The AppHost introduction does not break SDK integration since we are not modifying the in-proc flow. The SDK will continue to load `MSBuild.dll` directly for in-proc scenarios.

**SDK out-of-proc consideration**: The SDK can be configured to run MSBuild out-of-proc today via `DOTNET_CLI_RUN_MSBUILD_OUTOFPROC`, and this pattern will likely become more common as AOT work progresses for CLI commands that wrap MSBuild invocations. When the SDK does launch MSBuild out-of-proc, it will use the new app host (`MSBuild.exe`) when available.

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

### 2. Installer Repository (dotnet/dotnet VMR)
The app host creation happens in the installer/layout targets, similar to how Roslyn app hosts are created (PR https://github.com/dotnet/dotnet/pull/3180).

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

Before launching an app host process, set `DOTNET_ROOT` in the `ProcessStartInfo.Environment`:
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

### Edge Cases

| Issue | Solution |
|-------|----------|
| `DOTNET_HOST_PATH` not set | Fail with clear error. This should always be set by the SDK; if missing, it indicates an unexpected/unsupported environment. |
| Architecture-specific vars override `DOTNET_ROOT` | Clear `DOTNET_ROOT_X64`, `DOTNET_ROOT_X86`, `DOTNET_ROOT_ARM64` in `ProcessStartInfo.Environment` (see code above) |
| App host doesn't exist | Fall back to `dotnet MSBuild.dll` and **log a message** indicating fallback (e.g., for debugging older SDK scenarios) |