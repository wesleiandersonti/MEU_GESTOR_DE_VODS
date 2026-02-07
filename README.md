# MEU GESTOR DE VODS v1.0

Gerenciador moderno de VODs para listas IPTV M3U, desenvolvido em C# .NET 8.

## Download

Versao 1.0.0 - [Baixar aqui](https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases/latest)

## Instalacao

1. Baixe o arquivo `MEU_GESTOR_DE_VODS.zip` na aba Releases.
2. Extraia para qualquer pasta.
3. Execute `MeuGestorVODs.exe`.

Nao requer instalacao. Aplicativo portatil.

## Funcionalidades

### Arquitetura

- [x] C# .NET 8 - Tecnologia moderna da Microsoft
- [x] Arquitetura MVVM - Separacao clara de responsabilidades
- [x] Injecao de Dependencias - Codigo testavel e modular
- [x] Async/Await - Operacoes nao-bloqueantes

### Performance

- [x] HttpClient - Cliente HTTP moderno e eficiente
- [x] Downloads Paralelos - Configure multiplos downloads simultaneos
- [x] Cache de M3U - Evita downloads repetidos (TTL configuravel)
- [x] Resume de Downloads - Continua downloads interrompidos
- [x] Retry com Exponential Backoff - Recuperacao automatica de falhas

### Interface

- [x] WPF Moderno - Interface clean e responsiva
- [x] Progresso Individual - Barra de progresso para cada download
- [x] Filtro em Tempo Real - Busca instantanea na lista
- [x] DataGrid Avancado - Ordenacao e selecao multipla

### Seguranca

- [x] Validacao de URLs - Apenas HTTP/HTTPS permitido
- [x] Sanitizacao de Paths - Previne path traversal
- [x] Validacao de Config - Valores seguros por padrao

### Logging

- [x] Serilog - Logs estruturados em arquivo
- [x] Rotacao Diaria - Arquivos de log organizados

## Estrutura do Projeto

```text
MeuGestorVODs/
├── Models/
│   ├── M3UEntry.cs          # Modelo de entrada M3U
│   ├── DownloadTask.cs      # Modelo de tarefa de download
│   └── AppConfig.cs         # Configuracao da aplicacao
├── Services/
│   ├── M3UService.cs        # Parsing e cache de M3U
│   ├── DownloadService.cs   # Logica de download
│   ├── PlayerService.cs     # Integracao com players
│   └── UpdateService.cs     # Verificacao de atualizacoes
├── ViewModels/
│   └── MainViewModel.cs     # ViewModel principal
├── Security/
│   └── SecurityValidator.cs # Validacoes de seguranca
├── MainWindow.xaml          # Interface principal
├── App.xaml                 # Configuracao da aplicacao
└── MeuGestorVODs.csproj
```

## Como Compilar

### Pre-requisitos

- .NET 8 SDK
- Visual Studio 2022 ou VS Code

### Comandos

```bash
# Restaurar pacotes
dotnet restore

# Compilar
dotnet build

# Executar
dotnet run

# Publicar (Release)
dotnet publish -c Release -r win-x64 --self-contained true
```

## Configuracao

O arquivo de configuracao e salvo automaticamente em:

```text
%AppData%\MeuGestorVODs\config.json
```

Exemplo:

```json
{
  "M3UUrl": "https://exemplo.com/playlist.m3u",
  "DownloadPath": "C:\\Users\\Usuario\\Videos\\Meu Gestor VODs",
  "MaxParallelDownloads": 3,
  "CacheTtlMinutes": 30,
  "AutoCheckUpdates": true
}
```

## Fluxo de uso

### Download de VODs

1. Cole a URL do arquivo M3U.
2. Clique em "Load" para carregar a lista.
3. Selecione os itens desejados.
4. Clique em "Download Selected".
5. Acompanhe o progresso individual de cada arquivo.

### Player Integrado

- Selecione um item e clique em "Play Selected".
- Suporte automatico a VLC, MPV e players do sistema.

### Filtro

- Digite na caixa "Filter" para buscar por nome.
- Filtro em tempo real na lista.

### Downloads Paralelos

- Configure "Max Parallel Downloads" (1-5).
- Downloads simultaneos com controle de largura de banda.

## Seguranca

- [x] URLs validadas (apenas HTTP/HTTPS)
- [x] Sanitizacao de nomes de arquivo
- [x] Protecao contra path traversal
- [x] Validacao de esquemas de URL
- [x] Verificacao de tamanho de paths

## Logs

Logs sao salvos em:

```text
%AppData%\MeuGestorVODs\logs\app-YYYY-MM-DD.log
```

## Atualizacoes

O sistema verifica automaticamente atualizacoes na inicializacao.
Tambem e possivel verificar manualmente via botao "Check for Updates".

## Licenca

Licenca Comercial (paga) - uso mediante aquisicao de licenca.

## Sobre

MEU GESTOR DE VODS - Aplicativo moderno para gerenciamento de VODs de listas IPTV M3U.
Desenvolvido com C# .NET 8 e arquitetura MVVM.
