call cibuild.cmd --scope build --config Release --bootstrap-only

nuget.exe pack setup\MsBuild.Engine.Corext\MsBuild.Engine.Corext.nuspec -NonInteractive -OutputDirectory bin\Setup -Properties version=%%1;repoRoot=%~dp0;X86BinPath=bin\Release\x86\Windows_NT\Output;X64BinPath=bin\Release\x64\Windows_NT\Output -Verbosity Detailed