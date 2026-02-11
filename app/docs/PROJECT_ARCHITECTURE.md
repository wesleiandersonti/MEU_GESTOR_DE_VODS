# 📋 ARQUITETURA DO SISTEMA - MEU GESTOR DE VODS

## 🎯 Visão Geral

Sistema desktop WPF para gerenciamento de playlists IPTV M3U com persistência em SQLite, preparado para migração futura para SaaS.

---

## 🏗️ Arquitetura em Camadas

```
┌─────────────────────────────────────────┐
│           UI Layer (WPF)                │
│  - MainWindow.xaml                      │
│  - DataBinding                          │
│  - Commands & Events                    │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────▼─────────────────────┐
│        Service Layer                    │
│  - M3UService                           │
│  - DownloadService                      │
│  - DatabaseService                      │
│  - MigrationService                     │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────▼─────────────────────┐
│      Repository Layer                   │
│  - IEntryRepository                     │
│  - IDownloadHistoryRepository          │
│  - IFavoriteRepository                  │
│  - IM3uUrlRepository                    │
└───────────────────┬─────────────────────┘
                    │
┌───────────────────▼─────────────────────┐
│      Data Layer (SQLite)                │
│  - database.sqlite                      │
│  - Cache em memória                     │
└─────────────────────────────────────────┘
```

---

## 💾 Banco de Dados SQLite

### Tabelas

