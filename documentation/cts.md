# Clever Test Selection (CTS)

> **⚠️ STATUS**: CTS test execution hangs after the first batch of test invocations.
> The repo is configured for CTS on the **Debug** configuration, but actually using
> it end-to-end is blocked by an integration issue between CTS and MSBuild's tests
> running on Microsoft.Testing.Platform v2 + xUnit v3.
>
> Tracked on the `adopt-cts` branch.

[CTS](https://devdiv.visualstudio.com/DevDiv/_git/CodeCoverage) is an internal
test-impact-analysis tool. It records, for each test, the set of source files
the test exercised, and can later run only the tests affected by a given diff.

This repo is wired up for CTS via:

| File | Purpose |
|------|---------|
| `cts.json` | Source/module/filter configuration |
| `eng/cts/collect.ps1` | Builds a baseline by running all Debug tests |
| `eng/cts/apply.ps1` | Runs only tests affected by local changes |

## Prerequisites

Install the `cts` global tool (one-time):

```powershell
dotnet tool install cts --global --prerelease `
    --add-source https://devdiv.pkgs.visualstudio.com/_packaging/VS/nuget/v3/index.json `
    --interactive
```

The feed is internal; you'll need the [Azure Artifacts Credential Provider](https://github.com/microsoft/artifacts-credprovider).

## Usage

Build the repo in Debug first so the test DLLs exist under `artifacts/bin/**/Debug/`:

```powershell
.\build.cmd -v quiet
```

Create a baseline (do this on a clean checkout of `main`):

```powershell
.\eng\cts\collect.ps1 -Tag main
```

Then, after making changes, run only the impacted tests:

```powershell
.\eng\cts\apply.ps1 -Tag main
```

The baseline lives in `artifacts/cts/baseline` and logs in `artifacts/cts/logs`,
both of which are inside the gitignored `artifacts/` tree.

## Test runner setup

CTS requires Microsoft.Testing.Platform v2 (MTP v2). MSBuild test projects use
xUnit v3 through Arcade's `TestRunnerName=XUnitV3`, which by default pulls in the
older MTP v1 adapter (`xunit.v3.mtp-v1`). To work with CTS we override that:

- `_MSBuildMTPPin` in `Directory.Build.props` pinned to **MTP 2.2.1**.
- `_MSBuildXUnitV3Pin` pinned to **3.2.2** (xUnit v3).
- `Microsoft.Testing.Extensions.CodeCoverage` pinned to **18.7.0** (the older
  18.0.6 implements an MTP v1-only interface and crashes under MTP v2 with
  `TypeLoadException: Method 'OnTestSessionStartingAsync' ... does not have an
  implementation.`).
- `src/Directory.Build.targets` adds an explicit `xunit.v3.mtp-v2` PackageReference
  for `.NETCoreApp` test projects and uses a `_RemoveXunitMtpV1References` target
  to drop the conflicting `xunit.v3.mtp-v1.dll` from compile/runtime references.

After these changes, running a test project's `.exe` directly reports
`xUnit.net v3 Microsoft.Testing.Platform v2 Runner` and all tests pass.

## Known Issues

**CTS test execution hangs after the first batch** (as of 2026-05-29,
CTS v2.8.0-alpha.26271.2 with the MTP v2 / CodeCoverage 18.7.0 fixes above):

- Build, discovery, and the first ~9 test invocations succeed.
  The diagnostic logs under `artifacts/cts/logs/<TestProject>/testing/runTests/`
  show each spawned MTP server process running its 1 assigned test and exiting
  cleanly with `Total: 1, Errors: 0, Failed: 0`.
- After roughly 9 runTests invocations, no further processes are spawned and
  CTS sits idle (~3% CPU, no new diagnostic files).
- Reproducible with `--dop 1`, `--dop 32`, and any single test project
  (validated on `StringTools.UnitTests` with the temp narrow filter).
- Earlier we also saw `System.TypeLoadException` from
  `Microsoft.Testing.Extensions.CodeCoverage 18.0.6` against MTP 2.x. That is
  fixed by upgrading the package to 18.7.0; the post-fix hang is a separate
  issue.

### Root cause analysis (process dump, 2026-05-29)

Captured `cts.exe` and the spawned `dotnet.exe` testhost via `dotnet-dump`
during the hang. Findings:

1. **TCP connection is established** between cts (listening 51972) and testhost
   (51973) — both sockets show ESTABLISHED.
2. **Testhost is alive and waiting** at `Xunit.MicrosoftTestingPlatform
   .TestPlatformTestFramework.RunAsync` → `ServerTestHost.HandleMessagesAsync`
   → `TcpMessageHandler.ReadAsync` → `StreamReader.ReadBufferAsync`. It
   read CTS's first bytes (per testhost diag log it reached
   `TestHostBuilder.PlatformExitProcessOnUnhandledException`) and is now
   blocking on `ReadBufferAsync` for the next command.
3. **CTS is awaiting a JsonRpc response** at
   `TestingPlatformClient.RunTestsAsync` → `InternalInvokeAsync` →
   `JsonRpc.InvokeCoreAsync<TResult>`. It has sent a request and is waiting on
   the result Task to complete.
4. **CTS heap has 3 outstanding `<InvokeCoreAsync<InitializeResponseArgs>>`
   async state machines** and **4 live `StreamJsonRpc.JsonRpc` instances**
   even though `--dop 1` was used. Earlier connections appear not to be
   torn down between test invocations, suggesting a resource/state leak.
5. **Two threadpool workers are blocked** in synchronous `Kernel32.ReadFile`
   against pipe handles (`_fileType=3`/`FILE_TYPE_PIPE`, `_fileOptions=0` so
   no overlapped I/O) — these are the child process's redirected stdout/stderr
   handled via `AsyncOverSyncWithIoCancellation`. Two sync pipe reads burn 2
   threadpool workers per live child for the lifetime of the child; not the
   cause of the hang on its own, but compounds it.
6. The `9` is not exact — it's "approximately however many we manage to
   complete before whichever timer/threshold fires".

The mismatch is on the **JSON-RPC application layer**, not the wire framing:
both sides are using their respective framing (CTS uses
`HeaderDelimitedMessageHandler`, MTP v2 testhost uses `TcpMessageHandler`)
correctly for the first 9 invocations. After ~9 runs CTS has accumulated
stale JsonRpc connections / pending `Initialize` calls, and on the next
invocation either (a) it sends a request the testhost cannot complete, or
(b) it forgets to send the request that the testhost is awaiting.

This is almost certainly a CTS-side bug in the MTP v2 connection lifecycle,
not in MSBuild or xUnit v3 configuration. Filed for the CTS team with full
dumps and stacks at:
`artifacts/cts/dumps/{cts-hung.dmp, testhost-hung.dmp, cts-async.txt}`.

Workaround: none identified; needs investigation by the CTS team. See the
standalone repro at <https://github.com/jankratochvilcz/cts-xunit3> for the
simpler MTP-v2 + xUnit v3 sample that successfully runs all 7 tests end-to-end
— that scenario does not exceed ~9 test invocations so does not trip the
leak.

### How to reproduce and capture diagnostics

```powershell
# In one window, start a collect run with full diagnostics:
.\eng\cts\collect.ps1 -Tag hang-debug

# Add temporarily to collect.ps1 for verbose tracing:
#   --dop 1 --diagnostic --platform-diagnostic
#   --platform-diagnostic-verbosity trace
#   --print-console-output --console-output-verbosity verbose

# Once CTS goes idle (~10-20 sec after last "Connecting to client host"),
# in another window:
$ctsPid    = (Get-Process cts).Id
$childPid  = (Get-CimInstance Win32_Process -Filter "ParentProcessId=$ctsPid").ProcessId
dotnet-dump collect -p $ctsPid   -o cts-hung.dmp      --type Full
dotnet-dump collect -p $childPid -o testhost-hung.dmp --type Full
dotnet-dump analyze cts-hung.dmp -c "dumpasync" -c "exit" > cts-async.txt
```

## Test Environment Compatibility

MSBuild tests use `EnvironmentInvariant` to detect environment pollution. CTS compatibility required:
- Ignoring .NET profiler vars (`CORECLR_PROFILER`, `MicrosoftInstrumentationEngine_*`, etc.)
- Ignoring MSBuild CLI vars (`MSBuildLoadMicrosoftTargetsReadOnly`, `MSBUILDLOADALLFILESASWRITEABLE`)

These exemptions are in `src/UnitTests.Shared/TestEnvironment.cs`.

## Notes

- If a modified source file is outside `SourceCodeFiles.Include` or matches
  `SourceCodeFiles.Exclude`, CTS conservatively runs **all** tests in the
  affected test modules.
- `Filter.Include` in `cts.json` targets `artifacts/bin/**/Debug/net10.0/*UnitTests*.dll`.
  Other configurations (Release, net472) are not included.
- The scripts use `cts collect|apply testingplatform` (not `vstest`) because MSBuild
  tests run on Microsoft.Testing.Platform + xUnit v3, not VSTest.
