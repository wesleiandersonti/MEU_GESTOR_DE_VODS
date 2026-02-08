# MEU GESTOR DE VODS

Gerenciador de VODs para listas IPTV M3U em C# .NET 8 (WPF).

## 📚 Documentacao

### Para Usuarios
- **CHANGELOG.md** - Historico completo de versoes e alteracoes
- **BUILD_INSTRUCTIONS.md** - Como compilar o projeto

### Para Desenvolvedores
- **docs/PROJECT_ARCHITECTURE.md** - Arquitetura completa do sistema
- **docs/DATABASE_SCHEMA.md** - Documentacao do banco SQLite
- **docs/QUICK_START.md** - Guia rapido de desenvolvimento
- **docs/PLANO_TECNICO_POR_ARQUIVO_CLASSE.md** - Detalhes tecnicos

### Referencias
- **docs/BASE_DO_PROJETO.md** - Base e conceitos
- **docs/ARQUITETURA_XUI_ONE_IPTV.md** - Arquitetura XUI One
- **docs/MODELO_DE_DADOS_XUI.md** - Modelo de dados

## 🚀 Download

- Instalador (recomendado): `MeuGestorVODs-Setup-vX.X.X.exe`
- Portatil: `MeuGestorVODs-vX.X.X.zip`
- Releases: [https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases/latest](https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases/latest)

## 📋 Instalacao

### Opcao 1 - Instalador (recomendado)

1. Baixe `MeuGestorVODs-Setup-vX.X.X.exe`.
2. Execute o instalador.
3. O atalho da Area de Trabalho e criado automaticamente.
4. O instalador oferece:
   - atalhos no Menu Iniciar
   - pasta personalizada do Menu Iniciar
   - opcao "Iniciar com o Windows"
5. Para remover:
   - Menu Iniciar > `Desinstalar MEU GESTOR DE VODS`
   - ou Configuracoes do Windows > Aplicativos > Desinstalar

### Opcao 2 - Portatil

1. Baixe `MeuGestorVODs-vX.X.X.zip`.
2. Extraia para qualquer pasta.
3. Execute `MeuGestorVODs.exe`.

## ✅ Funcionalidades

### 🎬 Gerenciamento de VODs
- Carregar playlist M3U por URL
- ComboBox com historico de URLs (ultimas 20)
- Botoes interativos no topo: `Itens` e `Grupos`
- Listar itens com nome e grupo
- Painel secundario de grupos por categoria (com contagem)
- Clique em grupo para filtrar automaticamente a lista principal
- Filtro em tempo real
- Selecionar todos / desmarcar todos
- Download de itens selecionados com progresso por item
- Verificacao de arquivo ja existente (evita baixar duplicado)

### 💾 Banco de Dados SQLite
- **Persistencia em SQLite** (banco principal)
- **Historico de URLs M3U** com status online/offline
- **Botao "Historico"** - Visualiza todas as URLs testadas
- **Botao "Limpar Offline"** - Remove URLs fora do ar
- **Estatisticas do banco** - Total de entradas e categorias
- Backup automatico em arquivos TXT (formato M3U)
- Migracao automatica de dados antigos

### 🔄 Atualizacoes
- Verificacao de atualizacao automatica via GitHub
- Exibicao da versao atual na barra inferior
- Rollback para versao anterior

### 🎯 Acoes Rapidas
- Clique com botao esquerdo sobre um conteudo para abrir menu rapido:
  - Copiar URL do canal
  - Colar URL no campo M3U
  - Verificar se o link esta salvo no banco
  - Reproduzir o conteudo direto no VLC

## ⚙️ Estrutura automatica de pastas (TXT)

Na pasta de download selecionada, o app cria automaticamente:

```text
estrutura_downloads.txt
```

Formato:

```text
# Formato: Categoria=Pasta
Videos=Videos
Series=Series
Filmes=Filmes
Canais=Canais
24 Horas=24 Horas
Documentarios=Documentarios
Novelas=Novelas
Outros=Outros
```

Comportamento no download:

- cria as pastas automaticamente
- classifica cada item para a pasta correspondente
- ignora item que ja existe no destino (`Ja existe - ignorado`)

## 🗂️ Banco TXT de links (integracao automatica)

Assim que voce clica em `Carregar` e a URL M3U e validada, o app integra os links automaticamente em dois arquivos TXT dentro da pasta de download:

- `banco_vod_links.txt` (videos e series)
- `banco_canais_ao_vivo.txt` (canais ao vivo)

Formato de cada linha:

```text
Nome|Grupo|URL
```

Regras aplicadas:

- evita duplicidade por URL (nao grava o mesmo link duas vezes)
- separa automaticamente VOD x canais ao vivo
- salva links M3U/M3U8 e demais URLs conforme classificados na lista
- botoes na barra inferior para abrir direto no Bloco de Notas:
  - `Abrir TXT VOD`
  - `Abrir TXT Canais`
- no menu rapido do item (clique esquerdo):
  - `Verificar se ja esta salvo no TXT`
  - `Reproduzir no VLC`

## 🔄 Atualizacao e rollback

- Botao `Verificar Atualizacoes`:
  - consulta a ultima release do GitHub
  - compara com a versao instalada
  - baixa e abre o instalador automaticamente quando houver versao nova
- Campo `Versao atual` na barra inferior mostra a versao instalada
- Botao `Voltar Versao`:
  - permite informar a tag desejada (ex: `v1.0.4`)
  - baixa e executa o instalador da versao anterior

## 🔧 Build local

Pre-requisitos:

- .NET 8 SDK

Comandos:

```bash
dotnet restore
dotnet build
dotnet publish -c Release -r win-x64 --self-contained -o output
```

## 🤖 CI/CD

Workflow em `.github/workflows/build.yml`:

- build Windows
- gera instalador (`.exe`) com Inno Setup
- gera pacote portatil (`.zip`)
- publica assets automaticamente em releases por tag `v*`

## 📁 Estrutura atual do projeto

```text
.
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── Services.cs
├── App.xaml
├── App.xaml.cs
├── MeuGestorVODs.csproj
├── installer/
│   └── MeuGestorVODs.iss
└── .github/workflows/
    └── build.yml
```

## 📄 Licenca

Licenca Comercial (paga) - uso mediante aquisicao de licenca.
