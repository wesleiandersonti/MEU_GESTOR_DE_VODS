# Script PowerShell para Gerar Ícones do Aplicativo
# Requer: ImageMagick instalado (choco install imagemagick)
# Uso: Execute no PowerShell como Administrador: .\gerar_icones.ps1

param(
    [string]$IconSource = "..\..\icon_original.png",
    [string]$LogoSource = "..\..\logo_original.png",
    [string]$OutputDir = "."
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  GERADOR DE ÍCONES - MEU GESTOR DE VODS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se ImageMagick está instalado
$magick = Get-Command magick -ErrorAction SilentlyContinue
if (-not $magick) {
    Write-Host "❌ ImageMagick não encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Instale com:" -ForegroundColor Yellow
    Write-Host "  choco install imagemagick" -ForegroundColor White
    Write-Host ""
    Write-Host "Ou baixe em: https://imagemagick.org/script/download.php#windows" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ ImageMagick encontrado: $($magick.Source)" -ForegroundColor Green
Write-Host ""

# Criar estrutura de pastas
$folders = @(
    "$OutputDir\icons",
    "$OutputDir\logos",
    "$OutputDir\raw"
)

foreach ($folder in $folders) {
    if (-not (Test-Path $folder)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        Write-Host "📁 Criado: $folder" -ForegroundColor Gray
    }
}

Write-Host ""

# ============================================
# GERAR ÍCONE DO APP (Multi-resolução)
# ============================================
Write-Host "🎨 Gerando ícone do aplicativo..." -ForegroundColor Yellow

$iconSizes = @(16, 24, 32, 48, 64, 96, 128, 256)
$iconTempDir = "$OutputDir\icons\temp"
New-Item -ItemType Directory -Path $iconTempDir -Force | Out-Null

foreach ($size in $iconSizes) {
    $outputFile = "$iconTempDir\icon_$size.png"
    Write-Host "  📐 $size x $size..." -ForegroundColor Gray -NoNewline
    
    try {
        & magick convert "$IconSource" -resize ${size}x${size} -background transparent "$outputFile" 2>$null
        Write-Host " OK" -ForegroundColor Green
    } catch {
        Write-Host " ERRO" -ForegroundColor Red
    }
}

# Combinar em arquivo .ico
Write-Host "  🔄 Criando app_icon.ico..." -ForegroundColor Gray -NoNewline
try {
    $pngFiles = Get-ChildItem "$iconTempDir\icon_*.png" | Sort-Object Name
    & magick convert ($pngFiles.FullName) "$OutputDir\icons\app_icon.ico" 2>$null
    Write-Host " OK" -ForegroundColor Green
} catch {
    Write-Host " ERRO" -ForegroundColor Red
    Write-Host "     $_" -ForegroundColor Red
}

# Copiar PNGs individuais
foreach ($size in @(32, 48, 128, 256)) {
    Copy-Item "$iconTempDir\icon_$size.png" "$OutputDir\icons\app_icon_$size.png" -Force
}

# Limpar temp
Remove-Item $iconTempDir -Recurse -Force

Write-Host ""

# ============================================
# GERAR LOGOS (Diferentes resoluções)
# ============================================
Write-Host "🖼️  Gerando logos do sistema..." -ForegroundColor Yellow

$logoSizes = @(
    @{Width=1200; Height=630; Name="logo_social"},      # Open Graph
    @{Width=1200; Height=600; Name="logo_twitter"},     # Twitter Card
    @{Width=1280; Height=640; Name="logo_youtube"},     # YouTube
    @{Width=1920; Height=1080; Name="logo_hd"},         # Full HD
    @{Width=800; Height=400; Name="logo_readme"}        # README
)

foreach ($logo in $logoSizes) {
    $outputFile = "$OutputDir\logos\$($logo.Name).png"
    Write-Host "  📐 $($logo.Width) x $($logo.Height)..." -ForegroundColor Gray -NoNewline
    
    try {
        & magick convert "$LogoSource" -resize "$($logo.Width)x$($logo.Height)" -background transparent -gravity center -extent "$($logo.Width)x$($logo.Height)" "$outputFile" 2>$null
        Write-Host " OK" -ForegroundColor Green
    } catch {
        Write-Host " ERRO" -ForegroundColor Red
    }
}

Write-Host ""

# ============================================
# COPIAR ARQUIVOS ORIGINAIS
# ============================================
Write-Host "📂 Copiando arquivos originais..." -ForegroundColor Yellow

if (Test-Path $IconSource) {
    Copy-Item $IconSource "$OutputDir\raw\icon_original.png" -Force
    Write-Host "  ✅ icon_original.png" -ForegroundColor Green
}

if (Test-Path $LogoSource) {
    Copy-Item $LogoSource "$OutputDir\raw\logo_original.png" -Force
    Write-Host "  ✅ logo_original.png" -ForegroundColor Green
}

Write-Host ""

# ============================================
# RESUMO
# ============================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ✅ GERAÇÃO CONCLUÍDA!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📁 Arquivos gerados em: $OutputDir" -ForegroundColor White
Write-Host ""
Write-Host "Ícones do App:" -ForegroundColor Yellow
Get-ChildItem "$OutputDir\icons\*" -Include *.ico,*.png | ForEach-Object {
    $size = "{0:N2} KB" -f ($_.Length / 1KB)
    Write-Host "  📄 $($_.Name) ($size)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Logos do Sistema:" -ForegroundColor Yellow
Get-ChildItem "$OutputDir\logos\*.png" | ForEach-Object {
    $size = "{0:N2} KB" -f ($_.Length / 1KB)
    Write-Host "  📄 $($_.Name) ($size)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Próximos passos:" -ForegroundColor Cyan
Write-Host "  1. Configure o arquivo .csproj com:" -ForegroundColor White
Write-Host "     <ApplicationIcon>assets\icons\app_icon.ico</ApplicationIcon>" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Atualize MainWindow.xaml:" -ForegroundColor White
Write-Host "     Icon=""assets/icons/app_icon.ico""" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Atualize o instalador Inno Setup" -ForegroundColor White
Write-Host ""
