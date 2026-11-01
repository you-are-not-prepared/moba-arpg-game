@echo off
rem Runs scripts\moba.ps1 without changing PowerShell's script policy. Example: moba start  (in PowerShell: .\moba start)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\moba.ps1" %*
exit /b %ERRORLEVEL%
