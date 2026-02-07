# Plano Tecnico por Arquivo e Classe

Documento operacional para executar a evolucao do MEU GESTOR DE VODS com foco em arquitetura XUI-style, sem perder compatibilidade com o comportamento atual.

Este plano foi feito para guiar implementacao por etapas com baixo risco.

---

## 1) Escopo deste plano

Cobrir, de forma pratica e implementavel:

- separacao de Live TV, Filmes e Series
- organizacao por Bouquet > Emissora > Canal
- status de VOD salvo/nao salvo
- dedupe de URL live e VOD
- persistencia TXT atual + evolucao para formato hierarquico
- preparo para exportacao M3U e integracao backend

---

## 2) Mapa tecnico atual (as-is)

Arquivos centrais hoje:

- `MainWindow.xaml` (UI)
- `MainWindow.xaml.cs` (orquestracao + regras + IO + update)
- `Services.cs` (modelo + parser + download)

Pontos de atencao:

- alto acoplamento no code-behind (`MainWindow.xaml.cs` grande)
- logica de dominio misturada com UI
- `M3UEntry` ainda sem tipagem forte para Live/VOD
- persistencia TXT funcional, mas sem abstração de repositorio

---

## 3) Estrutura alvo (to-be)

Estrutura recomendada dentro do mesmo projeto .NET (sem criar solucao nova por enquanto):

```text
.
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   ├── ValueObjects/
│   └── Rules/
├── Application/
│   ├── Contracts/
│   ├── DTOs/
│   ├── UseCases/
│   └── Services/
├── Infrastructure/
│   ├── Parsing/
│   ├── Classification/
│   ├── Storage/
│   │   └── Txt/
│   ├── Export/
│   ├── Playback/
│   └── Updates/
├── Presentation/
│   ├── ViewModels/
│   ├── Views/
│   └── Dialogs/
├── MainWindow.xaml
├── MainWindow.xaml.cs
└── Services.cs (fase transitoria, depois removido)
```

---

## 4) Plano por fase (arquivos e classes)

## Fase 1 - Fundacao e separacao de camadas

Objetivo: tirar regra de negocio do code-behind sem quebrar fluxo atual.

### Arquivos a criar

- `Domain/Entities/PlaylistItem.cs`
- `Domain/Enums/PlaylistItemType.cs` (`LiveChannel`, `VodMovie`, `VodSeries`, `VodEpisode`, `Unknown`)
- `Application/Contracts/IPlaylistImporter.cs`
- `Application/Contracts/ILinkDatabaseRepository.cs`
- `Application/Contracts/IDownloadStructureRepository.cs`
- `Infrastructure/Parsing/M3UPlaylistImporter.cs`

### Classes e responsabilidade

- `PlaylistItem`:
  - representa item canonico de playlist
  - campos: `Name`, `GroupTitle`, `Url`, `TvgId`, `Logo`, `Type`, `Bouquet`, `Broadcaster`
- `M3UPlaylistImporter`:
  - parse da playlist
  - retorna `List<PlaylistItem>`

### Alteracoes previstas

- adaptar `MainWindow.xaml.cs` para chamar `IPlaylistImporter`
- manter `M3UEntry` apenas transitorio

---

## Fase 2 - Classificacao XUI-style

Objetivo: aplicar estrutura Bouquet > Emissora > Nome e separacao Live x VOD.

### Arquivos a criar

- `Domain/Entities/Bouquet.cs`
- `Domain/Entities/Broadcaster.cs`
- `Domain/Entities/LiveChannel.cs`
- `Domain/Entities/VodMovie.cs`
- `Domain/Entities/VodSeries.cs`
- `Domain/Entities/VodSeason.cs`
- `Domain/Entities/VodEpisode.cs`
- `Application/Services/ContentClassificationService.cs`
- `Domain/Rules/ClassificationRules.cs`
- `Infrastructure/Classification/DefaultClassificationRulesProvider.cs`

### Classes e responsabilidade

- `ContentClassificationService`:
  - identifica tipo do conteudo
  - resolve bouquet e emissora
  - determina VOD type (filme/serie/episodio)

---

## Fase 3 - Persistencia TXT profissional

Objetivo: formalizar bancos TXT com repositorios e dedupe.

### Arquivos a criar

- `Infrastructure/Storage/Txt/TxtFilePaths.cs`
- `Infrastructure/Storage/Txt/VodLinksTxtRepository.cs`
- `Infrastructure/Storage/Txt/LiveLinksTxtRepository.cs`
- `Infrastructure/Storage/Txt/LiveHierarchicalTxtRepository.cs`
- `Infrastructure/Storage/Txt/DownloadStructureTxtRepository.cs`
- `Application/UseCases/SyncTxtDatabasesUseCase.cs`

