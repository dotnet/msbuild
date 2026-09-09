# Debugging MSBuild hosted by Visual Studio

This procedure does not replace installed binaries. Select the VS instance and
launch it from a process with the required environment. Close only the relevant
existing instance if it would otherwise reuse an old environment.

## API-hosted builds

[BuildManager.AttachDebugger](../../../../src/Build/BackEnd/BuildManager/BuildManager.cs)
handles `MSBuildDebugBuildManagerOnStart` before beginning a build:

```powershell
$env:MSBuildDebugBuildManagerOnStart = '1'
$env:MSBuildDebugProcessName = 'devenv'
& (Join-Path $vsPath 'Common7\IDE\devenv.exe')
```

Value `1` launches a debugger where `FEATURE_DEBUG_LAUNCH` is supported. Value `2`
waits for console input to allow manual attachment; it does not detect attachment
and is unsuitable for unattended use. The process-name filter is a substring
match, not an exact executable identity.

## CLI and child processes

`MSBUILDDEBUGONSTART` is handled by the MSBuild executable's
[DebuggerLaunchCheck](../../../../src/MSBuild/XMake.cs), not by an arbitrary
API-hosting process such as `devenv`. Value `1` requests debugger launch; `2` waits
for console input; `3` excludes task-host nodes from debugger launch. These hooks
depend on the build's feature flags. Use the BuildManager hook for VS-hosted API
calls rather than promising the CLI hook will break inside VS.

## Binary logs for VS builds

VS filters events, especially for design-time builds.
[BuildManager.AppendDebuggingLoggers](../../../../src/Build/BackEnd/BuildManager/BuildManager.cs)
adds a binary logger when debug-engine logging and the process filter allow it:

```powershell
$env:MSBuildDebugEngine = '1'
$env:MSBuildDebugProcessName = 'devenv'
$env:MSBUILDDEBUGPATH = $diagnosticDirectory
& (Join-Path $vsPath 'Common7\IDE\devenv.exe')
```

Choose an existing writable `$diagnosticDirectory`.
[FrameworkDebugUtils.SetDebugPath](../../../../src/Framework/DebugUtils.cs) places
debug-engine output under `.MSBuild_Logs` beneath a writable supplied directory,
otherwise beneath the current directory or a temporary directory. Inspect the
actual output path rather than assuming the environment variable is the final
log-file directory.

Binary logs can contain project contents, environment values, paths, and sensitive
task inputs. Keep them local unless sharing is authorized, and inspect/redact
before publishing. These variables affect new child processes; preserve and
restore their previous values in the launching shell after the scenario. Confirm
the loaded modules and log provenance before concluding that local changes ran.
