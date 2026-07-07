---
name: deploy-msbuild-to-vs
description: Deploy locally-built MSBuild binaries into a Visual Studio installation for testing and debugging. Use when you need VS itself to use your local MSBuild changes, or when debugging MSBuild as invoked by VS.
argument-hint: Deploy local MSBuild to VS, patch VS with built MSBuild, debug MSBuild inside VS.
---

# Deploying Built MSBuild into Visual Studio

This skill guides you through replacing Visual Studio's bundled MSBuild with your locally-built binaries, so that builds initiated from within VS use your changes. It also covers debugging MSBuild when invoked by VS and restoring the original state.

## When to Use

- You need Visual Studio's build (Ctrl+Shift+B, Build Solution, design-time builds) to use your local MSBuild changes.
- You are debugging an issue that only reproduces when MSBuild is called through the VS process (`devenv.exe`).
- You want to test MSBuild changes against a real VS solution end-to-end.

**If you only need command-line testing**, prefer the bootstrap approach instead (see the `use-bootstrap-msbuild` skill). It is faster and does not risk breaking your VS installation.

## Prerequisites

- Windows (the deploy script targets VS on Windows).
- Visual Studio installed (2022 or later, including 2026 and preview versions).
- An **administrator** PowerShell prompt (required to write into `Program Files`).
- The MSBuild repo cloned and buildable.

## Step 1: Build MSBuild

Run a full build from the repo root to produce the bootstrap output:

```powershell
.\build.cmd
```

By default this builds `Debug` configuration. If you want `Release`:

```powershell
.\build.cmd /p:Configuration=Release
```

The build creates bootstrap binaries under `artifacts\bin\MSBuild.Bootstrap\{configuration}\`.

## Step 2: Locate Your VS MSBuild Bin Folder

Find the MSBuild `Bin` folder inside your VS installation. The path depends on your VS version and edition.

**⚠ Important**: If you have multiple VS versions or editions installed side-by-side, make sure you identify the correct installation path for the instance you want to patch. Confirm the path exists before deploying.

The general pattern is:

```text
C:\Program Files\Microsoft Visual Studio\{version}\{edition}\MSBuild\Current\Bin
```

Where `{version}` is the year (e.g. `2022`, `2026`) or a numeric version for previews (e.g. `18`), and `{edition}` is `Enterprise`, `Professional`, `Community`, or `Preview`.

Example paths:

| VS Version | Edition | Typical Path |
| ---------- | ------- | ------------ |
| 2026 | Enterprise | `C:\Program Files\Microsoft Visual Studio\2026\Enterprise\MSBuild\Current\Bin` |
| 2022 | Community | `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin` |
| Preview (v18) | Preview | `C:\Program Files\Microsoft Visual Studio\18\Preview\MSBuild\Current\Bin` |

### Finding the path automatically

The most reliable way to locate your VS installation is with `vswhere`:

```powershell
# List all VS installations with their paths
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -all -format table

# Get the MSBuild Bin path for the latest installation
$vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
"$vsPath\MSBuild\Current\Bin"
```

Alternatively, from a VS Developer Command Prompt:

```cmd
where msbuild
```

Use the directory containing that `MSBuild.exe`.

## Step 3: Deploy with the Script

From an **administrator** PowerShell, run:

```powershell
.\scripts\Deploy-MSBuild.ps1 -destination "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin"
```

### Script Parameters

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `-destination` | *(required)* | The VS MSBuild `Bin` folder path |
| `-configuration` | `Debug` | Must match the configuration used in `build.cmd` |
| `-runtime` | `Detect` | Auto-detects `Desktop` vs `Core` from the path. Use `Desktop` for VS, `Core` for .NET SDK |
| `-binDirectory` | `artifacts\bin` | Override if build output is elsewhere |
| `-makeBackup` | `$true` | Creates a timestamped `Backup-*` folder before overwriting |

### What Gets Copied

The script copies from `artifacts\bin\MSBuild.Bootstrap\{configuration}\net472\`:

- **Core DLLs**: `Microsoft.Build.dll`, `Microsoft.Build.Framework.dll`, `Microsoft.Build.Tasks.Core.dll`, `Microsoft.Build.Utilities.Core.dll`, `Microsoft.NET.StringTools.dll`
- **Targets files**: `Microsoft.Common.targets`, `Microsoft.CSharp.targets`, `Microsoft.VisualBasic.targets`, and many more
- **Executables**: `MSBuild.exe` (x86 and amd64), `MSBuildTaskHost.exe`
- **Framework dependencies**: Various `System.*` assemblies needed for .NET Framework

## Step 4: Test in Visual Studio

1. **Close and reopen** Visual Studio (it caches MSBuild on startup).
2. Open a solution and build — VS now uses your locally-built MSBuild.
3. Verify by checking the build output or binary log for your changes.

## Restoring the Original MSBuild

The deploy script creates a backup folder at `{destination}\Backup-{timestamp}\` by default.

To restore:

```powershell
# Find the backup folder
Get-ChildItem "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Backup-*"

