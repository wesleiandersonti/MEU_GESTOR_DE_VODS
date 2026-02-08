# 📝 CHANGELOG - MEU GESTOR DE VODS

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.

## 📋 Sobre Versionamento

Este projeto segue **[Semantic Versioning (SemVer)](https://semver.org/lang/pt-BR/)**:

```
MAJOR.MINOR.PATCH
│ │ │
│ │ └─ PATCH: Correções de bugs (1.0.0 → 1.0.1)
│ └── MINOR: Novas funcionalidades (1.0.99 → 1.1.0)
└── MAJOR: Mudanças incompatíveis (1.x.x → 2.0.0)
```

**IMPORTANTE:** Não existe limite de 99! Pode ter:
- ✅ 1.0.150 (correções)
- ✅ 1.0.999 (correções)
- ✅ 1.15.0 (funcionalidades)

**Só mude MINOR quando adicionar NOVA FUNCIONALIDADE, não por causa do número!**

📖 Veja o guia completo em: `docs/VERSIONAMENTO.md`

---

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere a [Semantic Versioning](https://semver.org/lang/pt-BR/).

---

## [1.0.21] - 2026-02-08

### 🎉 Adicionado (Features)
- **Barra de título customizada com controles de janela**
  - Janela agora usa `WindowChrome` com botões internos de minimizar, maximizar/restaurar e fechar.
  - Suporte a arrastar janela pelo cabeçalho e duplo clique para maximizar/restaurar.
- **Alternância de tema no cabeçalho**
  - Botão `Tema` com opções: Claro, Escuro e Sistema.
  - Aplicação dinâmica de brushes para fundo, painéis, status e cabeçalho.

### 🔄 Melhorado (UX/UI)
- **Tooltips funcionais em todos os botões principais**
  - Explicação de funcionalidade ao passar o mouse.
- **Drop de lista local mais confiável**
  - Ajuste de `Drop` com `Handled` e fallback para arquivos existentes de apps externos.

---

## [1.0.21] - 2026-02-08

### 🎉 Adicionado (Features)
- **Barra de título customizada (WindowChrome)**
  - Botões internos de minimizar, maximizar/restaurar e fechar.
  - Arrastar janela pelo cabeçalho e duplo clique para maximizar/restaurar.
- **Seletor de tema no cabeçalho**
  - Tema Claro, Escuro e Sistema.
  - Aplicação dinâmica de cores em fundo, painéis, status e cabeçalho.

### 🔄 Melhorado (UX/UI)
- Tooltips descritivos nos botões principais para facilitar uso.
- Ajustes no drag-and-drop de arquivo local para aceitar melhor payloads externos.

---

## [1.0.20] - 2026-02-08

### 🎉 Adicionado (Features)
- **Controles de painel no Monitoramento**
  - Botão `—` para minimizar e `□` para maximizar o painel de monitoramento sem abrir nova janela.
  - Layout interno com alternância entre normal/minimizado/maximizado.

### 🔄 Melhorado (UX/UI)
- **Estatísticas do Banco de Dados com visual alinhado**
  - Janela dedicada com total em destaque, grade de Top 10 categorias e colunas alinhadas.
  - Quantidades formatadas e alinhadas à direita para leitura rápida.
- **Drag-and-drop da lista local mais robusto**
  - Drop funciona em toda a área, inclusive no campo interno.
  - Suporte ampliado para playlists VLC/IPTV: `.m3u`, `.m3u8`, `.txt`, `.xspf`, `.pls`, `.asx`, `.wpl`, `.zpl`, `.vlc`, `.url`.
  - Fallback para extração de URLs HTTP em playlists não-M3U.

---

## [1.0.19] - 2026-02-08

### 🎉 Adicionado (Features)
- **Arrastar e soltar para lista local**
  - Área de `Arquivo Local` agora aceita drag-and-drop de `.m3u`, `.m3u8` e `.txt`.
  - Placeholder visual "Arraste e solte sua lista aqui..." quando nenhum arquivo está selecionado.
  - Validação de extensão no drop com aviso para arquivos inválidos.
- **Feedback visual no hover de drop**
  - Destaque de borda azul e fundo azul claro ao arrastar arquivo válido sobre a área.

### 🔄 Alterado (Changes)
- Botões do rodapé renomeados para:
  - `Baixar txt VODs`
  - `Baixar txt Canais`
- Exportação desses botões agora gera playlist `.m3u` pronta para VLC, com validação de compatibilidade (`#EXTM3U`, pares `#EXTINF + URL`, URLs válidas).

---

## [1.0.18] - 2026-02-08

### 🎉 Adicionado (Features)
- **IPTV Checker completo no botão Analisar Link**
  - Verificação de conectividade em massa (HEAD/GET parcial) sem reproduzir stream.
  - Status em tempo real por item: `Checking`, `ONLINE`, `OFFLINE`.
  - Filtros de resultado: Todos, ONLINE, OFFLINE e Duplicados.
  - Barra de progresso e contadores de análise.
- **Score de qualidade por servidor (0-100)**
  - Cálculo por host com taxa de sucesso, latência média e tempo de resposta.
  - Classificação: `Excelente`, `Bom`, `Regular`, `Ruim` com painel dedicado.
- **Detecção e tratamento de links duplicados**
  - Normalização de URL e marcação de duplicados.
  - Remoção em lote de duplicados.
  - Exportação M3U: apenas ONLINE, sem duplicados, ou limpo.
- **Persistência de logs e snapshots no SQLite**
  - Nova tabela `StreamCheckLog` para histórico de checagens.
  - Nova tabela `ServerScoreSnapshot` para histórico de score por servidor.

### 🔄 Alterado (Changes)
- Mantido fluxo de reprodução via VLC externo (sem player local interno).

---

## [1.0.17] - 2026-02-08

### 🐛 Corrigido (Fixes)
- Corrige erro de build no XAML da lista de Downloads (`StringFormat` inválido em `MainWindow.xaml`).
- Build volta a compilar normalmente no GitHub Actions.

---

## [1.0.16] - 2026-02-08

### 🔄 Alterado (Changes)
- Revertida a reprodução local e retomado o fluxo estável de reprodução via VLC externo.
- Removidos arquivos e dependências do player local (LibVLCSharp).

---

## [1.0.15] - 2026-02-08

### 🎉 Adicionado (Features)
- **Player de vídeo local com LibVLCSharp**
  - Janela popup com player profissional
  - Suporte a HLS (m3u8), DASH, RTMP, RTSP, MP4, MPEG-TS
  - Controles: Play/Pause, Stop, Volume
  - Painel técnico mostrando:
    - 📺 Nome do canal/filme/série
    - 🌐 Servidor (hostname extraído da URL)
    - ⚡ Latência (medida via Ping em tempo real)
    - 🎞️ Formato do vídeo (detectado automaticamente)
  - Botão "▶ Reproduzir" em downloads concluídos
  - Interface escura moderna

---

## [1.0.14] - 2026-02-08

### 🐛 Corrigido (Fixes)
- **Título da janela atualizado dinamicamente**
  - Remove versão hardcoded "v1.0" do título
  - Adiciona propriedade WindowTitle com binding dinâmico
  - Título agora mostra versão correta: "MEU GESTOR DE VODS v1.0.14"

---

## [1.0.13] - 2026-02-08

### 🐛 Corrigido (Fixes)
- **Correção de layout da barra inferior**
  - Corrige botões esticados verticalmente ocupando espaço excessivo
  - Remove linha extra do Grid.RowDefinitions
  - Adiciona VerticalAlignment="Center" nos botões
  - Restaura textos completos dos botões
  - Corrige estrutura do Grid principal

---

## [1.0.12] - 2026-02-08

### 🐛 Corrigido (Fixes)
- **Correção de layout da interface**
  - Corrige sobreposição do Main Content com Status Bar
  - Reduz tamanho dos botões da barra inferior (menores e mais compactos)
  - Corrige Grid.Row dos elementos principais
  - Ajusta Loading Overlay para cobrir toda a interface

---

## [1.0.11] - 2026-02-08

### 🎉 Adicionado (Features)
- **Suporte a arquivos locais M3U/M3U8/TXT**
  - Nova linha "Arquivo Local" na interface
  - Botão "Procurar" (laranja) para selecionar arquivo
  - Botão "Analisar lista local" (verde, negrito) para carregar
  - Suporte a extensões: .m3u, .m3u8, .txt
  - Registra arquivo local no histórico de URLs

---

## [1.0.10] - 2026-02-08

### 🐛 Corrigido (Fixes)
- Erro de build: `Cannot await 'void'` no método LoadM3UUrlHistory
- Alterado método de `async void` para `async Task` para permitir await correto

---

## [1.0.9] - 2026-02-08

### 🎉 Adicionado (Features)

#### Banco de Dados SQLite
- **Implementação completa do SQLite** como banco de dados principal
- **Repository Pattern** com interfaces preparadas para migração futura (SaaS)
- **Tabelas criadas:**
  - `Entries` - Armazena canais e VODs
  - `DownloadHistory` - Histórico de downloads
  - `Favorites` - Sistema de favoritos
  - `M3uUrlHistory` - Histórico de URLs M3U testadas
  - `SchemaVersion` - Controle de versão do banco
- **Índices otimizados** para buscas rápidas

#### Histórico de URLs M3U
- **ComboBox editável** com histórico de URLs
- **Salvamento automático** de cada URL testada
- **Botão "Histórico" (azul)** mostra:
  - Total de URLs
  - URLs online/offline
  - Últimas 10 URLs com data e quantidade de itens
- **Botão "Limpar Offline" (vermelho)**:
  - Lista URLs fora do ar
  - Remove em lote com confirmação
  - Atualiza ComboBox automaticamente
- **Estatísticas de uso:**
  - Contador de sucessos/falhas
  - Última verificação
  - Quantidade de itens carregados

#### Migração de Dados
- **Detecção automática** de arquivos TXT legados
- **Migração assistida** com diálogo de confirmação
- **Preservação completa** de metadados
- **Conversão automática** para formato M3U

#### Scripts de Build
- `build_completo.bat` - Build completo em um comando
- `etapa1_restore.bat` - Restaurar pacotes NuGet
- `etapa2_build.bat` - Compilar projeto
- `etapa4_publish.bat` - Publicar Release
- `BUILD_INSTRUCTIONS.md` - Guia completo de build

### 🔄 Modificado (Changes)

#### Persistência
- **Antes:** Apenas arquivos TXT (formato pipe-delimited)
- **Agora:** SQLite principal + TXT como backup
- Formato dos arquivos TXT corrigido para M3U padrão

#### UI/UX
- Campo URL M3U: TextBox → **ComboBox editável**
- Nova barra de botões: "Histórico" e "Limpar Offline"
- Status bar atualizada com contador de URLs
- Botão "Estatísticas BD" mostra total de entradas

#### Arquitetura
- Implementação de **Repository Pattern**
- Serviços refatorados para usar interfaces
- Separação clara entre camadas (UI, Service, Repository, Data)

### 🐛 Corrigido (Fixes)
- Formato incorreto dos arquivos TXT de banco
- Propriedade read-only `TotalMigrated` em `MigrationResult`
- Campos não inicializados em `DownloadItem`
- Eventos `PropertyChanged` marcados como nullable

### 📚 Documentação
- `PROJECT_ARCHITECTURE.md` - Documentação completa da arquitetura
- `CHANGELOG.md` - Este arquivo
- Atualização do `README.md` com novas funcionalidades

---

## [1.0.8] - 2026-02-07

### 🎉 Adicionado
- Carregamento de listas M3U via URL
- Download de VODs com progresso
- Agrupamento por Categoria/Subcategoria (XUI One style)
- Painel de grupos com TreeView
- Filtros e busca em tempo real
- Seleção em lote (selecionar/desmarcar todos)
- Integração com VLC (reprodução direta)
- Verificação de atualizações automática via GitHub
- Rollback para versões anteriores
- Banco TXT para links VOD e canais ao vivo
- Botões para abrir bancos TXT no Notepad
- Atalhos rápidos: copiar URL, colar URL, verificar TXT

### 🔄 Modificado
- Refatoração do parser M3U
- Melhorias na interface de usuário
- Otimização de performance com virtualização

---

## [1.0.7] - 2026-02-06

### 🎉 Adicionado
- Sistema de atualização automática
- Integração com GitHub Releases
- Download de atualizações em segundo plano

---

## [1.0.0] - 2026-02-01

### 🎉 Lançamento Inicial
- Versão base do projeto
- Carregamento M3U simples
- Download básico de arquivos

---

## 🗺️ Roadmap

### [1.1.0] - Planejado
- [ ] Sincronização em nuvem (preparação SaaS)
- [ ] Suporte a múltiplos perfis de usuário
- [ ] Exportação para formatos adicionais
- [ ] Filtros avançados de busca

### [2.0.0] - Planejado (SaaS)
- [ ] Backend em PostgreSQL
- [ ] API RESTful
- [ ] Autenticação de usuários
- [ ] Sincronização multi-dispositivo
- [ ] Web player integrado

---

## 📊 Estatísticas do Projeto

### Versão 1.0.9
- **Linhas de código:** ~3.500
- **Arquivos:** 15+
- **Tabelas SQLite:** 5
- **Interfaces:** 4
- **Scripts de build:** 4

### Commits por Versão
- v1.0.9: 12 commits
- v1.0.8: 8 commits
- v1.0.7: 3 commits

---

## 🏆 Contribuidores

- **wesleiandersonti** - Desenvolvedor principal
- **timtester123** - Base do projeto original

---

## 📞 Links Úteis

- **Repositório:** https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS
- **Releases:** https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases
- **Issues:** https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/issues
- **Wiki:** (em breve)

---

**Nota:** Para ver a lista completa de alterações, consulte o histórico de commits no GitHub.
