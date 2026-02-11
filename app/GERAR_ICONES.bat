@echo off
chcp 65001 >nul
cls

echo ============================================
echo   GERADOR DE ÍCONES - MEU GESTOR DE VODS
echo ============================================
echo.

REM Verificar se as imagens existem
if not exist "icon_original.png" (
    echo ❌ ERRO: icon_original.png não encontrado!
    echo.
    echo Coloque a imagem quadrada (Imagem 1) nesta pasta com o nome:
    echo    icon_original.png
    echo.
    pause
    exit /b 1
)

if not exist "logo_original.png" (
    echo ❌ ERRO: logo_original.png não encontrado!
    echo.
    echo Coloque a imagem horizontal (Imagem 2) nesta pasta com o nome:
    echo    logo_original.png
    echo.
    pause
    exit /b 1
)

echo ✅ Imagens encontradas!
echo.

REM Verificar se PowerShell está disponível
powershell -Command "Get-Host" >nul 2>&1
if errorlevel 1 (
    echo ❌ PowerShell não encontrado!
    pause
    exit /b 1
)

echo 🚀 Executando script PowerShell...
echo.

REM Executar script
powershell -ExecutionPolicy Bypass -File "assets\gerar_icones.ps1" -IconSource "icon_original.png" -LogoSource "logo_original.png" -OutputDir "assets"

echo.
echo ============================================
echo   PROCESSO CONCLUÍDO
echo ============================================
echo.
echo Pressione qualquer tecla para sair...
pause >nul
