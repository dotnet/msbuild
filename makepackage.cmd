set RepoRoot=%~dp0
set RepoRoot=%RepoRoot:~0,-1%

call cibuild.cmd --scope build --config Release --bootstrap-only

nuget.exe pack setup\MsBuild.Engine.Corext\MsBuild.Engine.Corext.nuspec -NonInteractive -OutputDirectory bin\Setup -Properties version=%1;repoRoot=%RepoRoot%;X86BinPath=%~dp0bin\Release\x86\Windows_NT\Output;X64BinPath=%~dp0bin\Release\x64\Windows_NT\Output -Verbosity Detailed