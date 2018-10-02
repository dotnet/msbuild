:: Simple script to build the XmlFileLogger project and then build again with the
:: XML logging enabled. Writes to buildlog.xml.
@echo off
setlocal
set DebugBuildOutputPath=%~dp0..\bin\Samples\Debug\XmlFileLogger
set TempBinPath=%~dp0..\bin\XmlFileLogger
set ProjectToBuild=%~dp0\XmlFileLogger\XmlFileLogger.csproj

echo Building source
MSBuild.exe "%ProjectToBuild%" /v:Minimal /nologo
echo.

echo ROBOCOPY %DebugBuildOutputPath% -^> %TempBinPath%
robocopy "%DebugBuildOutputPath%" "%TempBinPath%" *.* /S /NFL /NDL /NJH /NJS /nc /ns /np
echo.

echo Rebuilding sources with XmlFileLogger enabled (no ouput).
MSBuild.exe "%ProjectToBuild%" /verbosity:diagnostic /logger:XmlFileLogger,%TempBinPath%\XmlFileLogger.dll;%~dp0buildlog.xml /t:Rebuild>nul
echo MSBuild.exe returned exit code %errorlevel%
echo See buildlog.xml for XML log file.