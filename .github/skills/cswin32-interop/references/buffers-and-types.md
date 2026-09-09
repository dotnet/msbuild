# Native buffers and types

## Reuse the generated domain

Use the current native API's type rather than scattering casts through callers:

| Domain | Existing type families to inspect |
|---|---|
| Handles/modules | `HANDLE`, `HMODULE`, `SafeFileHandle` at managed ownership boundaries |
| HRESULTs and Win32 errors | `HRESULT`, `WIN32_ERROR`, BOOL according to the API |
| File operations | `FILE_FLAGS_AND_ATTRIBUTES`, `FILE_ACCESS_RIGHTS`, `FILE_SHARE_MODE`, `FILE_CREATION_DISPOSITION` |
| Process operations | `PROCESS_CREATION_FLAGS`, `STARTUPINFOW_FLAGS`, `PROCESS_ACCESS_RIGHTS` |
| Memory mapping | `PAGE_PROTECTION_FLAGS`, `FILE_MAP` |
| Restart Manager | `RM_APP_STATUS`, `RM_APP_TYPE` |
| Shell folders | `KNOWN_FOLDER_FLAG` |

Add an appropriate API/type/constant name to [NativeMethods.txt](../../../../src/Framework/NativeMethods.txt) when generation is needed. Do not add a local mirror enum before checking the pinned metadata.

Check numeric value, width, and semantics when replacing constants. In particular, a generic access mask and a file-specific access mask are not interchangeable merely because both names contain `READ`.

Some constants are not enum members. The existing type-library pattern combines `REGKIND.REGKIND_NONE` with `PInvoke.LOAD_TLB_AS_32BIT` or its counterpart at the constant's width:

```csharp
(REGKIND)((uint)REGKIND.REGKIND_NONE | PInvoke.LOAD_TLB_AS_32BIT)
```

Do not invent `REGKIND` members for those load flags. Verify the current generated declaration when adding a similar combination.

For an API returning `WIN32_ERROR`, keep that type through comparisons/helpers and cast only at a boundary such as a `Win32Exception` constructor. HRESULT success/failure is a different protocol.

Enum operations can also have runtime-specific cost. Where `HasFlag` is not optimized it can box; consider bitwise checks for a measured hot path, preserving the intended zero/composite-mask semantics rather than replacing every call mechanically.

## Handles and storage

Common conversion shapes include `(HANDLE)intPtr`, `(IntPtr)handle.Value`, and constructing a `SafeFileHandle` with an explicit ownership decision. The cast itself does not transfer lifetime or keep a `SafeHandle` alive.

When passing `DangerousGetHandle()` to a raw API, preserve the surrounding lifetime/pinning/add-ref strategy. Do not let an unrelated disposal or finalization close the handle during the native call.

Use named sentinels such as `HANDLE.Null` or `HANDLE.INVALID_HANDLE_VALUE` only when they match the API's failure contract; not every handle-returning function uses the same sentinel.

Anonymous union/member paths are generated details. Inspect the actual declaration rather than guessing how many `.Anonymous` segments exist.

## FILETIME

[FileTimeExtensions](../../../../src/Framework/Windows/Win32/Foundation/FileTimeExtensions.cs) extends `System.Runtime.InteropServices.ComTypes.FILETIME`:

| Helper | Result |
|---|---|
| `ToLong()` | The combined 64-bit count of 100-nanosecond intervals |
| `ToDateTime()` | Local `DateTime`, using `DateTime.FromFileTime` |
| `ToDateTimeUtc()` | UTC `DateTime`, using `DateTime.FromFileTimeUtc` |

The helper handles signed high/low fields correctly. Reuse it instead of repeating shifts and casts.

Choose local versus UTC for the *consumer's* contract. Restart Manager's `RM_UNIQUE_PROCESS.ProcessStartTime` comes from process creation time, not an already-local FILETIME. Its conversion to local time can be appropriate when comparing with a managed local `Process.StartTime`.

Sources: [RM_UNIQUE_PROCESS](https://learn.microsoft.com/en-us/windows/win32/api/restartmanager/ns-restartmanager-rm_unique_process) and [GetProcessTimes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocesstimes).

Inspect the member's actual generated type before selecting an extension. Do not assume this helper also extends a distinct Foundation `FILETIME` wrapper.

## BufferScope

[BufferScope<T>](../../../../src/Framework/Utilities/BufferScope.cs) can start with stack storage and rent from `ArrayPool<T>` when more capacity is needed:

```csharp
using BufferScope<char> buffer = new(stackalloc char[256]);
```

The size here is illustrative, not a required native buffer length. Choose a bounded stack allocation based on element size, call frequency, recursion/stack depth, and the API's expected data. Use pooled storage for larger or variable requirements.

Important contracts:

- `EnsureCapacity(capacity)` does not preserve contents by default; request `copy: true` only when the caller needs them.
- `Length` is capacity, not the count of valid output elements.
- `AsSpan()` and `Slice()` expose storage owned by the scope; do not retain them beyond disposal.
- Disposal returns rented arrays and clears reference-containing arrays as implemented by the helper.
- Avoid copying an owning scope, which can create conflicting disposal/lifetime assumptions.

Before writing `fixed` blocks, inspect existing CsWin32 convenience overloads. A string/span overload may already do the required pinning; a raw pointer must remain within the relevant storage lifetime.

## Native sizing protocols

Do not apply one resize recipe to every Windows API. Read whether the size is bytes or elements, whether it includes a terminator, how insufficient space is reported, and whether a result can grow between calls.

The `GetShortPathName` use in [NativeMethods.cs](../../../../src/Framework/NativeMethods.cs) is an existing `BufferScope<char>` example. Its returned length and retry path are specific to that API.

For a changed sizing loop, account for success, failure, insufficient capacity, a changed required size, and the maximum representable/allocation size. Slice only after establishing the valid output length. Preserve the original error/fallback contract instead of silently presenting a partial buffer as success.
