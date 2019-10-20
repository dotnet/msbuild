@echo off
set ALL_ARGS=
set a=%1
if not defined a goto :doneSetArgs

set ALL_ARGS=%~1%
shift

:setArgs
set a=%1
if not defined a goto :doneSetArgs

set ALL_ARGS=%ALL_ARGS%,%~1%
shift
goto setArgs

:doneSetArgs
echo %ALL_ARGS%
goto :EOF
