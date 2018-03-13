
set repoRoot=%~dp0..\..\

pushd %repoRoot%
set repoRoot=%CD%
popd

set X86BinPath=%repoRoot%\bin\Release\x86\Windows_NT\Output
set X64BinPath=%repoRoot%\bin\Release\x64\Windows_NT\Output

set version=0.0.0.0

if not "%1" == "" (
    set version=%1
)

nuget pack -noPackageAnalysis -Properties "version=%version%;repoRoot=%repoRoot%;X86BinPath=%X86BinPath%;X64BinPath=%X64BinPath%"
