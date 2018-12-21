@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0validate-sdk.ps1""" -restore -build -test -sign -pack -publish -ci %*"