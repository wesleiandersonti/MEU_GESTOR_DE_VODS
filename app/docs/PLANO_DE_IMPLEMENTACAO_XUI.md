# Plano de Implementacao - Evolucao XUI One

Plano oficial de execucao para evoluir o MEU GESTOR DE VODS para arquitetura escalavel inspirada em XUI One.

---

## 1) Objetivo

Transformar o app atual em uma base profissional de gestao IPTV com:

- modelagem consistente de Live + VOD
- organizacao por bouquets/categorias e emissoras
- persistencia confiavel (TXT + futura base relacional)
- exportacao M3U e preparo para integracao com backend XUI-style

---

## 2) Premissas

- manter funcionamento atual durante toda a evolucao
- evitar quebra de compatibilidade com arquivos TXT existentes
- cada fase deve entregar valor funcional mensuravel
- build CI deve ficar verde em todas as etapas

---

## 3) Fases do roadmap

## Fase 0 - Baseline e congelamento tecnico

Escopo:

- consolidar documentacao de base
- padronizar nomes de arquivos e contratos TXT
- registrar estado atual em release estavel

Entrega:

- docs publicados
- checklist de baseline aprovado

DoD:

- build local e CI verdes
- documentos de referencia atualizados

---

## Fase 1 - Refatoracao estrutural do codigo

Escopo:

- quebrar `Services.cs` em arquivos por responsabilidade
- introduzir estrutura por camadas:
  - `Domain`
  - `Application`
  - `Infrastructure`
  - `UI`

Entrega:

- mesma funcionalidade atual com arquitetura limpa

DoD:

- zero regressao funcional
- classes com responsabilidade unica

---

## Fase 2 - Modelo de dados operacional

Escopo:

- aplicar entidades descritas em `docs/MODELO_DE_DADOS_XUI.md`
- criar repositorio interno para dedupe e consulta de status
- manter modo TXT como fonte oficial inicial

Entrega:

- status claro de VOD salvo/nao salvo
- status claro de URL live conhecida/nova

DoD:

- dedupe por URL validado
- saidas de status validadas na interface

---

## Fase 3 - Organizacao de canais por bouquet/emissora

Escopo:

- classificador de categorias (bouquet)
- normalizacao de emissora
- visualizacao por agrupamento

Entrega:

- grade e filtros com agrupamento por categoria e emissora

DoD:

- canais organizados por bouquet > emissora > nome

---

## Fase 4 - Banco TXT hierarquico de canais ao vivo

Escopo:

- criar `canais_ao_vivo.txt` com estrutura hierarquica
- manter compatibilidade com `banco_canais_ao_vivo.txt`
- implementar estrategia de migracao sem perda

Entrega:

- exportador e leitor para os dois formatos (transitorio)

DoD:

- novo arquivo gerado automaticamente
- leitura e escrita consistentes

---

## Fase 5 - Gestao VOD (4 tipos)

Escopo:

- distinguir filme, serie, temporada e episodio
- registrar estado local por item
- preparar automacao futura de download por status

Entrega:

- listagem com status VOD ja salvo / nao salvo

DoD:

- validacao por amostra real de listas M3U

---

## Fase 6 - Exportacao M3U e integracao backend

Escopo:

- exportador M3U por bouquet/pacote
- contrato de dados para API futura de painel
- endpoints locais (ou services) para integracao externa

Entrega:

- arquivos M3U compativeis com players
- dados prontos para ingestao em backend XUI-style

DoD:

- arquivos M3U validos
- metadados corretos (`group-title`, nome, URL)

---

## Fase 7 - Persistencia relacional (opcional, recomendada)

Escopo:

- introduzir MariaDB/SQLite como camada oficial
- implementar dual-write (TXT + DB) por fase
- migracao controlada e reversivel

Entrega:

- consulta rapida, auditoria e historico de importacao

DoD:

- sem perda de compatibilidade com TXT
- rotinas de backup e rollback definidas

---

## 4) Cronograma sugerido

- Sprint 1: Fase 1 + Fase 2
- Sprint 2: Fase 3 + Fase 4
- Sprint 3: Fase 5 + Fase 6
- Sprint 4: Fase 7 + hardening

Obs: ajustar conforme capacidade e prioridade de release.

---

## 5) Qualidade e testes

Checklist minimo por feature:

- teste de importacao M3U real
- teste de dedupe de URL
- teste de classificacao (live vs vod)
- teste de escrita/leitura de TXT
- teste de build CI

Recomendado:

- testes unitarios para parser e classificadores
- testes de regressao com amostras de playlists

---

## 6) Estrategia de release

- branch principal: `main`
- espelho operacional: `master`
- tag semantica: `vX.Y.Z`
- assets por release:
  - setup `.exe`
  - pacote portatil `.zip`

---

## 7) Riscos e mitigacoes

Risco: classificacao incorreta de itens.

- mitigacao: regras configuraveis e fallback para `Outros`

Risco: duplicidade de links por variacoes de URL.

- mitigacao: normalizacao e dedupe por URL canonica

Risco: regressao em funcionalidades atuais.

- mitigacao: fases pequenas + validacao manual + CI obrigatorio

---

## 8) Criterios de sucesso

Projeto e considerado alinhado a arquitetura XUI-style quando:

- live e VOD estao segregados e auditaveis
- canais estao organizados por bouquet/emissora/nome
- VOD mostra status salvo/nao salvo de forma clara
- URLs novas de live sao detectadas e registradas automaticamente
- exportacao M3U funciona com metadados corretos
- documentacao e codigo permanecem sincronizados
