# Deploying and restoring a local Visual Studio installation

Read this procedure only for an explicitly requested local deployment or restore.
Replacing installed binaries is not part of a debugging-only request.

## When to Use

- You need Visual Studio's build (Ctrl+Shift+B, Build Solution, design-time builds) to use your local MSBuild changes.
- You are debugging an issue that only reproduces when MSBuild is called through the VS process (`devenv.exe`).
- You want to test MSBuild changes against a real VS solution end-to-end.

For command-line testing, use [bootstrap](../../use-bootstrap-msbuild/SKILL.md).
For attachment or logging without replacing binaries, use [debugging](debugging.md).

## Prerequisites

- Windows (the deploy script targets VS on Windows).
- A compatible Visual Studio installation selected by the user.
- Permission for the specific destination; elevate only the authorized copy/restore operation if necessary.
- The MSBuild repo cloned and buildable.
- A rollback snapshot and an inventory of files that the deployment may introduce.

Read [Deploy-MSBuild.ps1](../../../../scripts/Deploy-MSBuild.ps1) at the working
revision before copying. It is not transactional, does not stop processes, and
backs up individual existing files as it copies them, not the entire installation.

## Step 1: Build MSBuild

Reuse matching outputs when they already contain the requested revision and
configuration. If a build is needed, first resolve the repository SDK from
[global.json](../../../../global.json). Build without administrator privileges.
The deployment script needs bootstrap, architecture-specific MSBuild, and task
host outputs, so a repository build may be appropriate for this deployment:

```powershell
.\build.cmd -v quiet
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
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (!(Test-Path -LiteralPath $vswhere)) { throw "vswhere is unavailable." }
$json = & $vswhere -all -prerelease -products '*' -requires Microsoft.Component.MSBuild -format json
if ($LASTEXITCODE -ne 0) { throw "Visual Studio discovery failed." }
$json | ConvertFrom-Json | Select-Object instanceId, displayName, installationPath
```

Choose the requested IDE instance from these results; `-products '*'` can also
include Build Tools installations. Do not select the newest installation by
default. `vswhere -?` documents the installed version's supported formats and
selection options; `table` is not a supported format.

From the selected instance's Developer Command Prompt, this is another useful
cross-check, not proof of the intended installation:

```cmd
where msbuild
```

Resolve `$vsPath` from the selected instance and `$destination` to its
`MSBuild\Current\Bin` directory. Check that it is the intended existing directory.

## Step 3: Deploy with the Script

Before executing:

1. Inspect the script's source-file lists and confirm all required files exist for the selected configuration/runtime. A missing late file otherwise causes a partial deployment.
2. Record the selected instance, exact destination, source revision, configuration, and runtime in the authorized action.
3. Close that VS instance and its relevant build processes before copying. Identify processes by executable path and task ownership; if termination is necessary and authorized, use specific PIDs. Do not kill every `MSBuild.exe` or close unrelated VS instances.
4. Take an independent snapshot outside the destination and record the pre-deployment file inventory. Keep it until restoration is complete.

Only then run the authorized operation, elevating this step if the destination
requires it. The variables below must come from that preflight:

```powershell
.\scripts\Deploy-MSBuild.ps1 -destination $destination -configuration $configuration -runtime Desktop
if (!$?) { throw "Deployment failed; inspect the partial state before continuing." }
```

### Script Parameters

| Parameter | Default | Description |
| --------- | ------- | ----------- |
| `-destination` | *(required)* | The VS MSBuild `Bin` folder path |
| `-configuration` | `Debug` | Must match the configuration used in `build.cmd` |
| `-runtime` | `Detect` | Path-based heuristic; explicitly select `Desktop` for VS. Inspect current script support before selecting `Core`. |
| `-binDirectory` | `artifacts\bin` | Override if build output is elsewhere |
| `-makeBackup` | `$true` | Creates a timestamped `Backup-*` folder before overwriting |

### What Gets Copied

The script copies from `artifacts\bin\MSBuild.Bootstrap\{configuration}\net472\`:

- **Core DLLs**: `Microsoft.Build.dll`, `Microsoft.Build.Framework.dll`, `Microsoft.Build.Tasks.Core.dll`, `Microsoft.Build.Utilities.Core.dll`, `Microsoft.NET.StringTools.dll`
- **Targets files**: `Microsoft.Common.targets`, `Microsoft.CSharp.targets`, `Microsoft.VisualBasic.targets`, and many more
- **Executables**: `MSBuild.exe` (x86 and amd64), `MSBuildTaskHost.exe`
- **Framework dependencies**: Various `System.*` assemblies needed for .NET Framework

## Step 4: Test in Visual Studio

1. Launch the selected VS instance after copying.
2. Open the intended solution and exercise the requested build or design-time scenario.
3. Establish that the intended binaries were loaded, using module paths/version information or a binary log. A successful solution build alone does not prove the deployment was used.

## Restoring the Original MSBuild

The script creates `{destination}\Backup-{timestamp}\` by default, but that folder
contains only overwritten files that already existed. It does not record files
introduced by this deployment and may be incomplete after a copy failure.

For an authorized restore, first stop only the relevant processes and use the
independent snapshot and inventory. This example restores overwritten files from
one explicitly selected script backup; it is **not** a complete rollback:

```powershell
# $backup is the exact backup selected for this deployment, not a wildcard.
Get-ChildItem -LiteralPath $backup | Copy-Item -Destination $destination -Recurse -Force
```

Reconcile introduced files against the recorded inventory; remove only exact
paths proven to belong to this deployment and authorized for removal. If the
original state cannot be established, report that limitation and use the Visual
Studio Installer's Repair operation only with separate authorization.

⚠ **CAUTION**: If you overwrite MSBuild in Visual Studio and something goes wrong, VS itself may break (since VS uses MSBuild internally). Always keep the backup and know how to restore before deploying.

## Deploying to .NET SDK Instead

This is a separate, higher-risk operation, not a fallback for VS debugging.
Prefer an isolated bootstrap environment. If SDK patching is explicitly requested,
inspect the script's current Core target framework and the selected SDK's runtime
requirements, dependency manifests, and layout first. Do not copy a newer-runtime
MSBuild into an older SDK merely because the destination contains `dotnet\sdk`.
Use an isolated copy and an explicit `-runtime Core` only after compatibility and
rollback are established; there is no evergreen SDK-version command to paste.

## Cross-Machine Deployment

If you cannot build MSBuild on the target machine:

1. Build and deploy to an **empty folder**:

   ```powershell
   .\scripts\Deploy-MSBuild.ps1 -destination "C:\temp\msbuild-deploy"
   ```

2. On the target machine, select the compatible VS instance, close its relevant processes, and make the independent snapshot and file inventory first.
3. Copy only the intended staged outputs to the authorized destination, then establish which binaries VS loads.

## Troubleshooting

| Problem | Solution |
| ------- | -------- |
| Access denied when deploying | Run PowerShell as **Administrator** |
| VS won't start after deploy | Restore from `Backup-*` folder or run VS Installer Repair |
| Changes not reflected in VS | Check module paths, the selected instance, and only its relevant process PIDs |
| Configuration mismatch | Ensure `-configuration` matches what you passed to `build.cmd` |
| Build errors in MSBuild repo | Diagnose the first relevant error; preserve the worktree and do not reset it to obtain a "clean state" |

## See Also

- [Deploy-MSBuild documentation](https://github.com/dotnet/msbuild/blob/main/documentation/Deploy-MSBuild.md)
- [Building and Debugging on Full Framework](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md)
- [Bootstrap Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Bootstrap.md)
