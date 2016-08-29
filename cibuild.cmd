@echo off

echo cibuild.cmd invoked from the master branch--calling RebuildWithLocalMSBuild.cmd

RebuildWithLocalMSBuild.cmd /p:LocalizedBuild=true
