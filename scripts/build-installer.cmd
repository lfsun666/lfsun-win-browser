@echo off
setlocal
set VERSION=0.1.0
if not "%~1"=="" set VERSION=%~1

powershell -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" -Version %VERSION%
if errorlevel 1 (
  echo.
  echo Installer build failed.
  exit /b 1
)
echo.
echo Installer build completed.
endlocal
