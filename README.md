# MEU GESTOR DE VODS

Gerenciador de VODs para listas IPTV M3U em C# .NET 8 (WPF).

## 📚 Documentacao

- Base do projeto: `docs/BASE_DO_PROJETO.md`
- Arquitetura XUI One (referencia): `docs/ARQUITETURA_XUI_ONE_IPTV.md`
- Modelo de dados (XUI-style): `docs/MODELO_DE_DADOS_XUI.md`
- Plano de implementacao: `docs/PLANO_DE_IMPLEMENTACAO_XUI.md`
- Plano tecnico por arquivo/classe: `docs/PLANO_TECNICO_POR_ARQUIVO_CLASSE.md`

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

## ✅ Funcionalidades atuais

- Carregar playlist M3U por URL
- Botoes interativos no topo: `Itens` e `Grupos`
- Listar itens com nome e grupo
- Painel secundario de grupos por categoria (com contagem)
- Clique em grupo para filtrar automaticamente a lista principal
- Filtro em tempo real
- Selecionar todos / desmarcar todos
- Download de itens selecionados com progresso por item
- Verificacao de arquivo ja existente (evita baixar duplicado)
- Organizacao automatica em pastas por categoria via TXT
- Verificacao de atualizacao automatica
- Exibicao da versao atual na barra inferior
- Rollback para versao anterior (baixa e abre instalador da release escolhida)
- Clique com botao esquerdo sobre um conteudo para abrir menu rapido
- Menu rapido com:
  - copiar URL do canal
  - colar URL no campo M3U
  - verificar se o link ja esta salvo nos bancos TXT
  - reproduzir o conteudo direto no VLC

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
