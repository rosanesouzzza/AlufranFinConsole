@echo off
chcp 65001 > nul
color 0A

echo.
echo =====================================
echo     ALUFRAN - INICIALIZADOR
echo =====================================
echo.

echo [1/2] Iniciando API em http://localhost:5001...
start cmd /k "cd AlufranFinConsole.Api && dotnet run --configuration Debug"

timeout /t 5 /nobreak

echo [2/2] Iniciando Web em http://localhost:5287...
start cmd /k "cd AlufranFinConsole.Web && dotnet run --configuration Debug"

timeout /t 3 /nobreak

echo.
echo =====================================
echo Aplicacoes iniciadas com sucesso!
echo =====================================
echo.
echo API:  http://localhost:5001
echo Web:  http://localhost:5287
echo.
echo Login:
echo   Email: admin@alufran.local
echo   Senha: AlufranAdmin@2026
echo.
echo Abrindo navegador...
start http://localhost:5287

exit /b
