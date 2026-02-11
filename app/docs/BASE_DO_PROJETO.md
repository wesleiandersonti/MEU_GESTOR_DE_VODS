# Base do Projeto - MEU GESTOR DE VODS

Este documento e a base oficial de referencia do projeto fora do README.

Objetivo: manter visao tecnica, funcional e de operacao em um unico lugar para orientar evolucao, suporte e release.

Documentos complementares:

- `docs/ARQUITETURA_XUI_ONE_IPTV.md` - arquitetura de referencia XUI One, modelo de dados e diretrizes IPTV
- `docs/MODELO_DE_DADOS_XUI.md` - entidades, relacionamentos e contrato de dados
- `docs/PLANO_DE_IMPLEMENTACAO_XUI.md` - roadmap por fases, DoD e criterios de sucesso
- `docs/PLANO_TECNICO_POR_ARQUIVO_CLASSE.md` - plano detalhado de execucao por arquivos e classes
- `README.md` - visao rapida de uso e instalacao

---

## 1) Objetivo do produto

MEU GESTOR DE VODS e um aplicativo Windows (WPF, .NET 8) para:

- carregar listas IPTV M3U/M3U8 por URL
- listar e filtrar conteudo
- baixar conteudos selecionados
- organizar arquivos por categorias em estrutura automatica de pastas
- manter banco TXT local de links VOD e canais ao vivo
- atualizar e fazer rollback de versao via GitHub Releases

---

## 2) Escopo atual (implementado)

- carga de playlist M3U por URL
- parse de itens com Nome, Grupo e URL
- filtro em tempo real
- selecao em lote e download com progresso por item
- controle de duplicidade de arquivo no download
- estrutura automatica por TXT (`estrutura_downloads.txt`)
- banco TXT de links VOD (`banco_vod_links.txt`)
- banco TXT de canais ao vivo (`banco_canais_ao_vivo.txt`)
- botoes para abrir os dois bancos TXT no Bloco de Notas
- atualizacao automatica (download do setup da release mais nova)
- rollback para versao anterior por tag
- integracao com VLC para reproducao direta de item selecionado

---

## 3) Arquitetura atual

### 3.1 UI

- `MainWindow.xaml`: layout principal
- `MainWindow.xaml.cs`: eventos da interface e orquestracao dos fluxos

### 3.2 Servicos

- `Services.cs` contem:
  - `M3UEntry`
  - `M3UParser`
  - `M3UService`
  - `DownloadService`

### 3.3 Configuracao e build

- `MeuGestorVODs.csproj`
- workflow: `.github/workflows/build.yml`
- instalador Inno Setup: `installer/MeuGestorVODs.iss`

---

## 4) Fluxos principais

### 4.1 Carregar lista

1. Usuario informa URL M3U
2. App baixa conteudo da URL
3. Parser extrai itens
4. UI exibe itens e total
5. App salva links em bancos TXT (VOD e canais ao vivo)

### 4.2 Download

1. Usuario seleciona itens
2. App resolve categoria/pasta com base em `estrutura_downloads.txt`
3. App cria pastas automaticamente se nao existirem
4. App ignora arquivo ja existente
5. App baixa e atualiza progresso por item

### 4.3 Atualizacao e rollback

1. `Verificar Atualizacoes` consulta release mais recente no GitHub
2. Se houver versao nova, baixa setup e abre instalador
3. `Voltar Versao` permite informar tag anterior e executar rollback

---

## 5) Contratos TXT locais (banco de dados TXT)

Arquivos criados na pasta de download selecionada:

- `estrutura_downloads.txt`
- `banco_vod_links.txt`
- `banco_canais_ao_vivo.txt`

### 5.1 Formato `estrutura_downloads.txt`

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

### 5.2 Formato bancos de links

```text
Nome|Grupo|URL
```

Regras:

- nao duplicar por URL
- separar VOD e canais ao vivo

---

## 6) Padrao de qualidade (DoD)

Uma entrega so e considerada pronta quando:

- compila local (`dotnet build`)
- build GitHub Actions verde
- fluxo principal validado manualmente
- README atualizado quando houver mudanca funcional
- este documento atualizado quando houver mudanca de arquitetura/processo

---

## 7) Operacao de release

### 7.1 Build local

```bash
dotnet restore
dotnet build
dotnet publish -c Release -r win-x64 --self-contained -o output
```

### 7.2 Build CI

Push em `main`/`master` dispara build.
Tag `v*` dispara release com:

- instalador (`MeuGestorVODs-Setup-vX.X.X.exe`)
- pacote portatil (`MeuGestorVODs-vX.X.X.zip`)

---

## 8) Convencoes de desenvolvimento

- manter logica de negocio fora de UI sempre que possivel
- evitar regressao no fluxo de download
- preservar compatibilidade dos arquivos TXT existentes
- documentar toda mudanca relevante neste arquivo

---

## 9) Backlog tecnico recomendado (proximas fases)

- separar `Services.cs` em arquivos por responsabilidade
- extrair camada de dominio e aplicacao
- criar janela dedicada para grupos/categorias
- adicionar progresso global de processamento da URL (1% a 100%)
- adicionar testes de parser e classificacao

---

## 10) Notas de produto (entrada do time)

Use esta secao para registrar requisitos novos aprovados antes de implementar.

Template sugerido:

```text
[DATA] [ORIGEM]
- requisito:
- impacto tecnico:
- risco:
- aprovado por:
```
