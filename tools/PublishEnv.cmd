@echo off

if defined ProgramFiles(x86) (
    set "PublishProgramFiles=%ProgramFiles(x86)%"
) else (
    set "PublishProgramFiles=%ProgramFiles%"
)

if not defined DNX_PACKAGES (
    set DNX_PACKAGES=%~dp0\..\packages
)

set "PublishVSVersion=14.0"
set "PublishRoot=%~dp0"
set "PublishRoot=%PublishRoot:~0,-7%"
set "PublishBin=%PublishRoot%\artifacts\build"
set "PublishIntermediate=%PublishRoot%\intermediate"
set "PublishReferences=%PublishRoot%\references"
set "PublishSource=%PublishRoot%\src"
set "PublishTest=%PublishRoot%\test"
set "PublishTools=%PublishRoot%\tools"

if exist "%PublishProgramFiles%\MSBuild\14.0\Bin\msbuild.exe" (
   set PublishMSBuildPath="%PublishProgramFiles%\MSBuild\14.0\Bin"
)

set "PATH=%PATH%;%PublishMSBuildPath%"
set "PATH=%PATH%;%PublishTools%"