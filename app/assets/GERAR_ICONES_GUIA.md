# 📦 PACOTE DE GERAÇÃO DE ÍCONES - MEU GESTOR DE VODS

## 🎯 Visão Geral

Este pacote contém tudo necessário para gerar ícones do aplicativo em todas as resoluções necessárias para Windows.

---

## 📋 Resoluções Necessárias

### Para Ícone do Aplicativo (.ico) - Imagem 1 (Quadrada)

| Resolução | Uso |
|-----------|-----|
| 16x16 | Taskbar pequena, lista de janelas |
| 24x24 | Menu Iniciar (modo compacto) |
| 32x32 | Taskbar padrão, Explorer |
| 48x48 | Menu Iniciar (modo padrão) |
| 64x64 | Painel de controle, configurações |
| 96x96 | Explorer (vista detalhada) |
| 128x128 | Explorer (vista grandes ícones) |
| 256x256 | Explorer (vista extra grande) |

### Para Logo do Sistema (.png) - Imagem 2 (Horizontal)

| Resolução | Uso |
|-----------|-----|
| 1200x630 | Open Graph (Facebook, LinkedIn) |
| 1200x600 | Twitter Card |
| 1280x640 | YouTube thumbnail |
| 1920x1080 | Wallpaper/Splash screen |

---

## 🛠️ Opção 1: Ferramenta Online (Recomendado - Mais Fácil)

### Passo a Passo:

1. **Acesse**: https://www.icoconverter.com/ ou https://convertio.co/png-ico/

2. **Para o Ícone do App (Imagem 1)**:
   - Faça upload da imagem quadrada
   - Selecione: Multi-size icon file
   - Escolha tamanhos: 16, 32, 48, 64, 128, 256
   - Baixe: `app_icon.ico`

3. **Para as demais resoluções**:
   - Use: https://www.iloveimg.com/resize-image
   - Faça upload da imagem
   - Defina as dimensões desejadas
   - Baixe cada versão

---

## 🛠️ Opção 2: ImageMagick (Automatizado)

### Instalação:
```powershell
# Via Chocolatey
choco install imagemagick

# Ou baixe em:
# https://imagemagick.org/script/download.php#windows
```

### Script de Geração Automática:

Execute o script `gerar_icones.ps1` (incluído neste pacote):

```powershell
# Abra PowerShell como Administrador
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
.\gerar_icones.ps1
```

---

## 🛠️ Opção 3: GIMP (Gratuito)

1. Abra a imagem no GIMP
2. File → Export As
3. Nome: `icon.ico`
4. Selecione "Microsoft Windows icon"
5. Na janela de exportação, marque todas as resoluções desejadas
6. Export

---

## 📁 Estrutura de Arquivos Sugerida

```
assets/
├── icons/
│   ├── app_icon.ico              (Multi-resolução: 16-256)
│   ├── app_icon_16.png
│   ├── app_icon_32.png
│   ├── app_icon_48.png
│   ├── app_icon_128.png
│   └── app_icon_256.png
│
├── logos/
│   ├── logo_horizontal.png       (Imagem 2 original)
│   ├── logo_horizontal_1200.png  (Para README)
│   ├── logo_horizontal_1920.png  (Para splash)
│   └── logo_splash.png           (Para tela inicial)
│
└── raw/                          (Imagens originais)
    ├── icon_original.png         (Imagem 1)
    └── logo_original.png         (Imagem 2)
```

---

## 🔧 Configuração no Projeto

### 1. Adicionar ao Projeto Visual Studio

No arquivo `.csproj`:

```xml
<PropertyGroup>
  <ApplicationIcon>assets\icons\app_icon.ico</ApplicationIcon>
</PropertyGroup>

<ItemGroup>
  <Content Include="assets\icons\app_icon.ico">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### 2. Configurar Janela Principal

Em `MainWindow.xaml`:

```xml
<Window x:Class="MeuGestorVODs.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="MEU GESTOR DE VODS"
        Icon="assets/icons/app_icon.ico"
        ...>
```

### 3. Configurar Instalador (Inno Setup)

No arquivo `.iss`:

```pascal
[Setup]
SetupIconFile=assets\icons\app_icon.ico
UninstallDisplayIcon={app}\assets\icons\app_icon.ico

[Files]
Source: "assets\icons\app_icon.ico"; DestDir: "{app}\assets\icons"; Flags: ignoreversion
Source: "assets\logos\logo_horizontal.png"; DestDir: "{app}\assets\logos"; Flags: ignoreversion
```

### 4. Configurar Assembly Info

Em `AssemblyInfo.cs`:

```csharp
[assembly: AssemblyTitle("MEU GESTOR DE VODS")]
[assembly: AssemblyDescription("Gerenciador de Playlists IPTV M3U")]
[assembly: AssemblyCompany("wesleiandersonti")]
[assembly: AssemblyProduct("MEU GESTOR DE VODS")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Version info
[assembly: AssemblyVersion("1.0.43.0")]
[assembly: AssemblyFileVersion("1.0.43.0")]
```

---

## 🎨 Especificações Técnicas

### Formato .ICO
- **Formato**: Microsoft Windows Icon
- **Compressão**: PNG (para tamanhos ≥ 64x64) ou BMP (para menores)
- **Profundidade de cor**: 32-bit (RGBA)
- **Transparência**: Suportada

### Formatos PNG
- **Compressão**: PNG-24 ou PNG-32
- **Profundidade**: 24-bit (RGB) ou 32-bit (RGBA)
- **DPI**: 96 (padrão) ou 144 (HiDPI)

---

## ✅ Checklist de Implementação

- [ ] Gerar `app_icon.ico` (multi-resolução 16-256)
- [ ] Criar pasta `assets/icons/`
- [ ] Criar pasta `assets/logos/`
- [ ] Configurar `.csproj` com `<ApplicationIcon>`
- [ ] Adicionar `Icon` no `MainWindow.xaml`
- [ ] Atualizar instalador Inno Setup
- [ ] Testar ícone na taskbar
- [ ] Testar ícone no menu iniciar
- [ ] Testar ícone no Explorer (todas as vistas)
- [ ] Adicionar logo ao README.md
- [ ] Adicionar logo à tela de splash (opcional)

---

## 📞 Suporte

Se tiver problemas:
1. Verifique se a imagem original é PNG com transparência
2. Teste o ícone em diferentes tamanhos no Explorer
3. Use o Resource Hacker para verificar o conteúdo do .ico

---

**Criado em:** Fevereiro 2026
**Para:** MEU GESTOR DE VODS v1.0.43
