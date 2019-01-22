setlocal

set WINFORMS_REPO_PATH=%1
if "%WINFORMS_REPO_PATH%" == "" (
    echo Please pass a path to a local clone of the winforms repo
    exit /b 1
)

set WINFORMS_SHA=%2

if "%WINFORMS_SHA%" == "" (
    echo Please pass a winforms-repo SHA
    exit /b 1
)

set MSBUILD_WINFORMS_DIRECTORY=%~dp0

pushd %WINFORMS_REPO_PATH%

git checkout %WINFORMS_SHA%

if ERRORLEVEL 1 (
    echo Git checkout failed
    exit /b 1
)

popd

robocopy /mir %WINFORMS_REPO_PATH%\src\System.Windows.Forms\src\System\Resources %MSBUILD_WINFORMS_DIRECTORY%\System\Resources
set ROBOCOPY_ERROR=%ERRORLEVEL%

if "%ROBOCOPY_ERROR%"=="0" (
    goto :copy_ok
)
if "%ROBOCOPY_ERROR%"=="1" (
    goto :copy_ok
)

echo Copy error
exit /b 1

:copy_ok

md %MSBUILD_WINFORMS_DIRECTORY%\misc

copy /y %WINFORMS_REPO_PATH%\src\System.Windows.Forms\src\misc\ClientUtils.cs %MSBUILD_WINFORMS_DIRECTORY%\misc
if errorlevel 1 (
    echo Failed copying ClientUtils.cs
    exit /b 1
)

copy /y %WINFORMS_REPO_PATH%\src\System.Windows.Forms\src\misc\MultitargetUtil.cs %MSBUILD_WINFORMS_DIRECTORY%\misc
if errorlevel 1 (
    echo Failed copying MultitargetUtil.cs
    exit /b 1
)

md %MSBUILD_WINFORMS_DIRECTORY%\Resources

copy /y %WINFORMS_REPO_PATH%\src\System.Windows.Forms\src\Resources\SR.resx %MSBUILD_WINFORMS_DIRECTORY%\Resources\SR.resx
if errorlevel 1 (
    echo Failed copying SR.resx
    exit /b 1
)

git add %MSBUILD_WINFORMS_DIRECTORY%