#### 1. **Entries** - Canais e VODs
```sql
CREATE TABLE Entries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntryId TEXT UNIQUE NOT NULL,
    Name TEXT NOT NULL,
    Url TEXT UNIQUE NOT NULL,
    GroupTitle TEXT,
    Category TEXT,
    SubCategory TEXT,
    LogoUrl TEXT,
    TvgId TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

#### 2. **DownloadHistory** - Histórico de Downloads
```sql
CREATE TABLE DownloadHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntryUrl TEXT NOT NULL,
    EntryName TEXT NOT NULL,
    DownloadDate DATETIME DEFAULT CURRENT_TIMESTAMP,
    FilePath TEXT,
    FileSize INTEGER DEFAULT 0,
    Success BOOLEAN DEFAULT 1,
    ErrorMessage TEXT
);
```

#### 3. **Favorites** - Favoritos
```sql
CREATE TABLE Favorites (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EntryUrl TEXT UNIQUE NOT NULL,
    AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

#### 4. **M3uUrlHistory** - Histórico de URLs M3U ⭐ NOVO
```sql
CREATE TABLE M3uUrlHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Url TEXT UNIQUE NOT NULL,
    Name TEXT,
    IsOnline BOOLEAN DEFAULT 1,
    LastChecked DATETIME DEFAULT CURRENT_TIMESTAMP,
    EntryCount INTEGER DEFAULT 0,
    SuccessCount INTEGER DEFAULT 0,
    FailCount INTEGER DEFAULT 0,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

#### 5. **SchemaVersion** - Controle de Migrações
```sql
CREATE TABLE SchemaVersion (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    Version INTEGER NOT NULL DEFAULT 1,
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

---

## 🔧 Repository Pattern

### Interfaces Implementadas

#### IEntryRepository
- `ExistsByUrlAsync(string url)` - Verifica se entrada existe
- `AddAsync(M3UEntry entry)` - Adiciona entrada
- `AddRangeAsync(IEnumerable<M3UEntry>)` - Adiciona múltiplas
- `GetAllAsync()` - Lista todas
- `GetByCategoryAsync(string category)` - Filtra por categoria
- `SearchAsync(string term)` - Busca por termo
- `GetCountAsync()` - Conta total
- `DeleteByUrlAsync(string url)` - Remove por URL
- `GetVersionAsync()` - Versão do schema

#### IDownloadHistoryRepository
- `AddAsync(DownloadHistoryEntry entry)` - Registra download
- `GetByEntryIdAsync(string entryUrl)` - Histórico de entrada
- `GetRecentAsync(int count)` - Downloads recentes

#### IFavoriteRepository
- `AddAsync(string entryUrl)` - Adiciona favorito
- `RemoveAsync(string entryUrl)` - Remove favorito
- `IsFavoriteAsync(string entryUrl)` - Verifica favorito
- `GetAllAsync()` - Lista todos favoritos

#### IM3uUrlRepository ⭐ NOVO
- `SaveOrUpdateAsync(url, name, isOnline, entryCount)` - Salva/atualiza URL
- `GetAllAsync()` - Lista todas URLs
- `GetRecentAsync(int count)` - URLs recentes
- `GetOnlineAsync()` - URLs online
- `GetOfflineAsync()` - URLs offline
- `DeleteOfflineAsync()` - Remove URLs offline
- `ExistsAsync(string url)` - Verifica existência
- `UpdateStatusAsync(url, isOnline, entryCount)` - Atualiza status

---

## 📁 Estrutura de Arquivos

```
M3U_VOD_Downloader-master/
├── 📂 Repositories/              # Camada de Repositório
│   ├── Interfaces.cs             # Interfaces dos repositórios
│   ├── DatabaseService.cs        # Serviço SQLite
│   └── MigrationService.cs       # Migração TXT → SQLite
│
├── 📂 docs/                      # Documentação
│   ├── ARCHITECTURE.md           # Arquitetura XUI One
│   ├── DATA_MODEL.md             # Modelos de dados
│   └── IMPLEMENTATION_PLAN.md    # Plano de implementação
│
├── 📂 .github/workflows/
│   └── build.yml                 # CI/CD GitHub Actions
│
├── MainWindow.xaml               # Interface principal
├── MainWindow.xaml.cs            # Lógica principal
├── Services.cs                   # Serviços M3U e Download
├── MeuGestorVODs.csproj          # Projeto .NET 8
├── build_completo.bat            # Script build completo
└── database.sqlite               # Banco de dados (gerado)
```

---

## 🔄 Fluxo de Dados

### Carregar Lista M3U
```
1. Usuário insere URL → ComboBox
2. Clica "Carregar"
3. M3UService.LoadFromUrlAsync() → Parse M3U
4. PersistLinkDatabases():
   ├── Salva no SQLite (Entries)
   └── Sincroniza com TXT (backup)
5. SaveOrUpdateAsync() → Salva URL no histórico
6. Atualiza UI com lista carregada
```

### Download de VOD
```
1. Usuário seleciona itens
2. Clica "Baixar Selecionados"
3. BuildOutputPath() → Define caminho
4. DownloadService.DownloadFileAsync()
5. Registra em DownloadHistory (SQLite)
6. Atualiza progresso na UI
```

### Histórico de URLs ⭐
```
1. App inicia → LoadM3UUrlHistory()
2. Carrega últimas 20 URLs no ComboBox
3. Usuário pode selecionar ou digitar nova
4. Ao carregar → Salva/atualiza no M3uUrlHistory
5. Botão "Histórico" → Mostra estatísticas
6. Botão "Limpar Offline" → Remove URLs fora do ar
```

---

## 🚀 Funcionalidades Implementadas

### ✅ Versão 1.0.8 (Anterior)
- Carregar listas M3U de URL
- Download de VODs
- Agrupamento por Categoria/Subcategoria
- Filtros e busca
- Atualização automática via GitHub

### ✅ Versão 1.0.9 (Atual)
- **SQLite como banco principal**
- **Repository Pattern** (preparado para PostgreSQL futuro)
- **Migração automática** de arquivos TXT
- **Histórico de downloads** no banco
- **Sistema de favoritos**
- **Histórico de URLs M3U** ⭐
- **ComboBox com autocomplete** para URLs
- **Gerenciamento de URLs offline** ⭐
- **Estatísticas do banco de dados**

---

## 🎯 Preparação para SaaS

### Repository Pattern
Permite trocar facilmente SQLite → PostgreSQL:

```csharp
// Desktop (atual)
_databaseService = new DatabaseService(DownloadPath);

// Futuro SaaS (apenas mudar implementação)
_databaseService = new PostgreSqlDatabaseService(connectionString);
```

### Interfaces Abstratas
Toda a lógica de negócio usa interfaces, não implementações concretas.

---

## 📝 Convenções de Código

### Nomenclatura
- **Classes:** PascalCase (ex: `DatabaseService`)
- **Interfaces:** I + PascalCase (ex: `IEntryRepository`)
- **Métodos:** PascalCase (ex: `GetAllAsync`)
- **Variáveis:** camelCase (ex: `_databaseService`)
- **Constantes:** UPPER_CASE (ex: `VodLinksDatabaseFileName`)

### Async/Await
- Métodos que acessam banco: `Async` suffix
- Retornos: `Task<T>` ou `Task`
- Usar `await` sempre que possível

### Tratamento de Erros
```csharp
try
{
    // Operação
}
catch (Exception ex)
{
    StatusMessage = $"Erro: {ex.Message}";
    // Log se necessário
}
```

---

## 🔍 Depuração

### Verificar Banco SQLite
Use o [DB Browser for SQLite](https://sqlitebrowser.org/):
1. Abra o arquivo `database.sqlite`
2. Navegue pelas tabelas
3. Execute queries SQL

### Logs de Build
GitHub Actions: https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/actions

### Testes Locais
```cmd
cd M3U_VOD_Downloader-master
dotnet run
```

---

## 📦 Dependências

### NuGet Packages
```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
<PackageReference Include="Dapper" Version="2.1.28" />
```

### .NET 8.0
- TargetFramework: `net8.0-windows`
- WPF com Windows Forms
- Self-contained publish

---

## 🔄 CI/CD Pipeline

### GitHub Actions Workflow
```yaml
Trigger: push para main/master ou tags v*
Steps:
1. Checkout código
2. Setup .NET 8.0
3. dotnet publish (Release)
4. Instala Inno Setup
5. Build do instalador .exe
6. Cria Release no GitHub (se for tag)
```

### Criar Nova Release
```bash
git tag -a v1.0.10 -m "Descrição"
git push origin v1.0.10
```

---

## 📞 Contato e Suporte

- **GitHub:** https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS
- **Issues:** Reporte bugs e solicite features
- **Wiki:** Documentação adicional (se houver)

---

**Última atualização:** 08/02/2026
**Versão atual:** 1.0.9
**Próxima versão planejada:** 1.1.0 (SaaS ready)
