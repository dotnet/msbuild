
set repoRoot=%~dp0..\..\

pushd %repoRoot%
set repoRoot=%CD%\
popd

set X86BinPath=%repoRoot%bin\Release\x86\Windows_NT\Output\
set X64BinPath=%repoRoot%bin\Release\x64\Windows_NT\Output\

nuget pack -Properties "version=2.3.4.5;repoRoot=%repoRoot%;X86BinPath=%X86BinPath%;X64BinPath=%X64BinPath%" -noPackageAnalysis
