@echo off
REM Fully automated P2P integration tests using MelonMod
REM Builds test mod, deploys to Schedule 1, runs tests, reports results

echo =====================================
echo SteamNetworkLib Automated Tests
echo =====================================
echo.

powershell -ExecutionPolicy Bypass -File "Run-AutomatedGameTests.ps1"

echo.
pause
