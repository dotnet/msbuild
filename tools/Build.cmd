@echo off

call %~dp0\EnsurePublishEnv.cmd
msbuild %PublishRoot%\dirs.proj /p:configuration=Release