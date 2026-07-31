@echo off
setlocal
cd /d "%~dp0"

echo Upgrade Planner launcher
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: "dotnet" was not found on PATH.
    echo Install the .NET SDK from https://dotnet.microsoft.com/download and try again.
    pause
    exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
    echo ERROR: "npm" was not found on PATH.
    echo Install Node.js from https://nodejs.org and try again.
    pause
    exit /b 1
)

if not exist "client\node_modules" (
    echo Installing client dependencies - first run only
    pushd client
    call npm install
    popd
)

echo Starting API on http://localhost:5131 ...
start "Upgrade Planner API" /D "%~dp0server\UpgradePlanner.Api" cmd /k dotnet run --launch-profile http

echo Starting client on http://localhost:5176 ...
start "Upgrade Planner Client" /D "%~dp0client" cmd /k npm run dev

echo.
echo Waiting for the dev servers to come up...
timeout /t 5 /nobreak >nul
start "" "http://localhost:5176"

echo.
echo Both servers should now be running in their own windows.
echo If a window closed instead of staying open, scroll up in that
echo window's history (or re-run) to see what error it printed.
echo.
echo This window is safe to close.
pause
