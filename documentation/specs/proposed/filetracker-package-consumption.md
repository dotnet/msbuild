# FileTracker Package Consumption

## Status

**Deferred** - Per maintainer comment on [Issue #649](https://github.com/dotnet/msbuild/issues/649): "This doesn't seem that important at the moment."

## Background

The source code for `Tracker.exe` and `FileTracker.dll` (`FileTracker32.dll`, `FileTracker64.dll`, `FileTrackerA4.dll`) is not open source, but those assemblies are part of the MSBuild binary distribution. Currently:

1. **Bootstrap process** copies FileTracker binaries from the installed Visual Studio (`$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\`)
2. **Tests** fall back to installed versions, making testing coordinated changes difficult
3. **Runtime loading** via `InprocTrackingNativeMethods.cs` dynamically loads the appropriate FileTracker DLL based on architecture

## Current Implementation

### Where FileTracker Binaries Are Sourced

In `eng/BootStrapMsBuild.targets`:

```xml
<ItemGroup>
  <InstalledVersionedExtensions Include="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\**\Tracker*.dll" />
  <InstalledVersionedExtensions Include="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\**\Tracker*.exe" />
  <InstalledVersionedExtensions Include="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\**\FileTracker*.dll" />
</ItemGroup>
```

### How FileTracker Is Loaded at Runtime

In `src/Shared/InprocTrackingNativeMethods.cs`:

```csharp
private static readonly Lazy<string> fileTrackerDllName = new Lazy<string>(() => 
    RuntimeInformation.ProcessArchitecture == Architecture.Arm64 
        ? "FileTrackerA4.dll" 
        : (IntPtr.Size == sizeof(Int32)) 
            ? "FileTracker32.dll" 
            : "FileTracker64.dll");
```

The path is resolved via `FrameworkLocationHelper.GeneratePathToBuildToolsForToolsVersion()`.

## Proposed Solution

### Phase 1: Internal Package Creation (Microsoft Internal)

Microsoft would need to:

1. Create a NuGet package (e.g., `Microsoft.Build.FileTracker`) containing:
   - `Tracker.exe`
   - `FileTracker32.dll`
   - `FileTracker64.dll`
   - `FileTrackerA4.dll`
   - Appropriate `.targets` file for content file deployment

2. Publish the package to a feed accessible by MSBuild builds (internal or public)

### Phase 2: MSBuild Repository Changes

If/when such a package exists, the following changes would be needed:

#### 1. Add Package Reference

In `eng/Versions.props`:
```xml
<MicrosoftBuildFileTrackerVersion>1.0.0</MicrosoftBuildFileTrackerVersion>
```

In `Directory.Packages.props`:
```xml
<PackageVersion Include="Microsoft.Build.FileTracker" Version="$(MicrosoftBuildFileTrackerVersion)" />
```

#### 2. Update Bootstrap Process

In `eng/BootStrapMsBuild.targets`, replace VS-sourced FileTracker binaries with package content:

```xml
<PropertyGroup>
  <FileTrackerPackagePath>$(NuGetPackageRoot)microsoft.build.filetracker\$(MicrosoftBuildFileTrackerVersion)\</FileTrackerPackagePath>
</PropertyGroup>

<ItemGroup>
  <!-- Use package instead of VS installation -->
  <FileTrackerBinaries Include="$(FileTrackerPackagePath)tools\**\*.*" />
</ItemGroup>
```

#### 3. Update Test Infrastructure

Ensure tests can locate FileTracker from the package location or bootstrap folder rather than requiring VS installation.

#### 4. Consider Conditional Fallback

For backwards compatibility, consider fallback to VS-installed versions if the package is not available:

```xml
<PropertyGroup>
  <UseFileTrackerPackage Condition="'$(UseFileTrackerPackage)' == ''">true</UseFileTrackerPackage>
</PropertyGroup>
```

## Why This Was Deferred

1. **Closed-source binaries**: The FileTracker binaries are not open source and cannot be built from this repository
2. **Internal dependencies**: Requires coordination with Microsoft internal build systems
3. **Limited impact**: Most developers use VS-installed MSBuild where FileTracker is already available
4. **Complexity**: Would require changes to packaging, bootstrap, and test infrastructure

## Related Issues

- [#649](https://github.com/dotnet/msbuild/issues/649) - FileTracker should be consumed from a package
- [#10714](https://github.com/dotnet/msbuild/issues/10714) - Tracker.exe and FileTracker32.dll source discussion
- [#12063](https://github.com/dotnet/msbuild/issues/12063) - FileTrackerTests dependency on discontinued Csc task

## References

- [FileTracker class documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.build.utilities.filetracker)
- [Microsoft Detours](https://github.com/microsoft/Detours) - The underlying technology (now MIT licensed)
