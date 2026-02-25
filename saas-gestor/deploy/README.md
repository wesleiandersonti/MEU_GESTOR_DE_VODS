# Deploy MGV

Guia rapido para deploy, rollback, staging e validacao operacional.

## Scripts

- `deploy/bootstrap-env.sh`
  - Gera `.env` automaticamente se ele nao existir.
  - Se `.env` ja existir, preserva configuracao atual.
  - Nao imprime segredos.

- `deploy/deploy-mgv.sh`
  - Atualiza codigo (`git pull --ff-only`).
  - Garante `.env` (`bootstrap-env.sh`).
  - Sobe/rebuilda `backend` + `youtube-live-manager`.
  - Executa health checks locais.

- `deploy/rollback-mgv.sh <tag>`
  - Lista tags disponiveis.
  - Derruba stack atual.
  - Faz checkout da tag informada.
  - Sobe stack novamente e valida health.

## Produção (VM MGV)

```bash
cd /opt/saas-gestor
chmod +x deploy/*.sh
./deploy/deploy-mgv.sh
```

Validacao pos-deploy:

```bash
docker compose ps
curl -fsS http://localhost:3000/api/v1/health
docker compose exec -T backend node -e "fetch('http://youtube-live-manager:8787/health').then(r=>r.ok?process.exit(0):process.exit(1))"
```

## Rollback

```bash
cd /opt/saas-gestor
./deploy/rollback-mgv.sh v1.0.74
```

## Staging

Subir ambiente paralelo (nao afeta producao):

```bash
cd /opt/saas-gestor
docker compose -f docker-compose.staging.yml up -d --build
docker compose -f docker-compose.staging.yml ps
curl -fsS http://localhost:3100/api/v1/health
```

Parar staging:

```bash
docker compose -f docker-compose.staging.yml down
```

## Release app 1.0.74

1) Garantir commits em `main`.

2) Tag + push:

```bash
git checkout main
git pull --ff-only
git tag v1.0.74
git push origin main
git push origin v1.0.74
```

3) Aguardar assets no GitHub Release e calcular SHA256 do instalador:

```bash
sha256sum MeuGestorVODs-Setup-v1.0.74.exe
```

4) Atualizar manifests:
- `app/update.json`
- `update.json`

5) Commit apenas de manifests:

```bash
git add app/update.json update.json
git commit -m "chore(release): publica updater do app 1.0.74 com sha256"
git push origin main
```

## Monitoramento basico

```bash
docker compose logs -f backend youtube-live-manager
curl -fsS http://localhost:3000/api/v1/health
docker compose exec -T backend node -e "fetch('http://youtube-live-manager:8787/health').then(r=>r.ok?process.exit(0):process.exit(1))"
```

Exemplo cron de uptime (opcional):

```cron
*/5 * * * * cd /opt/saas-gestor && ./deploy/deploy-mgv.sh >> /var/log/mgv-deploy-cron.log 2>&1
```

## Seguranca

- Nunca commite `.env`
- Mantenha backup seguro de `.env`
- Nao regenere `.env` em producao sem necessidade
