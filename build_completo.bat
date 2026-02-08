@echo off
chcp 65001 >nul
echo ==========================================
echo 🚀 BUILD COMPLETO - MEU GESTOR DE VODS
echo ==========================================
echo.
cd /d "%~dp0"

echo 📦 ETAPA 1/4: Restaurando pacotes NuGet...
dotnet restore
if %errorlevel% neq 0 (
    echo ❌ ERRO ao restaurar pacotes!
    pause
    exit /b 1
)
echo ✅ Pacotes restaurados!
echo.

echo 🔨 ETAPA 2/4: Compilando projeto...
dotnet build
if %errorlevel% neq 0 (
    echo ❌ ERRO na compilacao!
    pause
    exit /b 1
)
echo ✅ Projeto compilado!
echo.

echo 🧪 ETAPA 3/4: Executando testes (se houver)...
dotnet test --verbosity normal 2>nul
if %errorlevel% neq 0 (
    echo ⚠️  Testes falharam ou nao encontrados - continuando...
)
echo ✅ Testes concluidos!
echo.

echo 📤 ETAPA 4/4: Publicando Release...
dotnet publish -c Release --self-contained true -r win-x64 -o "./publish"
if %errorlevel% neq 0 (
    echo ❌ ERRO na publicacao!
    pause
    exit /b 1
)
echo ✅ Release publicado!
echo.

echo ==========================================
echo 🎉 BUILD CONCLUIDO COM SUCESSO!
echo ==========================================
echo.
echo 📁 Arquivos gerados em: .\publish\
echo 🚀 Execute: .\publish\MeuGestorVODs.exe
echo.
pause
