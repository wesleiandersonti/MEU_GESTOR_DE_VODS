# YouTube Live Manager (24/7)

Sistema completo para detectar, validar e exportar apenas canais YouTube Live ativos, com integracao MariaDB (XUI-One) e API REST protegida por API key.

## Stack escolhida

- Linguagem: Node.js + TypeScript
- API: Express
- Banco: MariaDB (`mysql2`)
- Resolver de stream: `yt-dlp` (obrigatorio)
- Scanner 24/7: fila em memoria + concorrencia + retries/backoff + circuit breaker

## Estrutura obrigatoria

```text
/src
  /config
  /db
    /connection
    /migrations
    /repositories
  /core
    liveDetector
    streamResolver
    validator
    scanner
    exporter
  /api
    /routes
    /controllers
    /middleware
  /utils
  main.ts

/output
  active_channels.m3u
```

## Requisitos (Ubuntu 22.04)

```bash
sudo apt update
sudo apt install -y curl python3 python3-pip ffmpeg
sudo pip3 install -U yt-dlp
yt-dlp --version
```

Node.js 20+ recomendado.

## Instalacao

```bash
cd saas-gestor/youtube-live-manager
cp .env.example .env
npm install
```

## Variaveis de ambiente

Arquivo base: `.env.example`.

Minimas para subir:

- `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASS`, `DB_NAME`
- `API_KEY`
- `YTDLP_PATH`
- `SCAN_INTERVAL_SEC`, `SCAN_CONCURRENCY`
- `SEED_DEFAULT_CHANNELS` (`true` para importar automaticamente o catalogo padrao na inicializacao)

## Migrations SQL (MariaDB)

Arquivo: `src/db/migrations/001_init.sql`

Cria (se nao existir):

- `yt_channels`
- `yt_channel_status`
- `yt_checks_history`
- `yt_exports`

Executar migration:

```bash
npm run migrate
```

Observacao: o sistema usa tabelas proprias e nao altera tabelas internas do XUI-One.

## Rodando em desenvolvimento

```bash
npm run dev
```

## Rodando em producao

```bash
npm run build
npm start
```

## Scanner 24/7

- Auto start por `SCANNER_AUTOSTART=true`
- Controle por API:
  - `GET /scanner/status`
  - `POST /scanner/start`
  - `POST /scanner/stop`
  - `POST /scanner/run-once`

Fluxo por canal:

1. Detecta live ativa no `/live` via `yt-dlp`.
2. Se live ativa, resolve URL de stream (`-g`) via `yt-dlp`.
3. Valida stream (`HEAD/GET`, assinatura de playlist m3u8/mpd).
4. Atualiza status atual + salva historico.
5. Em bloqueio (429/captcha), marca `BLOCKED` e aplica cooldown com circuit breaker.

## API REST

Header obrigatorio (exceto `/health`; `/stream/:id` aceita API key ou IP whitelist):

```text
X-API-Key: <API_KEY>
```

### 1) Health

```bash
curl http://127.0.0.1:8085/health
```

### 2) Canais

Listar:

```bash
curl -H "X-API-Key: $API_KEY" "http://127.0.0.1:8085/channels?page=1&limit=20&is_online=true"
```

Criar:

```bash
curl -X POST http://127.0.0.1:8085/channels   -H "Content-Type: application/json"   -H "X-API-Key: $API_KEY"   -d '{
    "name": "CNN Brasil",
    "category": "NOTICIAS_BRASIL",
    "channel_url": "https://www.youtube.com/@CNNBrasil",
    "enabled": true
  }'
```

Atualizar:

```bash
curl -X PUT http://127.0.0.1:8085/channels/1   -H "Content-Type: application/json"   -H "X-API-Key: $API_KEY"   -d '{"enabled": false}'
```

Desativar (soft delete):

```bash
curl -X DELETE -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/channels/1
```

Importar catalogo Brasil + Global pronto (idempotente):

```bash
curl -X POST -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/channels/catalog/import
```

Consultar categorias, bouquets e usuarios do catalogo:

```bash
curl -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/channels/catalog
```

### 3) Status e historico

```bash
curl -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/channels/1/status
curl -H "X-API-Key: $API_KEY" "http://127.0.0.1:8085/channels/1/history?limit=50"
```

### 4) Exportacao M3U

Gerar:

```bash
curl -X POST -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/export/m3u
```

Ultimo M3U:

```bash
curl -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/export/m3u/latest
curl -H "X-API-Key: $API_KEY" -OJ http://127.0.0.1:8085/export/m3u/latest/download
```

### 5) Scanner control

```bash
curl -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/scanner/status
curl -X POST -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/scanner/start
curl -X POST -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/scanner/stop
curl -X POST -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/scanner/run-once
```

### 6) URL proxy de stream

```bash
curl -L -H "X-API-Key: $API_KEY" http://127.0.0.1:8085/stream/1
```

Se o stream resolvido estiver expirado, o endpoint resolve on-demand e redireciona (`302`) para a URL final.

## Exemplo de payloads/respostas

### POST /channels (request)

```json
{
  "name": "Record News",
  "category": "NOTICIAS_BRASIL",
  "channel_url": "https://www.youtube.com/@recordnews",
  "enabled": true
}
```

### POST /channels (response)

```json
{
  "ok": true,
  "item": {
    "id": 7,
    "name": "Record News",
    "category": "NOTICIAS_BRASIL",
    "channelUrl": "https://www.youtube.com/@recordnews",
    "liveUrl": "https://www.youtube.com/@recordnews/live",
    "enabled": true,
    "createdAt": "2026-02-17T15:00:00.000Z",
    "updatedAt": "2026-02-17T15:00:00.000Z"
  }
}
```

### GET /channels/7/status (response)

```json
{
  "ok": true,
  "channel": {
    "id": 7,
    "name": "Record News",
    "category": "NOTICIAS_BRASIL",
    "channelUrl": "https://www.youtube.com/@recordnews",
    "liveUrl": "https://www.youtube.com/@recordnews/live",
    "enabled": true,
    "createdAt": "2026-02-17T15:00:00.000Z",
    "updatedAt": "2026-02-17T15:00:00.000Z"
  },
  "status": {
    "channelId": 7,
    "isLive": true,
    "isOnline": true,
    "liveVideoId": "abcd1234",
    "streamUrl": "https://manifest.googlevideo.com/api/manifest/hls_playlist/...",
    "format": "HLS",
    "lastHttpCode": 200,
    "lastCheckedAt": "2026-02-17T15:05:03.000Z",
    "errorCode": null,
    "errorMessage": null
  }
}
```

## Exemplo de M3U final

```m3u
#EXTM3U
#EXTINF:-1 group-title="NOTICIAS_BRASIL",CNN Brasil (YT Live)
http://127.0.0.1:8085/stream/1
#EXTINF:-1 group-title="NOTICIAS_BRASIL",Record News (YT Live)
http://127.0.0.1:8085/stream/7
```

## Integracao com aba YouTube_para_m3u no app desktop

No desktop, a aba atual gera M3U a partir de links manuais. Este servico adiciona:

- deteccao de live ativa real,
- validacao online antes de exportar,
- historico/metricas,
- endpoint proxy para stream expiravel,
- operacao 24/7 para alimentar painel externo/XUI-SAAS.

Basta consumir os endpoints deste servico pela aba para substituir a geracao estatica por uma lista dinamica apenas com canais ativos.
