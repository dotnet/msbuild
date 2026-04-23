Fixes #12640

### Context

End-to-end tests for multithreaded (`/mt`) MSBuild execution were missing. The existing test coverage only had unit-level tests for task routing but no E2E validation of building real projects with `/mt`.

### Changes Made

- Added new `MSBuild.EndToEnd.Tests` test project with SDK-style and non-SDK-style test assets.
- SDK assets: single console app and multi-project solution (ConsoleApp + 4 libraries).
- Non-SDK assets: single `.NET Framework 4.7.2` project and multi-project solution (ConsoleApp + 2 libraries), Windows-only.
- Tests cover `/m:1 /mt`, `/m:2 /mt`, `/m:8 /mt`, and `/mt` alone.
- Binlog build + replay tests for both SDK and non-SDK.
- Added `global.json` to SDK test assets to prevent bootstrap SDK resolution hijacking (see dotnet/runtime#118488).
- Added `InternalsVisibleTo` for the new test assembly in `Microsoft.Build`, `Microsoft.Build.Framework`, and `MSBuild`.
- Added project to `MSBuild.slnx`.
- Replaced local `CopyFilesRecursively` with existing `FileUtilities.CopyDirectory`.
- Fixed copy-paste bug in `Library4/Class4.cs`.

### Testing

End-to-end tests in `MultithreadedExecution_Tests.cs`:
- `MultithreadedBuild_Success` — SDK-style builds with various `/mt` combinations.
- `MultithreadedBuild_BinaryLogging` — SDK-style build with binlog + replay.
- `MultithreadedBuild_NonSdkStyle_Success` — non-SDK builds (Windows-only).
- `MultithreadedBuild_NonSdkStyle_BinaryLogging` — non-SDK binlog + replay (Windows-only).
