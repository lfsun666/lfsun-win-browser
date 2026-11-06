@echo off
setlocal
cd /d "%~dp0.."
powershell -ExecutionPolicy Bypass -File "%~dp0release.ps1" %*
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Release build failed with code %ERRORLEVEL%.
    pause
    exit /b %ERRORLEVEL%
)
endlocal
