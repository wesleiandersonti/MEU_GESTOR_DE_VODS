@echo off
chcp 65001 >nul
echo ============================================
echo   GERAR ÍCONE - MÉTODO SIMPLES
echo ============================================
echo.
echo Este script gera o ícone do app usando ImageMagick.
echo.

REM Verificar se ImageMagick está instalado
magick -version >nul 2>&1
if errorlevel 1 (
    echo ❌ ImageMagick não encontrado!
    echo.
    echo Instale uma das opções abaixo:
    echo.
    echo OPÇÃO 1 - Chocolatey (Recomendado):
    echo   choco install imagemagick
    echo.
    echo OPÇÃO 2 - Download manual:
    echo   https://imagemagick.org/script/download.php#windows
    echo.
    echo OPÇÃO 3 - Use ferramenta online:
    echo   https://www.icoconverter.com/
    echo.
    pause
    exit /b 1
)

echo ✅ ImageMagick encontrado!
echo.

REM Verificar imagem original
if not exist "icon_original.png" (
    echo ❌ Arquivo icon_original.png não encontrado!
    echo.
    echo Coloque a imagem quadrada nesta pasta com o nome:
    echo    icon_original.png
    echo.
    pause
    exit /b 1
)

echo 🎨 Gerando ícone em múltiplas resoluções...
echo.

REM Criar pasta de saída
if not exist "icons" mkdir icons

REM Gerar cada resolução
echo 📐 16x16...   & magick convert icon_original.png -resize 16x16 icons/icon_16.png
echo 📐 24x24...   & magick convert icon_original.png -resize 24x24 icons/icon_24.png
echo 📐 32x32...   & magick convert icon_original.png -resize 32x32 icons/icon_32.png
echo 📐 48x48...   & magick convert icon_original.png -resize 48x48 icons/icon_48.png
echo 📐 64x64...   & magick convert icon_original.png -resize 64x64 icons/icon_64.png
echo 📐 96x96...   & magick convert icon_original.png -resize 96x96 icons/icon_96.png
echo 📐 128x128... & magick convert icon_original.png -resize 128x128 icons/icon_128.png
echo 📐 256x256... & magick convert icon_original.png -resize 256x256 icons/icon_256.png

echo.
echo 🔄 Combinando em arquivo .ico...
magick convert icons/icon_16.png icons/icon_24.png icons/icon_32.png icons/icon_48.png icons/icon_64.png icons/icon_96.png icons/icon_128.png icons/icon_256.png icons/app_icon.ico

echo.
echo ✅ ÍCONE GERADO COM SUCESSO!
echo.
echo 📁 Arquivo: icons/app_icon.ico
echo 📊 Tamanho: 
for %%F in (icons/app_icon.ico) do echo    %%~zF bytes
echo.
echo ============================================
echo   PRÓXIMOS PASSOS:
echo ============================================
echo 1. O ícone já está configurado no projeto
echo 2. Compile o projeto: dotnet build
echo 3. Execute e verifique o ícone na taskbar
echo.
pause
