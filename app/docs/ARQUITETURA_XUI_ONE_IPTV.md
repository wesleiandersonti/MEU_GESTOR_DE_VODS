# Arquitetura XUI One para IPTV

Documento tecnico de referencia para orientar a evolucao do MEU GESTOR DE VODS com padrao operacional inspirado em XUI One.

---

## 1) O que e XUI One

XUI One e um painel de gerenciamento IPTV/OTT para administracao de:

- canais ao vivo
- VOD (filmes e series)
- usuarios e planos
- categorias e bouquets
- API de entrega de listas e streams

No ecossistema IPTV, ele funciona como backend central do servico.

---

## 2) Arquitetura geral (referencia)

### 2.1 Servidor principal

- painel web/admin para operacao
- API para apps e players
- banco MariaDB para usuarios, conteudo, categorias e estatisticas
- servicos internos para autenticacao, emissao de listas e metadados

### 2.2 Load balancers

- distribuicao de streams
- alta disponibilidade
- reducao de sobrecarga no servidor central

### 2.3 Servidores de streaming/transcodificacao

- entrega de live e VOD
- transcodificacao de bitrate/resolucao
- cache para reduzir latencia e banda

### 2.4 Camada de dados

- usuarios e credenciais
- pacotes/bouquets liberados
- relacao entre canais, VOD, categorias e pacotes

---

## 3) Modelo de organizacao de conteudo

### 3.1 Estrutura logica principal

1. Bouquets (categorias)
2. Canais ao vivo
3. Series
4. Filmes (VOD)

### 3.2 Regras de organizacao

- cada bouquet representa uma categoria (ex.: Esportes, Filmes, Infantil, Documentarios)
- canais ao vivo devem ser organizados por:
  - categoria (bouquet)
  - emissora
  - nome do canal
- VOD deve ficar separado de canais ao vivo

---

## 4) Gestao de VOD (4 tipos)

Para padrao de dados e escalabilidade, usar 4 tipos de entidade VOD:

1. Filme
2. Serie
3. Temporada
4. Episodio

Para cada VOD:

- verificar se ja esta salvo localmente
- exibir status na listagem:
  - `VOD ja salvo`
  - `VOD nao salvo`
- manter preparado para automacao de download/registro em fases futuras

---

## 5) Gestao de canais ao vivo

### 5.1 Regras

- organizar por categoria, emissora e nome
- verificar se URL ja existe
- quando URL nova:
  - salvar automaticamente em TXT de canais ao vivo

### 5.2 Arquivo dedicado de canais ao vivo

Nome recomendado de referencia:

- `canais_ao_vivo.txt`

Formato recomendado:

```text
[Bouquet] Esportes
  [Emissora] ESPN
    ESPN HD|http://servidor/live/espn_hd.m3u8
```

Observacao de compatibilidade com estado atual do app:

- atualmente existe `banco_canais_ao_vivo.txt` no produto
- manter compatibilidade com ambos durante fase de migracao

---

## 6) Saida esperada do programa

O sistema deve informar de forma clara:

- quais VODs ja estao salvos
- quais VODs nao estao salvos
- quais canais ao vivo ja possuem URL registrada
- quais URLs de canais ao vivo foram adicionadas recentemente

---

## 7) Compatibilidade IPTV/M3U

O modelo precisa suportar exportacao M3U:

- `#EXTINF` com `group-title`
- nome do canal/conteudo
- URL do stream

Exemplo:

```text
#EXTINF:-1 group-title="Esportes",ESPN HD
http://servidor/live/espn_hd.m3u8
```

---

## 8) Fluxo operacional de plataforma (XUI-style)

1. Instalacao base (Ubuntu + MariaDB + dependencias)
2. Configuracao de rede, SSL e seguranca
3. Definicao de balanceadores
4. Cadastro/importacao de canais live
5. Cadastro/importacao de VOD (filmes/series)
6. Criacao de categorias/bouquets/pacotes
7. Criacao de usuarios e planos
8. Entrega via API + playback em servidores de stream

---

## 9) Requisitos de infraestrutura (producao)

- CPU: 6+ cores
- RAM: 16-32 GB
- Storage: SSD/NVMe
- Rede: 1 Gbps ou superior
- SO recomendado: Ubuntu 20.04+

---

## 10) Mapeamento para o MEU GESTOR DE VODS

### 10.1 Ja implementado

- leitura de lista M3U por URL
- separacao TXT de VOD e canais ao vivo
- deduplicacao por URL
- estrutura automatica de pastas para download

### 10.2 Proximos passos de arquitetura

1. Separar `Services.cs` em camadas (Domain/Application/Infrastructure)
2. Modelar entidades de bouquet, emissora, canal live, filme, serie, temporada, episodio
3. Implementar arquivo `canais_ao_vivo.txt` em formato hierarquico
4. Exibir status de VOD salvo/nao salvo diretamente na grade
5. Exportador M3U dedicado por categoria e por pacote

---

## 11) Resumo executivo

| Componente | Funcao |
|---|---|
| Painel XUI One | Gerenciar usuarios, canais, VOD e pacotes |
| MariaDB | Persistir organizacao e relacionamentos |
| Load balancer | Distribuir carga e aumentar disponibilidade |
| Streaming nodes | Entregar live e VOD para clientes |
| Categorias/Bouquets | Organizar canais, filmes e series |
