> 🚧 Note
>
> This page is a work in progress.

### Failed SDK Resolution
SDK resolvers previously attempted to continue when one critically fails (throws an unhandled exception). This lead to misleading error messages such as:

```
warning MSB4242: The SDK resolver "Microsoft.DotNet.MSBuildWorkloadSdkResolver" failed to run. 's' is an invalid start of a property name. Expected a '"'. LineNumber: 14 | BytePositionInLine: 8.
error MSB4236: The SDK 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator' specified could not be found. [C:\foo\bar.csproj]
```

`MSB4236` is a red herring while `MSB4242` is the real error despite being logged as a warning. Because of this, SDK resolvers now fail the build _immediately_ upon unhandled exceptions. These exceptions are propogated as `SdkResolverException`s, and `MSB4242` has been promoted to an error code. The new error message appears like so:

```
C:\src\temp\8-18>"C:\foo\dotnet-sdk-6.0.100-preview.7.21379.14-win-x64\dotnet.exe" build    
Microsoft (R) Build Engine version 17.0.0-dev-21420-01+5df152759 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

C:\foo\bar.csproj : error MSB4242: SDK Resolver Failure: "The SDK resolver "Microsoft.DotNet.MSBuildWorkloadSdkResolver" failed while attempting to resolve the SDK "Microsoft.NET.Sdk". Exception: "'s' is an invalid start of a property name. Expected a '"'. LineNumber: 14 | BytePositionInLine: 8."".

Build FAILED.

C:\foo\bar.csproj : error MSB4242: SDK Resolver Failure: "The SDK resolver "Microsoft.DotNet.MSBuildWorkloadSdkResolver" failed while attempting to resolve the SDK "Microsoft.NET.Sdk". Exception: "'s' is an invalid start of a property name. Expected a '"'. LineNumber: 14 | BytePositionInLine: 8."".
    0 Warning(s)
    1 Error(s)

Time Elapsed 00:00:00.15
```