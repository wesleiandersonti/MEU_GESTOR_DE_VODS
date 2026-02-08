@echo off
echo ==========================================
echo ETAPA 4: Publicando Release...
echo ==========================================
cd /d "%~dp0"
dotnet publish -c Release --self-contained true -r win-x64 -o "./publish"
if %errorlevel% neq 0 (
    echo ERRO na publicacao!
    pause
    exit /b 1
)
echo.
echo ✓ Release publicado com sucesso!
echo Local: .\publish\
echo.
pause
