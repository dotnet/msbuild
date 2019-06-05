# Support for remote host object

A remote host object must be registered in the [Running Object Table (ROT)](https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nn-objidl-irunningobjecttable) before calling `RegisterHostObject(string projectFile, string targetName, string taskName, string monikerName)`. In the out-of-process node, MSBuild will call [`IRunningObjectTable::GetObject`](https://docs.microsoft.com/en-us/windows/desktop/api/objidl/nf-objidl-irunningobjecttable-getobject) to get the host object by the monikerName that was registered via `RegisterHostObject`.

[The registration of interfaces](https://docs.microsoft.com/en-us/dotnet/framework/interop/how-to-register-primary-interop-assemblies) is the only thing interop with COM that need extra care. There are 3 interfaces involved in out-of-proc tasks work: `IVsMSBuildTaskFileManager`, `IPersistFileCheckSum` and `ITaskHost`. `IVsMSBuildTaskFileManager` and `IPersistFileCheckSum` are registered globally in Windows registry by VS existing setup. `ITaskHost` is also configured in VS using registration-free. So the only work is to configure it using registration-free in **MSBuild**. That results the change in msbuild.exe.manifest file and the change to generate tlb file for ITaskHost.

## Annotated additions to the msbuild.exe.manifest file.
```
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

## Bigger context

If is part of the work for [allowing out-of-proc tasks to access unsaved changes](https://github.com/dotnet/project-system/issues/4406)

## More reference:

[RegFree COM Walkthrough](https://msdn.microsoft.com/library/ms973913.aspx)

[RegFree COM with .NET Framework](https://docs.microsoft.com/dotnet/framework/interop/configure-net-framework-based-com-components-for-reg)
