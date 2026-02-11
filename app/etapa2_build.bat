@echo off
echo ==========================================
echo ETAPA 2: Compilando o projeto...
echo ==========================================
cd /d "%~dp0"
dotnet build
if %errorlevel% neq 0 (
    echo ERRO na compilacao!
    pause
    exit /b 1
)
echo.
echo ✓ Projeto compilado com sucesso!
echo.
pause
