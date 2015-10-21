
setlocal EnableDelayedExpansion
where dnvm
if %ERRORLEVEL% neq 0 (
    @powershell -NoProfile -ExecutionPolicy unrestricted -Command "&{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}"
    set PATH=!PATH!;!USERPROFILE!\.dnx\bin
    set DNX_HOME=!USERPROFILE!\.dnx
    goto continue
)

:continue
echo *** Building dotnet ***
call %~dp0scripts/bootstrap.cmd
if %errorlevel% neq 0 exit /b %errorlevel%

echo *** Packaging dotnet ***
call %~dp0scripts/package.cmd
if %errorlevel% neq 0 exit /b %errorlevel%
