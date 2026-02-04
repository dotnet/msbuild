---
name: use-bootstrap-msbuild
description: Guide for testing bug reproductions against locally-built MSBuild. Use this when you have a repro project and want to verify a fix works before submitting a PR.
---

# Testing Bug Reproductions with Bootstrap MSBuild

This skill guides you through testing a bug reproduction project against your locally-built MSBuild to verify a fix.

## Overview

After making changes to MSBuild, you need to test them against a repro project. The "bootstrap" is a self-contained MSBuild installation built from your local changes. It includes all dependencies needed to build real projects.

## Step 1: Build MSBuild with Bootstrap

Build MSBuild to create the bootstrap directory:

```powershell
# Windows
.\build.cmd

# Unix/macOS
./build.sh
```

This creates the bootstrap at `artifacts\bin\bootstrap\` with your changes.

Rerun this after making any code change. If run in the default mode, there may be some errors on subsequent builds about locked files (due to MSBuild worker node processes lingering). If so, run `./artifacts/bin/bootstrap/core/dotnet build-server shutdown`.

## Step 2: Run Your Repro Project

### .NET Core / .NET SDK Projects

Use the bootstrap `dotnet` CLI directly (preferred):

```powershell
# Windows
artifacts\bin\bootstrap\core\dotnet.exe build <path-to-repro.csproj>
```

```bash
# Unix/macOS
./artifacts/bin/bootstrap/core/dotnet build <path-to-repro.csproj>
```

All of the usual command line arguments should work, including `-bl` to create binlogs.

### .NET Framework Projects

If the problem is specific to the .NET Framework `MSBuild.exe` that is used in Visual Studio, and you're running on Windows, you can use the bootstrap MSBuild.exe directly:

```powershell
artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\MSBuild.exe <path-to-repro.csproj>
```

**Note**: The .NET Framework bootstrap output will only be complete when built on Windows using `MSBuild.exe`.

### Changes not reflected

1. Verify bootstrap was rebuilt: check `artifacts\bin\bootstrap\core\sdk\*\MSBuild.dll` timestamp
2. Kill any lingering MSBuild server processes:
   ```powershell
   ./artifacts/bin/bootstrap/core/dotnet build-server shutdown
   # Or use the helper function after sourcing msbuild-build-env.ps1
   killdotnet
   ```

### Repro works with bootstrap but not with installed MSBuild

Your fix is working! The repro uses your local changes while the installed MSBuild has the bug.

## Quick Reference

| Scenario | Command |
|----------|---------|
| Build bootstrap | `.\build.cmd` |
| .NET Core repro | `artifacts\bin\bootstrap\core\dotnet.exe build <project>` |
| .NET Framework repro | `artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\MSBuild.exe <project>` |

## See Also

- [Bootstrap Documentation](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Bootstrap.md)
- [Building and Debugging Guide](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/Building-Testing-and-Debugging-on-Full-Framework-MSBuild.md)
