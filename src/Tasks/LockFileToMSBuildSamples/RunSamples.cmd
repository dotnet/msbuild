@echo off
setlocal

set MSBUILD=msbuild /v:m /nologo

REM ------------------------------------------------------
REM Choose a Project File:
set PROJECTLOCKFILE="NetFramework4.5.2\project.lock.json"
REM set PROJECTLOCKFILE="NetCoreApp1.0\project.lock.json"

REM Choose a target moniker (used in some samples):
set TARGETMONIKER=".NETFramework,Version=v4.5.2/win"
REM set TARGETMONIKER=".NETCoreApp,Version=v1.0/osx.10.11-x64"

REM ------------------------------------------------------
REM SAMPLES: Set Default Command Prefix
set RUNSAMPLE=call %MSBUILD% SampleTargets.targets /p:ProjectLockFile=%PROJECTLOCKFILE%

REM ------------------------------------------------------
echo Sample 1: Display Counts of all Items
%RUNSAMPLE% /t:DisplayItemCounts

REM ------------------------------------------------------
REM echo Sample 2: Display The RIDs in the LockFile
REM %RUNSAMPLE% /t:DisplayAllRIDs

REM ------------------------------------------------------
REM echo Sample 3: Display The Targets in the LockFile
REM %RUNSAMPLE% /t:DisplayAllTargets

REM ------------------------------------------------------
REM echo Sample 4: Display libraries in a specific Target: %TARGETMONIKER%
REM %RUNSAMPLE% /t:DisplayLibrariesPerTarget /p:TargetMoniker=%TARGETMONIKER%

REM ------------------------------------------------------
REM echo Sample 5: Display Compile Time Assemblies in a specific Target: %TARGETMONIKER%
REM %RUNSAMPLE% /t:DisplayCompilePerTarget /p:TargetMoniker=%TARGETMONIKER%

REM ------------------------------------------------------
REM echo Sample 6: Display libraries in a specific target and their dependencies: %TARGETMONIKER%
REM %RUNSAMPLE% /t:DisplayLibrariesPerTargetAndDeps /p:TargetMoniker=%TARGETMONIKER%

REM ------------------------------------------------------
REM echo Sample Z: Display all Identities created by LockFileToMSBuild task
REM %RUNSAMPLE% /t:DisplayAllIdentities

