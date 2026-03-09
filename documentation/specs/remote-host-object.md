# Support for remote host object

A remote host object must be registered in the [Running Object Table (ROT)](https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nn-objidl-irunningobjecttable) before calling `RegisterHostObject(string projectFile, string targetName, string taskName, string monikerName)`. In the out-of-process node, MSBuild will call [`IRunningObjectTable::GetObject`](https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nf-objidl-irunningobjecttable-getobject) to get the host object by the monikerName that was registered via `RegisterHostObject`.

[The registration of interfaces](https://docs.microsoft.com/en-us/dotnet/framework/interop/how-to-register-primary-interop-assemblies) is the only thing interop with COM that need extra care. There are 3 interfaces involved in out-of-proc tasks work: `IVsMSBuildTaskFileManager`, `IPersistFileCheckSum` and `ITaskHost`. `IVsMSBuildTaskFileManager` and `IPersistFileCheckSum` are registered globally in Windows registry by VS existing setup. `ITaskHost` is also configured in VS using registration-free. So the only work is to configure it using registration-free in **MSBuild**. That results the change in msbuild.exe.manifest file and the change to generate tlb file for ITaskHost.

## Annotated additions to the msbuild.exe.manifest file

```xml
<file name="Microsoft.Build.Framework.tlb"> -- Location of the tlb, it should be in the same directory as msbuild.exe
    <typelib
        tlbid="{D8A9BA71-4724-481D-9CA7-0DA23A1D615C}" -- matches what is embedded in the tlb with ITaskHost
        version="15.1" -- matches the version in tlb
        helpdir=""/>
</file>

<comInterfaceExternalProxyStub
    iid="{9049A481-D0E9-414f-8F92-D4F67A0359A6}" -- iid of type ITaskHost for COM
    name="ITaskHost" -- does not have to match
    tlbid="{D8A9BA71-4724-481D-9CA7-0DA23A1D615C}" -- tlb id, so it can link to previous session
    proxyStubClsid32="{00020424-0000-0000-C000-000000000046}" /> -- universal marshaler built in Windows
```

## Shipping `Microsoft.Build.Framework.tlb` with the .NET SDK

Starting with .NET SDK 10.0.3xx, `Microsoft.Build.Framework.tlb` is shipped alongside the MSBuild apphost in the SDK layout on Windows (see [dotnet/msbuild#13175](https://github.com/dotnet/msbuild/pull/13175)).

### Why it is needed

MSBuild now supports running as a native apphost executable (`MSBuild.exe`) instead of being launched via `dotnet MSBuild.dll`. When MSBuild runs as an apphost, it spawns out-of-process worker nodes and task hosts that are also native executables. These child processes communicate with the parent build process and, on Windows, may need to marshal COM interfaces — specifically `ITaskHost` — across process boundaries.

The Windows universal marshaler (`{00020424-0000-0000-C000-000000000046}`) requires type library information to know how to serialize interface method calls between processes. Without the `.tlb` file present next to the MSBuild apphost, COM marshaling of `ITaskHost` fails, breaking any scenario that relies on remote host objects (e.g., Visual Studio passing unsaved file buffers to out-of-proc build tasks).

Previously, only the Visual Studio installation of MSBuild (`msbuild.exe` under VS) shipped this `.tlb`. The .NET SDK's `dotnet MSBuild.dll` entry point did not need it because it did not use activation contexts and the COM registration was handled by VS. With the apphost model, the SDK-shipped MSBuild must be self-contained for registration-free COM, requiring the `.tlb` to be present in the SDK layout directory.

### What capabilities this adds

With the `.tlb` included in the SDK, the MSBuild apphost gains full support for:

- **Remote host objects via the Running Object Table (ROT)** — IDE hosts (such as Visual Studio) can register host objects that out-of-proc MSBuild worker nodes retrieve through `IRunningObjectTable::GetObject`. This is the mechanism that allows build tasks running in separate processes to access unsaved file changes from the IDE.
- **Registration-free COM for `ITaskHost`** — the apphost's manifest (`MSBuild.exe.manifest`) references the `.tlb` to enable COM proxy/stub generation without requiring any machine-wide registry entries. This makes the SDK's MSBuild fully portable and xcopy-deployable.
- **Parity with Visual Studio's MSBuild** — the SDK-shipped MSBuild apphost now has the same COM interop capabilities as the VS-shipped `msbuild.exe`, ensuring consistent behavior regardless of how MSBuild tasks are executed.

### SDK layout

The `.tlb` is placed in the SDK root directory alongside `MSBuild.dll` and `MSBuild.exe`:

```
sdk/<version>/
├── MSBuild.dll
├── MSBuild.exe              ← apphost (Windows)
├── MSBuild.exe.manifest
├── Microsoft.Build.Framework.tlb
└── ...
```

The SDK build produces the apphost via the `CreateMSBuildAppHost` target in `GenerateLayout.targets`, which also copies the `.tlb` to the output directory (Windows only).

## Practical example: using `IDispatch` to communicate with a COM host object

When MSBuild runs out-of-process (e.g., as an apphost), the build task receives the host object as an `ITaskHost` COM proxy. Calling strongly-typed .NET interfaces on a COM proxy requires matching type libraries for every interface involved — which is impractical when the host object lives in a different process (e.g., Visual Studio) with its own private types.

The solution is **late-bound invocation via `IDispatch`**: the task calls a well-known method by name and exchanges data as a simple string, avoiding the need to marshal complex .NET types across the COM boundary.

### Host side (Visual Studio — [WebTools PR 701336](https://dev.azure.com/devdiv/DevDiv/_git/WebTools/pullrequest/701336))

The VS host object implements a `QueryAllTaskItems` method that serializes its data to JSON:

```csharp
// Simplified from the WebTools host object implementation.
// The host object is registered in the ROT before the build starts.
public class VSMsDeployTaskHostObject : ITaskHost
{
    private readonly List<ITaskItem> _taskItems = new();

    // IDispatch-callable method — returns a JSON string so that
    // only primitive types cross the COM boundary.
    public string QueryAllTaskItems()
    {
        var dtos = _taskItems.Select(item => new
        {
            item.ItemSpec,
            Metadata = new Dictionary<string, string>
            {
                ["UserName"] = item.GetMetadata("UserName"),
            }
        });

        return JsonSerializer.Serialize(dtos);
    }
}
```

### Task side (SDK Container task — [dotnet/sdk#52856](https://github.com/dotnet/sdk/pull/52856))

The out-of-proc MSBuild task retrieves the host object from the ROT and calls `QueryAllTaskItems` via `IDispatch` (reflection's `InvokeMember` dispatches through `IDispatch` when the target is a COM proxy):

```csharp
internal sealed class VSHostObject(ITaskHost? hostObject, TaskLoggingHelper log)
{
    /// <summary>
    /// Calls QueryAllTaskItems on the host object via IDispatch.
    /// InvokeMember uses IDispatch::Invoke under the hood when the
    /// target is a COM proxy, so no shared type library is needed
    /// beyond ITaskHost itself.
    /// </summary>
    private IEnumerable<ITaskItem>? GetTaskItems()
    {
        try
        {
            // Late-bound call — works across COM process boundaries
            // because only a string is marshaled back.
            string? json = (string?)hostObject!
                .GetType()
                .InvokeMember(
                    "QueryAllTaskItems",
                    BindingFlags.InvokeMethod,
                    binder: null,
                    target: hostObject,
                    args: null);

            if (!string.IsNullOrEmpty(json))
            {
                // Deserialize the JSON into TaskItem instances
                // on the task side.
                var dtos = JsonSerializer
                    .Deserialize<List<TaskItemDto>>(json);
                return dtos?.Select(ConvertToTaskItem).ToList();
            }
        }
        catch (Exception ex)
        {
            log.LogMessage(MessageImportance.Low,
                "IDispatch call failed: {0}", ex.Message);
        }

        // Fallback: in-proc path where the host object is a
        // real .NET object, not a COM proxy.
        if (hostObject is IEnumerable<ITaskItem> items)
            return items;

        return null;
    }
}
```

### Why this pattern works

| Concern | How it is addressed |
|---|---|
| **No shared type libraries** | The only COM interface that crosses the boundary is `ITaskHost`, whose `.tlb` is already shipped. The `QueryAllTaskItems` method is called via `IDispatch`, so no additional `.tlb` is needed for the host object's concrete type. |
| **Simple marshaling** | The method returns a `string` (JSON). Strings are `BSTR` in COM and are universally marshalable by the built-in OLE Automation marshaler. |
| **Backward compatibility** | If the host object does not support `QueryAllTaskItems` (older VS versions), the reflection call throws and the code falls back to casting `ITaskHost` to `IEnumerable<ITaskItem>` — the legacy in-proc path. |
| **Security** | Credentials extracted from the JSON are held in environment variables only for the duration of the task execution and cleared in a `finally` block. |

## Bigger context

It is part of the work for [allowing out-of-proc tasks to access unsaved changes](https://github.com/dotnet/project-system/issues/4406) and [MSBuild apphost support](https://github.com/dotnet/msbuild/issues/12995).