# Copy everything back
Copy-Item "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\Backup-{timestamp}\*" `
          "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\" -Recurse -Force
```

Alternatively, run **Visual Studio Installer → Modify → Repair** to fully restore the original binaries.

⚠ **CAUTION**: If you overwrite MSBuild in Visual Studio and something goes wrong, VS itself may break (since VS uses MSBuild internally). Always keep the backup and know how to restore before deploying.

## Debugging MSBuild Inside Visual Studio

When MSBuild is invoked by VS (for real builds or design-time builds), you can attach a debugger using environment variables. Set these **before** launching VS:

### Breakpoint at BuildManager.BeginBuild (Recommended for VS)

```powershell
$env:MSBuildDebugBuildManagerOnStart = "1"   # 1 = Debugger.Launch(), 2 = wait for attach
$env:MSBuildDebugProcessName = "devenv"      # Only trigger in the VS process
devenv.exe
```

This breaks into `BuildManager.BeginBuild`, which is where VS calls into MSBuild's API.

### Breakpoint at MSBuild Entry Point

```powershell
$env:MSBUILDDEBUGONSTART = "1"   # 1 = Launch debugger, 2 = Wait for attach, 3 = Main process only
devenv.exe
```

### Capturing Full Binary Logs from VS

VS heavily filters MSBuild events and suppresses them entirely during design-time builds. To capture everything:

```powershell
$env:MSBuildDebugEngine = "1"
$env:MSBUILDDEBUGPATH = "C:\temp\MSBuild_Logs"   # Optional: override log output directory
$env:MSBuildDebugProcessName = "devenv"            # Optional: only log from VS
devenv.exe
```

This injects a binary log at `BuildManager.BeginBuild`, capturing full logs for both real and design-time builds. Logs are saved to `MSBuild_Logs\` under the current directory (or `MSBUILDDEBUGPATH` if set).

## Deploying to .NET SDK Instead

To patch a .NET SDK installation rather than VS:

```powershell
.\scripts\Deploy-MSBuild.ps1 -destination "C:\Program Files\dotnet\sdk\10.0.100" -runtime Core
```

The script auto-detects SDK paths (containing `dotnet` and `sdk`), but you can force it with `-runtime Core`.

## Cross-Machine Deployment

If you cannot build MSBuild on the target machine:

1. Build and deploy to an **empty folder**:

   ```powershell
   .\scripts\Deploy-MSBuild.ps1 -destination "C:\temp\msbuild-deploy"
   ```

2. Copy the contents of that folder to the target machine's VS MSBuild Bin folder.
3. Make a manual backup of the target machine's files first.

## Troubleshooting

| Problem | Solution |
| ------- | -------- |
| Access denied when deploying | Run PowerShell as **Administrator** |
| VS won't start after deploy | Restore from `Backup-*` folder or run VS Installer Repair |
| Changes not reflected in VS | Close all VS instances and reopen; kill lingering `MSBuild.exe` processes |
| Configuration mismatch | Ensure `-configuration` matches what you passed to `build.cmd` |
| Build errors in MSBuild repo | Run `.\build.cmd` from a clean state; see [Something's wrong in my build](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Something's-wrong-in-my-build.md) |

## Quick Reference

| Step | Command |
| ---- | ------- |
| Build MSBuild | `.\build.cmd` |
| Deploy to VS (admin) | `.\scripts\Deploy-MSBuild.ps1 -destination "{VS MSBuild Bin path}"` |
| Deploy Release build | `.\scripts\Deploy-MSBuild.ps1 -destination "{path}" -configuration Release` |
| Debug MSBuild in VS | Set `$env:MSBuildDebugBuildManagerOnStart = "1"` then launch `devenv.exe` |
| Capture VS binlogs | Set `$env:MSBuildDebugEngine = "1"` then launch `devenv.exe` |
| Restore original | Copy back from `Backup-*` folder or run VS Installer Repair |

## See Also

- [Deploy-MSBuild documentation](https://github.com/dotnet/msbuild/blob/main/documentation/Deploy-MSBuild.md)
- [Building and Debugging on Full Framework](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md)
- [Bootstrap Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Bootstrap.md)
