@echo off
REM Start API on port 5001
start cmd /k "cd AlufranFinConsole.Api && dotnet run"

REM Wait 5 seconds for API to start
timeout /t 5 /nobreak

REM Start Web on port 5002
start cmd /k "cd AlufranFinConsole.Web && dotnet run"

echo.
echo ====================================
echo Applications are starting...
echo API: https://localhost:5001
echo Web: https://localhost:5002
echo ====================================
