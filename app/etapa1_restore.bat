@echo off
echo ==========================================
echo ETAPA 1: Restaurando pacotes NuGet...
echo ==========================================
cd /d "%~dp0"
dotnet restore
if %errorlevel% neq 0 (
    echo ERRO ao restaurar pacotes!
    pause
    exit /b 1
)
echo.
echo ✓ Pacotes restaurados com sucesso!
echo.
pause
