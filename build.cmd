@echo off
cd %~dp0

SETLOCAL
SET NUGET_VERSION=latest
SET CACHED_NUGET=%LocalAppData%\NuGet\nuget.%NUGET_VERSION%.exe
SET BUILDCMD_KOREBUILD_VERSION=""
SET BUILDCMD_DNX_VERSION=""

IF EXIST %CACHED_NUGET% goto copynuget
echo Downloading latest version of NuGet.exe...
IF NOT EXIST %LocalAppData%\NuGet md %LocalAppData%\NuGet
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/%NUGET_VERSION%/nuget.exe' -OutFile '%CACHED_NUGET%'"

:copynuget
IF EXIST .nuget\nuget.exe goto restore
md .nuget
copy %CACHED_NUGET% .nuget\nuget.exe > nul

:restore
IF EXIST packages\KoreBuild goto run
IF %BUILDCMD_KOREBUILD_VERSION%=="" (
	.nuget\nuget.exe install KoreBuild -ExcludeVersion -o packages -nocache -pre
) ELSE (
	.nuget\nuget.exe install KoreBuild -version %BUILDCMD_KOREBUILD_VERSION% -ExcludeVersion -o packages -nocache -pre
)
.nuget\nuget.exe install Sake -ExcludeVersion -Out packages

:run
CALL packages\KoreBuild\build\dnvm use default -runtime CoreCLR -arch x64
packages\Sake\tools\Sake.exe -I packages\KoreBuild\build -f makefile.shade %*
