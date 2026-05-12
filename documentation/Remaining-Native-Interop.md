# Remaining Native Interop (`[ComImport]` / `[DllImport]`)

This document inventories the Windows-specific native interop in this repository
that has **not** yet been migrated to [CsWin32](https://github.com/microsoft/CsWin32).
It is intended as a roadmap for future cleanup work and as a record of which
declarations are intentionally kept hand-written.

See [`.github/skills/cswin32-interop/SKILL.md`](../.github/skills/cswin32-interop/SKILL.md)
and [`.github/skills/cswin32-com/SKILL.md`](../.github/skills/cswin32-com/SKILL.md)
for the migration playbooks.

## Status overview

| Area | File | Kind | Reason still hand-written |
| --- | --- | --- | --- |
| TLB / metadata COM | [src/Tasks/NativeMethods.cs](../src/Tasks/NativeMethods.cs) | `[ComImport]` x6, `[DllImport("oleaut32")]` | TypeLib registration + .NET Framework metadata APIs not yet covered. |
| TypeLib COM helpers | [src/Tasks/Interop.cs](../src/Tasks/Interop.cs), [src/Tasks/IFixedTypeInfo.cs](../src/Tasks/IFixedTypeInfo.cs) | `[ComImport]` x4 | Custom `ITypeLib2` / `ITypeInfo` shapes; CsWin32 metadata lacks the bespoke `ref Guid` / array marshaling. |
| Manifest signing | [src/Tasks/ManifestUtil/NativeMethods.cs](../src/Tasks/ManifestUtil/NativeMethods.cs), [src/Tasks/ManifestUtil/mansign2.cs](../src/Tasks/ManifestUtil/mansign2.cs), [src/Tasks/ManifestUtil/MetadataReader.cs](../src/Tasks/ManifestUtil/MetadataReader.cs) | `[DllImport]` ~25, `[ComImport]` x2 | `mscorwks.dll` strong-name APIs + `crypt32.dll` `Cert*Context` shapes. `mscorwks` is .NET Framework only and not in Win32 metadata. |
| Strong-name helpers | [src/Tasks/Utilities/StrongNameHelpers.cs](../src/Tasks/Utilities/StrongNameHelpers.cs) | `[ComImport]` x2 | `IClrStrongName` / `IMetaDataDispenser` — same `mscorwks` concern. |
| FileTracker host load | [src/Shared/InprocTrackingNativeMethods.cs](../src/Shared/InprocTrackingNativeMethods.cs) | `[DllImport("kernel32")]` x2 | Uses `SafeLibraryHandle` (CsWin32 here is configured `useSafeHandles: false`). |
| Directory enumeration | [src/Framework/FileSystem/WindowsNative.cs](../src/Framework/FileSystem/WindowsNative.cs) | `[DllImport]` x4 (`FindFirstFileW`/`FindNextFileW`/`FindClose`/`PathMatchSpecExW`) | Uses `SafeFindFileHandle` + custom `Win32FindData`. Migration requires switching away from `SafeHandle`. |
| UnGAC tool | [src/Package/Microsoft.Build.UnGAC/NativeMethods.cs](../src/Package/Microsoft.Build.UnGAC/NativeMethods.cs) | `[ComImport]` `IAssemblyCache` + `[DllImport("fusion.dll")]` | Tiny standalone net472 tool; not worth wiring CsWin32 into. |
| Task host (net35) | [src/MSBuildTaskHost/Utilities/NativeMethods.cs](../src/MSBuildTaskHost/Utilities/NativeMethods.cs), [src/MSBuildTaskHost/CommunicationsUtilities.cs](../src/MSBuildTaskHost/CommunicationsUtilities.cs) | `[DllImport("kernel32")]` x6 | Targets `net35` — CsWin32 source generator is unavailable. Must stay hand-written. |
| VS setup configuration | [src/Framework/VisualStudioLocationHelper.cs](../src/Framework/VisualStudioLocationHelper.cs) (line 104) | `[DllImport("Microsoft.VisualStudio.Setup.Configuration.Native.dll")]` | Non-Windows API; ships with VS. Not in Win32 metadata. |
| Test-only back-end node helpers | [src/Build/BackEnd/Node/NativeMethods.cs](../src/Build/BackEnd/Node/NativeMethods.cs) | `[DllImport("kernel32")]` `CreateProcess` (+ structs) | Only consumed by `FEATURE_FILE_TRACKER` tests. Low priority. |
| Tests (driver / fixtures) | [src/UnitTests.Shared/DriveMapping.cs](../src/UnitTests.Shared/DriveMapping.cs), [src/Tasks.UnitTests/AddToWin32Manifest_Tests.cs](../src/Tasks.UnitTests/AddToWin32Manifest_Tests.cs), [src/Utilities.UnitTests/TrackedDependencies/FileTrackerTests.cs](../src/Utilities.UnitTests/TrackedDependencies/FileTrackerTests.cs), [src/Build.UnitTests/BackEnd/TargetUpToDateChecker_Tests.cs](../src/Build.UnitTests/BackEnd/TargetUpToDateChecker_Tests.cs) | `[DllImport]` (`DefineDosDevice`, `QueryDosDevice`, `LoadLibrary`, `FindResource`, `CreateFile`, `GetVolumeInformation`, ...) | Test-only. CsWin32 access is technically available via `InternalsVisibleTo`; pure cleanup task. |

## Detail by area

### 1. COM TypeLib / CLR metadata (`src/Tasks/NativeMethods.cs`)

Ten hand-written `[ComImport]` interfaces back the COM Reference / typelib-registration
task chain (`ResolveComReference`, `RegisterAssembly`, `UnregisterAssembly`,
`GenerateResource`, `GenerateBootstrapper`):

| Interface | IID / role |
| --- | --- |
| `ICreateTypeLib` | TypeLib authoring (`CreateTypeLib2`). |
| `IMetaDataDispenser` | CLR metadata dispenser (`mscoree`). |
| `IMetaDataImport` / `IMetaDataImport2` | CLR metadata import (huge surface; partially declared with `void`-returning stubs to skip slots). |
| `IMetaDataAssemblyImport` | Assembly-level metadata import. |
| `IAssemblyCache` / `IAssemblyName` / `IAssemblyEnum` | Fusion GAC API (`fusion.dll`). **Migrated** — see `src/Tasks/AssemblyDependency/Fusion/`. |

Associated `[DllImport]`s:

* `oleaut32!RegisterTypeLib`, `UnRegisterTypeLib`, `LoadTypeLibEx`, `LoadRegTypeLib`, `QueryPathOfRegTypeLib`

**Migration notes**

* `oleaut32` typelib APIs are present in Win32 metadata (`Windows.Win32.System.Ole`).
  Adding `RegisterTypeLib`, `UnRegisterTypeLib`, `LoadTypeLibEx`, `LoadRegTypeLib`,
  `QueryPathOfRegTypeLib` to [`src/Framework/NativeMethods.txt`](../src/Framework/NativeMethods.txt)
  would replace the five hand-written declarations.
* `IClassFactory` already exists in CsWin32 — remove the local copy and the
  `[ComImport]` declaration in `NativeMethods.cs`.
* `IMetaData*` / `IAssembly*` are **not** in Win32 metadata (they belong to the
  CLR and Fusion). These must remain hand-written or be vendored from
  CsWin32-compatible metadata.

### 2. TypeLib helpers (`src/Tasks/Interop.cs`, `src/Tasks/IFixedTypeInfo.cs`)

Custom shapes of `ITypeLib2`, `ITypeInfo2`, `ICreateTypeInfo`, `ICreateTypeLib2`
plus a `IFixedTypeInfo` wrapper that re-declares `ITypeInfo` to fix the standard
runtime marshaler's mistakes with `GetRefTypeOfImplType`. The hand-rolled shapes
are required for correctness — even if CsWin32 surfaces these interfaces, the
existing marshaling fix-ups have to be preserved.

### 3. ClickOnce / strong-name signing (`src/Tasks/ManifestUtil/*`)

`mansign2.cs` calls `mscorwks.dll` (`StrongName*` legacy APIs), `crypt32.dll`
(`CertEnumCertificatesInStore`, `CertGetCertificateContextProperty`,
`CryptAcquireContext`), and `kernel32!FormatMessage` / `LocalFree`.

* `mscorwks` is **.NET Framework specific** and not in Win32 metadata; these
  cannot be migrated to CsWin32.
* `crypt32` Cert/Crypt APIs are in Win32 metadata, but the existing code uses
  `CRYPTOAPI_BLOB` / `IntPtr` patterns. Migration is feasible but requires
  reworking buffer ownership. Tracked as low-priority cleanup.
* `kernel32!FormatMessage` could trivially be added to `NativeMethods.txt`.

`MetadataReader.cs` declares `IMetaDataDispenser` / `IMetaDataImport2` via
`[ComImport]` — same `mscorwks` constraint as above.

### 4. Strong-name helpers (`src/Tasks/Utilities/StrongNameHelpers.cs`)

`[ComImport] IClrStrongName` and a CLR-private `IMetaDataDispenser` variant.
Same constraint: CLR-only, not in Win32 metadata.

### 5. FileTracker bootstrapping (`src/Shared/InprocTrackingNativeMethods.cs`)

`LoadLibrary` + `GetProcAddress` to bind to `FileTracker.dll`. The reason this
is still hand-written is that it uses `SafeLibraryHandle` (a `SafeHandle`
subclass), while the repo's CsWin32 config has `useSafeHandles: false`.

Migration would replace `SafeLibraryHandle` with `HMODULE` and a `try/finally`
or `IDisposable` wrapper. Function-pointer plumbing (`GetProcAddress` -> typed
delegate) is unchanged.

### 6. Windows directory enumeration (`src/Framework/FileSystem/WindowsNative.cs`)

`FindFirstFileW` / `FindNextFileW` / `FindClose` / `PathMatchSpecExW`.

The hand-written declarations use `[MarshalAs(UnmanagedType.ByValTStr)]` for
the file-name fields of `WIN32_FIND_DATA` and return a `SafeFindFileHandle`.
With CsWin32's `allowMarshaling: false`, the generated `WIN32_FIND_DATAW`
uses fixed-size `char` arrays — a workable migration path, but the
`SafeHandle`-based enumerator wrapper would need to be rewritten.

### 7. UnGAC standalone tool (`src/Package/Microsoft.Build.UnGAC`)

Tiny `IAssemblyCache` + `fusion!CreateAssemblyCache` to remove MSBuild's old
GAC entries on install. Single-purpose net472 tool. Not worth wiring CsWin32 in
to.

### 8. Task host (`src/MSBuildTaskHost`)

Targets `net35`. CsWin32 source generation does not run on net35, so these
hand-written declarations (`GetEnvironmentStrings`, `SetEnvironmentVariable`,
`SetCurrentDirectory`, ...) **must stay**.

### 9. VS setup configuration (`src/Framework/VisualStudioLocationHelper.cs`)

`GetSetupConfiguration` lives in `Microsoft.VisualStudio.Setup.Configuration.Native.dll`
(VS-installer redistributable), not in the Win32 metadata. Hand-written
declaration is the only option.

### 10. Back-end node CreateProcess wrapper (`src/Build/BackEnd/Node/NativeMethods.cs`)

`CreateProcess` + `STARTUP_INFO` + `SECURITY_ATTRIBUTES` + `PROCESS_INFORMATION`
duplicates types already in CsWin32. The only consumer is the
`FEATURE_FILE_TRACKER` unit-test fixture
(`Utilities.UnitTests/TrackedDependencies/FileTrackerTests.cs`). When that
test is updated to call `Windows.Win32.PInvoke.CreateProcess`, this whole file
can be deleted.

### 11. Test fixtures

Tests in `UnitTests.Shared`, `Tasks.UnitTests`, `Utilities.UnitTests`, and
`Build.UnitTests` use small hand-written `[DllImport]`s for fixture setup
(drive mapping, resource extraction, hard-link creation, etc.). All have
`InternalsVisibleTo` to `Microsoft.Build.Framework`, so they can adopt CsWin32
incrementally. Pure-cleanup work; no behavior changes expected.

## Completed in this branch

* `kernel32!SetEnvironmentVariable` (Framework) -> `PInvoke.SetEnvironmentVariable`.
* `REGDB_E_CLASSNOTREG`, `TYPE_E_REGISTRYACCESS`, `TYPE_E_CANTLOADLIBRARY` magic
  numbers -> `HRESULT.*` typed constants (added to `NativeMethods.txt`).
* `ERROR_ACCESS_DENIED`, `ERROR_INVALID_FILENAME`, `ERROR_SHARING_VIOLATION`
  hard-coded HRESULTs in `Tasks/Copy.cs` and
  `Tasks/BootstrapperUtil/ResourceUpdater.cs` -> `(int)(HRESULT)WIN32_ERROR.*`.
* Dead constants and `crypt32`/`advapi32` declarations removed from
  `Tasks/NativeMethods.cs` (`NullPtr`, `ERROR_SUCCESS`,
  `HRESULT_E_CLASSNOTREGISTERED`, `CRYPTOAPI_BLOB`, `PFXImportCertStore`
  block, dead local `IClassFactory` `[ComImport]`).
* **Fusion (GAC) `[ComImport]` interfaces** — `IAssemblyCache`, `IAssemblyName`,
  `IAssemblyEnum` converted to struct-based COM with `delegate* unmanaged[Stdcall]`
  vtables, following the WMI pattern in `src/Framework/Utilities/Wmi/`. The four
  `fusion.dll` `[DllImport]`s (`CreateAssemblyCache`, `CreateAssemblyEnum`,
  `CreateAssemblyNameObject`, `GetCachePath`) and the `ASSEMBLY_INFO`,
  `AssemblyCacheFlags`, `CreateAssemblyNameObjectFlags`,
  `AssemblyNameDisplayFlags`, `ASSEMBLYINFO_FLAG` types moved to
  `src/Tasks/AssemblyDependency/Fusion/`. Declarations sourced from
  `CLR\src\inc\fusion.idl`.

## Recommended next steps (in priority order)

1. **`oleaut32` typelib APIs** — add five names to `NativeMethods.txt`, delete
   the five `[DllImport]` lines in `Tasks/NativeMethods.cs`. Lowest risk, biggest
   visible reduction.
2. **`kernel32!FormatMessage`** in `ManifestUtil/mansign2.cs` — single-line swap.
3. **`Build/BackEnd/Node/NativeMethods.cs`** — replace test-only `CreateProcess`
   wrapper with `PInvoke.CreateProcess`, then delete the file.
4. **Test fixtures** (`DriveMapping`, `AddToWin32Manifest_Tests`,
   `TargetUpToDateChecker_Tests`) — straight rewrites against CsWin32.
5. **`WindowsNative.cs` `FindFirstFile*`** — larger; requires reworking
   `SafeFindFileHandle`.
6. **`InprocTrackingNativeMethods.cs`** — same `SafeHandle` consideration.

Items not on this list (CLR metadata, `mscorwks`, VS setup native, net35 task
host, UnGAC tool) are intentionally left hand-written for the reasons described
above.
