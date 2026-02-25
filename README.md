# MEU GESTOR DE VODS (APP)

Este repositório contém **apenas o app desktop WPF** do MEU GESTOR DE VODS:

- interface do app (`app/`)
- instalador
- updater/manifests de update
- changelog e pipeline de release do app

## Separação APP x SAAS

O backend SaaS (API, Docker, deploy em VM, YouTube Live Manager 24/7) foi separado para um repositório dedicado:

- `https://github.com/wesleiandersonti/MGV-SAAS`

Essa separação mantém o ciclo de release do app independente da operação do backend em produção.

## Release atual do app

- Versão: `1.0.74`
- Tag: `v1.0.74`
