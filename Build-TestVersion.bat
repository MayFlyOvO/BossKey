@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-App.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
    pause
)

exit /b %EXIT_CODE%