### Regras

- manter arquivos atuais:
  - `banco_vod_links.txt`
  - `banco_canais_ao_vivo.txt`
- criar arquivo novo hierarquico:
  - `canais_ao_vivo.txt`

### Formato alvo de `canais_ao_vivo.txt`

```text
[Bouquet] Esportes
  [Emissora] ESPN
    ESPN HD|http://exemplo/live/espn.m3u8
```

### Resultado esperado da fase

- lista de URLs live novas adicionadas
- lista de URLs live ja existentes
- lista de VODs salvos/nao salvos

---

## Fase 4 - Estado de VOD na listagem

Objetivo: exibir claramente status VOD salvo/nao salvo na UI.

### Arquivos a criar

- `Presentation/ViewModels/PlaylistItemViewModel.cs`
- `Presentation/ViewModels/MainViewModel.cs` (inicio da migracao do code-behind)

### Arquivos a alterar

- `MainWindow.xaml`
- `MainWindow.xaml.cs`

### Campos/colunas novos na grade

- `Tipo` (Live, Filme, Serie, Episodio)
- `Bouquet`
- `Emissora`
- `Status VOD` (`VOD ja salvo` / `VOD nao salvo`)
- `Status URL Live` (`URL registrada` / `URL nova`)

---

## Fase 5 - Saida consolidada de processamento

Objetivo: emitir resumo tecnico apos importacao.

### Arquivos a criar

- `Application/DTOs/ImportSummary.cs`
- `Application/UseCases/ImportPlaylistUseCase.cs`

### `ImportSummary` (minimo)

- `TotalItems`
- `TotalVod`
- `TotalLive`
- `VodSavedCount`
- `VodNotSavedCount`
- `LiveAlreadyRegisteredCount`
- `LiveNewAddedCount`

### Saida na UI

- barra de status com resumo curto
- opcional: dialog de resumo detalhado

---

## Fase 6 - Exportacao M3U

Objetivo: preparar dados para exportacao IPTV padrao.

### Arquivos a criar

- `Infrastructure/Export/M3UExportService.cs`
- `Application/UseCases/ExportM3UUseCase.cs`

### Regras de export

- gerar `#EXTINF` com `group-title`
- incluir nome e URL
- permitir export por:
  - bouquet
  - tipo (`Live`/`VOD`)
  - pacote futuro

---

## Fase 7 - Integracao backend XUI-style (preparo)

Objetivo: deixar pronto para sync com backend.

### Arquivos a criar

- `Application/Contracts/IBackendPublisher.cs`
- `Infrastructure/Updates/GitHubReleaseClient.cs` (separar da UI)
- `Infrastructure/Playback/VlcLauncher.cs`
- `Infrastructure/Backend/JsonBundleExporter.cs`

### Nota

Nesta fase ainda pode ser modo arquivo/JSON. API real de XUI entra depois.

---

## 5) Classes novas prioritarias (ordem de criacao)

1. `PlaylistItem`
2. `M3UPlaylistImporter`
3. `ContentClassificationService`
4. `VodLinksTxtRepository`
5. `LiveLinksTxtRepository`
6. `LiveHierarchicalTxtRepository`
7. `ImportPlaylistUseCase`
8. `ImportSummary`
9. `M3UExportService`

---

## 6) Plano de commits recomendado

Sequencia sugerida:

1. `refactor: introduce domain playlist item and importer contract`
2. `feat: add xui-style classification for live and vod`
3. `feat: add txt repositories and hierarchical live file`
4. `feat: show vod/local and live/url status in UI grid`
5. `feat: add import summary output`
6. `feat: add m3u export service`
7. `refactor: extract updater and vlc launcher from MainWindow`

---

## 7) Definicao de pronto por fase

Cada fase so fecha quando:

- compila local (`dotnet build`)
- build GitHub Actions verde
- fluxo principal validado manualmente
- documentacao atualizada (`README` + `docs`)

---

## 8) Checklist de execucao (tracking)

- [ ] Fase 1 concluida
- [ ] Fase 2 concluida
- [ ] Fase 3 concluida
- [ ] Fase 4 concluida
- [ ] Fase 5 concluida
- [ ] Fase 6 concluida
- [ ] Fase 7 concluida

---

## 9) Observacoes operacionais

- nao remover TXT legados antes da fase de migracao completa
- manter dual-write quando introduzir base relacional
- validar com playlists reais de operacao antes de release final
