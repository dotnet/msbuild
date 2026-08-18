# SDK Resolution Algorithm

In 17.3 under ChangeWave 17.4 the sdk resolution algorithm is changed.

## Reason for change

Previously (before ChangeWave 17.4) all SDK resolvers were loaded and then ordered by priority. The resolvers are tried one after one until one of them succeeds. In order to decrease the number of assemblies to be load we change the behavior in 17.3 under ChangeWave 17.4.

## New SDK Resolution Algorithm

Under ChangeWave 17.4 all the resolvers divides into two groups:

- Specific resolvers, i.e. resolvers with specified sdk name pattern `ResolvableSdkPattern`
- General resolvers, i.e. resolvers without specified sdk name pattern `ResolvableSdkPattern`

The resolving algorithm works in two passes.

- On the first pass all the specific resolvers that match the given sdk name would be loaded, ordered by priority and tried one after one.
- If the sdk is not found, on the second pass all general resolvers would be loaded, ordered by priority and tried one after one.

By default the resolvers are general. To make all the resolvers from some dll specific, in the corresponding manifest (xml file) one need to specify the `ResolvableSdkPattern` using C# regex format:

```xml
<SdkResolver>
  <Path>MySdkResolver.dll</Path>
  <ResolvableSdkPattern>MySdk.*</ResolvableSdkPattern>
</SdkResolver>
```

Note, that the manifest file, if exists, from ChangeWave 17.4 would have preference over the dll.
The sdk discovery works according to the following algorithm:

- First try locate the manifest file and use it.
- If it is not found, we try to locate the dll in the resolver's folder.
Both xml and dll name should match the following name pattern `...\SdkResolvers\(ResolverName)\(ResolverName).(xml/dll)`.

## Failed SDK Resolution

> 🚧 Note
>
> This page is a work in progress.

SDK resolvers previously attempted to continue when one critically fails (throws an unhandled exception). This lead to misleading error messages such as:

```text
warning MSB4242: The SDK resolver "Microsoft.DotNet.MSBuildWorkloadSdkResolver" failed to run. 's' is an invalid start of a property name. Expected a '"'. LineNumber: 14 | BytePositionInLine: 8.
error MSB4236: The SDK 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator' specified could not be found. [C:\foo\bar.csproj]
```

`MSB4236` is a red herring while `MSB4242` is the real error despite being logged as a warning. Because of this, SDK resolvers now fail the build _immediately_ upon unhandled exceptions. These exceptions are propogated as `SdkResolverException`s, and `MSB4242` has been promoted to an error code. The new error message appears like so:

```text
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
