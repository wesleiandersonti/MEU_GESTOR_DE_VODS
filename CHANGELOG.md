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
