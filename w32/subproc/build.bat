@if "%1" == "gcc" GoTo GCCBuild
if not exist .\WinDebug\nul mkdir .\WinDebug
cl.exe /nologo /MT /W4 /GX /Z7 /YX /Od /I .. /I . /I ../include /I ../.. /D WIN32 /D WINDOWS32 /D _DEBUG /D _WINDOWS /FR.\WinDebug/ /Fp.\WinDebug/subproc.pch /Fo.\WinDebug/ /c misc.c
cl.exe /nologo /MT /W4 /GX /Z7 /YX /Od /I .. /I . /I ../include /I ../.. /D WIN32 /D WINDOWS32 /D _DEBUG /D _WINDOWS /FR.\WinDebug/ /Fp.\WinDebug/subproc.pch /Fo.\WinDebug/ /c sub_proc.c
cl.exe /nologo /MT /W4 /GX /Z7 /YX /Od /I .. /I . /I ../include /I ../.. /D WIN32 /D WINDOWS32 /D _DEBUG /D _WINDOWS /FR.\WinDebug/ /Fp.\WinDebug/subproc.pch /Fo.\WinDebug/ /c w32err.c
lib.exe /NOLOGO /OUT:.\WinDebug\subproc.lib  .\WinDebug/misc.obj  .\WinDebug/sub_proc.obj  .\WinDebug/w32err.obj
if not exist .\WinRel\nul mkdir .\WinRel
cl.exe /nologo /MT /W4 /GX /YX /O2 /I .. /I . /I ../include /I ../.. /D WIN32 /D WINDOWS32 /D NDEBUG /D _WINDOWS /FR.\WinRel/ /Fp.\WinRel/subproc.pch /Fo.\WinRel/ /c misc.c
cl.exe /nologo /MT /W4 /GX /YX /O2 /I .. /I . /I ../include /I ../.. /D WIN32 /D WINDOWS32 /D NDEBUG /D _WINDOWS /FR.\WinRel/ /Fp.\WinRel/subproc.pch /Fo.\WinRel/ /c sub_proc.c
cl.exe /nologo /MT /W4 /GX /YX /O2 /I .. /I . /I ../include /I ../.. /D WIN32 /D WINDOWS32 /D NDEBUG /D _WINDOWS /FR.\WinRel/ /Fp.\WinRel/subproc.pch /Fo.\WinRel/ /c w32err.c
lib.exe /NOLOGO /OUT:.\WinRel\subproc.lib  .\WinRel/misc.obj  .\WinRel/sub_proc.obj  .\WinRel/w32err.obj
GoTo BuildEnd
:GCCBuild
gcc -mthreads -Wall -gdwarf-2 -g3 -O2 -I.. -I. -I../include -I../.. -DWINDOWS32 -c misc.c -o ../../w32_misc.o
gcc -mthreads -Wall -gdwarf-2 -g3 -O2 -I.. -I. -I../include -I../.. -DWINDOWS32 -c sub_proc.c -o ../../sub_proc.o
gcc -mthreads -Wall -gdwarf-2 -g3 -O2 -I.. -I. -I../include -I../.. -DWINDOWS32 -c w32err.c -o ../../w32err.o
:BuildEnd

@echo off
rem Copyright (C) 1996-2012 Free Software Foundation, Inc.
rem This file is part of GNU Make.
rem
rem GNU Make is free software; you can redistribute it and/or modify it under
rem the terms of the GNU General Public License as published by the Free
rem Software Foundation; either version 3 of the License, or (at your option)
rem any later version.
rem
rem GNU Make is distributed in the hope that it will be useful, but WITHOUT
rem ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
rem FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for.
rem more details.
rem
rem You should have received a copy of the GNU General Public License along
rem with this program.  If not, see <http://www.gnu.org/licenses/>.
