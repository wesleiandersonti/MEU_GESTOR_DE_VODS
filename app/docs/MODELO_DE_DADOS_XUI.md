# Modelo de Dados XUI - Base de Referencia

Este documento define o modelo de dados alvo para o MEU GESTOR DE VODS com padrao de operacao inspirado em XUI One.

Objetivo: permitir evolucao de um app local baseado em TXT para uma arquitetura escalavel com persistencia estruturada, compativel com IPTV/M3U e integracao futura com painel XUI.

---

## 1) Principios de modelagem

- separar Live TV de VOD
- representar hierarquia de series (serie > temporada > episodio)
- permitir organizacao por bouquets/categorias e emissoras
- manter deduplicacao por URL e por origem
- suportar exportacao M3U e sincronizacao com backend
- manter compatibilidade com os TXT atuais durante migracao

---

## 2) Entidades principais

### 2.1 Catalogo e organizacao

- `ProviderSource`: origem da lista (nome, url, tipo, status)
- `Bouquet`: categoria principal (ex.: Esportes, Filmes, Infantil)
- `Broadcaster`: emissora/canal de marca (ex.: ESPN, HBO)
- `LiveChannel`: canal ao vivo

### 2.2 VOD

- `VodMovie`: filme
- `VodSeries`: serie
- `VodSeason`: temporada
- `VodEpisode`: episodio

### 2.3 Midia e stream

- `MediaSource`: URL/arquivo de origem de stream
- `MediaQuality`: metadados de qualidade (resolucao, codec, bitrate)

### 2.4 Usuario e pacote (roadmap)

- `UserAccount`
- `PackagePlan`
- `UserEntitlement` (relacao usuario x bouquet/pacote)

### 2.5 Operacao e auditoria

- `ImportJob`: historico de carga de playlist
- `SyncJob`: historico de sincronizacao com XUI/backends
- `ChangeLog`: trilha de alteracoes

---

## 3) Relacionamentos (alto nivel)

- `ProviderSource` 1:N `ImportJob`
- `Bouquet` 1:N `LiveChannel`
- `Broadcaster` 1:N `LiveChannel`
- `Bouquet` 1:N `VodMovie`
- `Bouquet` 1:N `VodSeries`
- `VodSeries` 1:N `VodSeason`
- `VodSeason` 1:N `VodEpisode`
- `LiveChannel` 1:N `MediaSource`
- `VodMovie` 1:N `MediaSource`
- `VodEpisode` 1:N `MediaSource`

---

## 4) Contrato de dados para Live e VOD

### 4.1 LiveChannel (minimo)

- `id` (PK)
- `provider_source_id` (FK)
- `bouquet_id` (FK)
- `broadcaster_id` (FK, opcional)
- `name`
- `tvg_id`
- `tvg_logo`
- `group_title`
- `stream_url`
- `is_active`
- `created_at`, `updated_at`

### 4.2 VodMovie (minimo)

- `id` (PK)
- `provider_source_id` (FK)
- `bouquet_id` (FK)
- `title`
- `description`
- `poster_url`
- `vod_url`
- `is_saved_local`
- `local_path`
- `created_at`, `updated_at`

### 4.3 VodSeries / Season / Episode (minimo)

`VodSeries`:

- `id`, `provider_source_id`, `bouquet_id`, `title`, `description`, `poster_url`

`VodSeason`:

- `id`, `series_id`, `season_number`, `name`

`VodEpisode`:

- `id`, `season_id`, `episode_number`, `title`, `episode_url`, `is_saved_local`, `local_path`

---

## 5) Dicionario de estados

### 5.1 VOD

- `SAVED_LOCAL`: VOD ja salvo
- `NOT_SAVED_LOCAL`: VOD nao salvo
- `DOWNLOAD_PENDING`: pronto para automacao de download

### 5.2 URL live

- `LIVE_URL_KNOWN`: URL ja registrada
- `LIVE_URL_NEW`: URL adicionada recentemente

---

## 6) Regras de deduplicacao

- chave primaria de dedupe Live: `normalize(stream_url)`
- chave primaria de dedupe VOD: `normalize(vod_url ou episode_url)`
- dedupe secundario: `provider + normalized_name + normalized_group`
- ao importar, nunca apagar automaticamente dados antigos sem flag de expiracao

---

## 7) Compatibilidade com TXT atual (modo transitorio)

Arquivos atuais:

- `estrutura_downloads.txt`
- `banco_vod_links.txt`
- `banco_canais_ao_vivo.txt`

Mapeamento:

- `banco_vod_links.txt` -> `VodMovie` / `VodEpisode` (campos: Nome|Grupo|URL)
- `banco_canais_ao_vivo.txt` -> `LiveChannel` (campos: Nome|Grupo|URL)

Recomendacao:

- manter dual-write (TXT + DB) ate concluir migracao

---

## 8) Estrutura recomendada em MariaDB (fase backend)

Tabelas base:

- `provider_sources`
- `bouquets`
- `broadcasters`
- `live_channels`
- `vod_movies`
- `vod_series`
- `vod_seasons`
- `vod_episodes`
- `media_sources`
- `import_jobs`
- `sync_jobs`

Indices criticos:

- `idx_live_stream_url` em `live_channels(stream_url)`
- `idx_vod_url` em `vod_movies(vod_url)`
- `idx_episode_url` em `vod_episodes(episode_url)`
- `idx_group_title` em colunas de grupo/categoria

---

## 9) Exportacao M3U (contrato)

Formato minimo por item live:

```text
#EXTINF:-1 tvg-id="{tvg_id}" tvg-logo="{logo}" group-title="{bouquet}",{name}
{stream_url}
```

Formato minimo por item VOD:

```text
#EXTINF:-1 group-title="{categoria}",{titulo}
{vod_url}
```

---

## 10) Metadados obrigatorios para XUI-style

- origem (`provider_source`)
- tipo (`live`, `movie`, `series`, `episode`)
- categoria/bouquet
- url canonica
- status de disponibilidade local
- timestamps de importacao/atualizacao

---

## 11) Criterios de aceitacao do modelo

O modelo esta aprovado quando:

- permite separar live e VOD sem ambiguidades
- suporta hierarquia completa de series
- deduplica URLs de forma deterministica
- suporta exportacao M3U sem perda de dados
- permite migrar de TXT para DB sem regressao funcional
