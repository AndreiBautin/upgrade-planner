@echo off
setlocal
cd /d "%~dp0"

if not exist "client\node_modules" (
    echo Installing client dependencies (first run only)...
    pushd client
    call npm install
    popd
)

start "Upgrade Planner API" /D "%~dp0server\UpgradePlanner.Api" cmd /k dotnet run --launch-profile http
start "Upgrade Planner Client" /D "%~dp0client" cmd /k npm run dev

echo Waiting for the dev servers to come up...
timeout /t 5 /nobreak >nul
start "" "http://localhost:5176"

endlocal
