@if "%_echo%"=="" echo off

::@echo ... Starting msbuild.exe (only) pupdate ...
echo Starting msbuild.exe (only) update...
setlocal

if exist xpupdate.log del xpupdate.log


for /f %%i in ('dir /b /ad /on %windir%\microsoft.net\framework\v4.*') do set fxpath=%windir%\microsoft.net\framework\%%i

call :Doit copy %_NTTREE%\bin\i386\msbuild.??? %fxpath% /y
call :Doit copy %_NTTREE%\bin\i386\msbuild.urt.config %fxpath%\msbuild.exe.config /y

@echo Now kicking off async refresh of native images ...

setlocal
set complus_installroot=
set complus_version=

 start "update native image for msbuildexe" /low /min %fxpath%\ngen install "%_NTTREE%\bin\i386\msbuild.exe"

endlocal


goto :eof


::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:doit
echo %* >> xpupdate.log
%* >> xpupdate.log 2>&1 2>con
if errorlevel 1 echo Error running command "%*" >> xpupdate.log > con
goto :eof


