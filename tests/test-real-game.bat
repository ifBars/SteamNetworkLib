@echo off
REM Quick integration test using real Schedule 1 installation
REM This launches host and client instances with Goldberg emulator

echo =====================================
echo SteamNetworkLib Real Game Test
echo =====================================
echo.
echo Choose test mode:
echo 1. Shared Config (faster, requires timing)
echo 2. Isolated Copies (slower, more reliable)
echo.

set /p choice="Enter choice (1 or 2): "

if "%choice%"=="1" (
    echo.
    echo Running with shared config...
    powershell -ExecutionPolicy Bypass -File "Run-RealGameTests.ps1"
) else if "%choice%"=="2" (
    echo.
    echo Running with isolated copies...
    powershell -ExecutionPolicy Bypass -File "Run-RealGameTests-Isolated.ps1"
) else (
    echo.
    echo Invalid choice. Using shared config by default...
    powershell -ExecutionPolicy Bypass -File "Run-RealGameTests.ps1"
)

echo.
pause
