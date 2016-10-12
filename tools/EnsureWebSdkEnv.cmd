@echo off

if not defined WebSdkRoot (
    echo Initializing web sdk environment
    call %~dp0\WebSdkEnv.cmd
)
