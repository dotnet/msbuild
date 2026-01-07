# Enable Binary Log Collection via Environment Variable

## Purpose

Enable binary logging in CI/CD pipelines without modifying artifacts on disk.

**Proposed solution:** An environment variable that enables diagnostic logging without touching any files on disk-no response file creation, no project file modifications, no build script changes.

**Important for company-wide deployment:** When enabling this feature organization-wide (e.g., via CI/CD pipeline configuration), the team setting the environment variable may not be the team that owns individual codebases. Ensure stakeholders understand that builds with `/warnaserror` may be affected and be ready to mitigate this.

### Demoting Warnings to Messages

For scenarios where warnings would break builds (e.g., `/warnaserror` is enabled), set:

```bash
set MSBUILD_LOGGING_ARGS_LEVEL=message
```

| Value | Behavior |
|-------|----------|
| `warning` (default) | Issues logged as warnings; may fail `/warnaserror` builds |
| `message` | Issues logged as low-importance messages; never fails builds |

**Problem scenarios addressed:**

- `-noAutoResponse` blocks response files entirely
- Creating `Directory.Build.rsp` requires writing new files to the source tree
- Modifying existing RSP files risks merge conflicts or unintended side effects
- Some build environments restrict write access to source directories

### Why Not MSBUILDDEBUGENGINE?

The existing `MSBUILDDEBUGENGINE=1` + `MSBUILDDEBUGPATH` mechanism works but has limitations for the desired CI/CD scenarios:

- **Excessive logging:** Captures *all* MSBuild invocations including design-time builds, generating many files
- **No filename control:** Auto-generates filenames; cannot specify output path with `{}` placeholder for unique names
- **Debug overhead:** Enables additional debugging infrastructure beyond just binary logging

## Supported Arguments

- `-bl` / `/bl` / `-binarylogger` / `/binarylogger` (with optional parameters)
- `-check` / `/check` (with optional parameters)

> **Note:** The `deferred` mode for `-check` is not currently supported. Enabling this feature requires changes to the MSBuild codebase. See section "Build Check (-check) Handling" below.

> **Recommendation:** For CI/CD use, specify an **absolute path** with the `{}` placeholder (e.g., `-bl:C:\BuildLogs\build{}.binlog` or `-bl:/var/log/builds/build{}.binlog`) to generate unique filenames in a known location, avoiding CWD-relative paths that vary by build.

**All other switches are blocked** to maintain diagnosability.

### Rationale

Environment variables that unexpectedly affect build behavior are notoriously difficult to diagnose (e.g., `Platform` is a known source of build issues). By restricting this environment variable to logging/diagnostic switches only, we ensure it cannot accidentally change build outcomes-only what gets recorded about the build.

## Argument Processing Order

1. **MSBuild.rsp** (next to MSBuild.exe) - skipped if `-noAutoResponse` present
2. **Directory.Build.rsp** (next to project) - skipped if `-noAutoResponse` present
3. **MSBUILD_LOGGING_ARGS** - always processed, regardless of `-noAutoResponse`
4. **Command-line arguments**

### Why Precedence Doesn't Matter Here

Since `MSBUILD_LOGGING_ARGS` only allows logging switches (`-bl` and `-check`), traditional precedence concerns don't apply:

- **`-bl` is additive:** Each `-bl` argument creates a separate binlog file (requires [#12706](https://github.com/dotnet/msbuild/pull/12706)). Multiple sources specifying `-bl` simply result in multiple binlog files-there's no conflict to resolve.

## Implementation Flow

1. `MSBuildApp.Execute()` called
2. Check for `-noAutoResponse` in command line
3. Process response files (if no `-noAutoResponse`)
4. Read `MSBUILD_LOGGING_ARGS` environment variable
5. Validate and filter arguments
6. Prepend valid arguments to command line
7. Parse combined command line (merging happens here)
8. Execute build

## Scope and Limitations

### Supported Entry Points

This environment variable only affects builds that go through MSBuild's `Main()` entry point:

| Entry Point | Supported | Notes |
|-------------|-----------|-------|
| `MSBuild.exe` | ✅ Yes | |
| `dotnet build` | ✅ Yes | |
| `dotnet msbuild` | ✅ Yes | |
| Visual Studio (IDE builds) | ❌ No | Uses MSBuild API directly |
| `devenv.exe /build` | ❌ No | Uses MSBuild API directly |
| MSBuildWorkspace (Roslyn) | ❌ No | Uses MSBuild API directly |
| Custom build drivers via API | ❌ No | Any direct `Microsoft.Build` API usage |

### API-Driven Builds

For builds that use the MSBuild API directly (including Visual Studio and `devenv.exe /build`), this environment variable has no effect.

**Alternative:** Use `MSBUILDDEBUGENGINE` to inject binlog collection into API-driven builds. This existing mechanism is already used for debugging Visual Studio builds and works across all MSBuild entry points.
```bash
# For API-driven builds (VS, devenv.exe /build, etc.)
set MSBUILDDEBUGENGINE=1

# For command-line builds (MSBuild.exe, dotnet build)
set MSBUILD_LOGGING_ARGS=-bl:build{}.binlog
```

## Warning Messages

Issues are logged as **warnings** by default. Note that users with `/warnaserror` enabled will see these as errors-by opting into this environment variable, users also opt into these diagnostics.

### Messages

- **Informational:** "Using arguments from MSBUILD_LOGGING_ARGS environment variable: {0}" - build continues with arguments applied
- **Unsupported argument:** "MSBUILD_LOGGING_ARGS: Ignoring unsupported argument '{0}'. Only -bl and -check arguments are allowed." - the specific invalid argument is skipped, other valid arguments in the same env var are still processed (e.g., `-bl:a.binlog -maxcpucount:4` → `-bl:a.binlog` is applied, `-maxcpucount:4` is ignored with warning)
- **Malformed input:** "Error processing MSBUILD_LOGGING_ARGS environment variable: {0}" - the entire environment variable is skipped to avoid partial/unpredictable behavior, build proceeds as if the env var was not set

## Build Check (-check) Handling

### Deferred Analysis Mode

`-check:deferred` enables binlog replay analysis with reduced build-time overhead:

- **During build:** Flag recorded in binlog along with additional data needed for checks; BuildCheck NOT activated
- **During replay:** Binlog reader activates BuildCheck for analysis

**Rationale:** BuildCheck analysis can be expensive and checks can fail the build. The environment variable is for diagnostics that can be analyzed later, allowing teams to record data with minimal impact to the build itself.

### Example Workflow
```bash
# 1. Configure environment
set MSBUILD_LOGGING_ARGS=-bl:build{}.binlog -check:deferred

# 2. Run build (reduced overhead, no BuildCheck analysis during build)
msbuild solution.sln

# 3. Later: Replay binlog (BuildCheck analyzes recorded events)
msbuild build{}.binlog
```

## CI/CD Integration


### Environment Variable

- Set `MSBUILD_LOGGING_ARGS=-bl:build{}.binlog`
- No file creation needed
- The `{}` placeholder generates unique filenames for each build invocation

### Combining Both Approaches
```bash
# Environment provides base logging
set MSBUILD_LOGGING_ARGS=-bl:base{}.binlog -check:deferred

# Command line adds specific logging
msbuild solution.sln -bl:detailed.binlog

# Result: Two binlog files created (base{...}.binlog + detailed.binlog)
```