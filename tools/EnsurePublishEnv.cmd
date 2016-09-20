@echo off

if not defined PublishRoot (
    echo Initializing Publish environment
    call %~dp0\PublishEnv.cmd
)
