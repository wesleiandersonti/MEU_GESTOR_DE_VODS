# MEU GESTOR DE VODS v1.0

Gerenciador moderno de VODs para listas IPTV M3U, desenvolvido em C# .NET 8.

## 🚀 Funcionalidades

### Arquitetura
- ✅ **C# .NET 8** - Tecnologia moderna da Microsoft
- ✅ **Arquitetura MVVM** - Separação clara de responsabilidades
- ✅ **Injeção de Dependências** - Código testável e modular
- ✅ **Async/Await** - Operações não-bloqueantes

### Performance
- ✅ **HttpClient** - Cliente HTTP moderno e eficiente
- ✅ **Downloads Paralelos** - Configure múltiplos downloads simultâneos
- ✅ **Cache de M3U** - Evita downloads repetidos (TTL configurável)
- ✅ **Resume de Downloads** - Continua downloads interrompidos
- ✅ **Retry com Exponential Backoff** - Recuperação automática de falhas

### Interface
- ✅ **WPF Moderno** - Interface clean e responsiva
- ✅ **Progresso Individual** - Barra de progresso para cada download
- ✅ **Filtro em Tempo Real** - Busca instantânea na lista
- ✅ **DataGrid Avançado** - Ordenação e seleção múltipla

### Segurança
- ✅ **Validação de URLs** - Apenas HTTP/HTTPS permitido
- ✅ **Sanitização de Paths** - Previne path traversal
- ✅ **Validação de Config** - Valores seguros por padrão

### Logging
- ✅ **Serilog** - Logs estruturados em arquivo
- ✅ **Rotação Diária** - Arquivos de log organizados

## 📁 Estrutura do Projeto

```
MeuGestorVODs/
├── Models/
│   ├── M3UEntry.cs          # Modelo de entrada M3U
│   ├── DownloadTask.cs      # Modelo de tarefa de download
│   └── AppConfig.cs         # Configuração da aplicação
├── Services/
│   ├── M3UService.cs        # Parsing e cache de M3U
│   ├── DownloadService.cs   # Lógica de download
│   ├── PlayerService.cs     # Integração com players
│   └── UpdateService.cs     # Verificação de atualizações
├── ViewModels/
│   └── MainViewModel.cs     # ViewModel principal
├── Security/
│   └── SecurityValidator.cs # Validações de segurança
├── MainWindow.xaml          # Interface principal
├── App.xaml                 # Configuração da aplicação
└── MeuGestorVODs.csproj
```

## 🔧 Como Compilar

### Pré-requisitos
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

## ⚙️ Configuração

O arquivo de configuração é salvo automaticamente em:
```
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

## 🎯 Funcionalidades

### Download de VODs
1. Cole a URL do arquivo M3U
2. Clique em "Load" para carregar a lista
3. Selecione os itens desejados
4. Clique em "Download Selected"
5. Acompanhe o progresso individual de cada arquivo

### Player Integrado
- Selecione um item e clique em "Play Selected"
- Suporte automático a VLC, MPV e players do sistema

### Filtro
- Digite na caixa "Filter" para buscar por nome
- Filtro em tempo real na lista

### Downloads Paralelos
- Configure "Max Parallel Downloads" (1-5)
- Downloads simultâneos com controle de largura de banda

## 🛡️ Segurança

- ✅ URLs validadas (apenas HTTP/HTTPS)
- ✅ Sanitização de nomes de arquivo
- ✅ Proteção contra path traversal
- ✅ Validação de esquemas de URL
- ✅ Verificação de tamanho de paths

## 📝 Logs

Logs são salvos em:
```
%AppData%\MeuGestorVODs\logs\app-YYYY-MM-DD.log
```

## 🔄 Atualizações

O sistema verifica automaticamente atualizações na inicialização.
Também é possível verificar manualmente via botão "Check for Updates".

## 📄 Licença

MIT License - Free to use and modify.

## 🙏 Sobre

MEU GESTOR DE VODS - Aplicativo moderno para gerenciamento de VODs de listas IPTV M3U.
Desenvolvido com C# .NET 8 e arquitetura MVVM.
