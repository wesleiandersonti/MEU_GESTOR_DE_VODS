# 🗄️ DOCUMENTAÇÃO DO BANCO DE DADOS

Referência completa do schema SQLite utilizado no sistema.

---

## 📊 Visão Geral

**Banco:** SQLite 3  
**Arquivo:** `database.sqlite`  
**Local:** Pasta de downloads configurada pelo usuário  
**ORM:** Dapper (micro-ORM)  

---

## 📋 Tabelas

### 1. Entries - Canais e VODs

Armazena todos os canais e vídeos carregados das listas M3U.

#### Schema
```sql
CREATE TABLE IF NOT EXISTS Entries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,           -- ID interno auto-incremental
    EntryId TEXT UNIQUE NOT NULL,                   -- ID externo (tvg-id ou GUID)
    Name TEXT NOT NULL,                             -- Nome do canal/vídeo
    Url TEXT UNIQUE NOT NULL,                       -- URL do stream/arquivo
    GroupTitle TEXT,                                -- Título do grupo (categoria completa)
    Category TEXT,                                  -- Categoria principal (ex: Filmes, Séries)
    SubCategory TEXT,                               -- Subcategoria
    LogoUrl TEXT,                                   -- URL da logo/imagem
    TvgId TEXT,                                     -- ID da TVG
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,   -- Data de criação
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP    -- Data de atualização
);
```

#### Índices
```sql
CREATE INDEX idx_entries_url ON Entries(Url);           -- Busca por URL
CREATE INDEX idx_entries_category ON Entries(Category); -- Filtro por categoria
CREATE INDEX idx_entries_name ON Entries(Name);         -- Busca por nome
```

#### Exemplo de Uso
```csharp
// Inserir
await _databaseService.Entries.AddAsync(new M3UEntry {
    Id = "123",
    Name = "Canal Teste",
    Url = "http://exemplo.com/stream",
    Category = "Canais",
    GroupTitle = "Canais | Esportes"
});

// Buscar
var entries = await _databaseService.Entries.SearchAsync("ESPN");
```

---

### 2. DownloadHistory - Histórico de Downloads

Registra todas as operações de download realizadas.

#### Schema
```sql
CREATE TABLE IF NOT EXISTS DownloadHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,           -- ID do download
    EntryUrl TEXT NOT NULL,                         -- URL da entrada
    EntryName TEXT NOT NULL,                        -- Nome da entrada
    DownloadDate DATETIME DEFAULT CURRENT_TIMESTAMP,-- Data/hora do download
    FilePath TEXT,                                  -- Caminho local do arquivo
    FileSize INTEGER DEFAULT 0,                     -- Tamanho em bytes
    Success BOOLEAN DEFAULT 1,                      -- Sucesso (1) ou falha (0)
    ErrorMessage TEXT                               -- Mensagem de erro (se houver)
);
```

#### Índices
```sql
CREATE INDEX idx_downloads_url ON DownloadHistory(EntryUrl);   -- Histórico por entrada
CREATE INDEX idx_downloads_date ON DownloadHistory(DownloadDate); -- Ordenar por data
```

#### Exemplo de Uso
```csharp
// Registrar download
await _databaseService.Downloads.AddAsync(new DownloadHistoryEntry {
    EntryUrl = "http://exemplo.com/video.mp4",
    EntryName = "Vídeo Teste",
    FilePath = "C:\\Downloads\\video.mp4",
    FileSize = 1024000,
    Success = true
});

// Ver histórico
var history = await _databaseService.Downloads.GetByEntryIdAsync(url);
```

---

### 3. Favorites - Favoritos

Sistema de favoritos para marcar entradas preferidas.

#### Schema
```sql
CREATE TABLE IF NOT EXISTS Favorites (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,           -- ID do favorito
    EntryUrl TEXT UNIQUE NOT NULL,                  -- URL da entrada favorita
    AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP      -- Data de adição
);
```

#### Índices
```sql
CREATE INDEX idx_favorites_url ON Favorites(EntryUrl); -- Verificar favorito
```

#### Exemplo de Uso
```csharp
// Adicionar favorito
await _databaseService.Favorites.AddAsync("http://exemplo.com/canal");

// Verificar
bool isFav = await _databaseService.Favorites.IsFavoriteAsync(url);

// Listar todos
var favorites = await _databaseService.Favorites.GetAllAsync();
```

---

### 4. M3uUrlHistory - Histórico de URLs M3U ⭐ NOVO

Armazena todas as URLs M3U que foram testadas/carregadas.

#### Schema
```sql
CREATE TABLE IF NOT EXISTS M3uUrlHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,           -- ID do registro
    Url TEXT UNIQUE NOT NULL,                       -- URL da lista M3U
    Name TEXT,                                      -- Nome/descrição
    IsOnline BOOLEAN DEFAULT 1,                     -- Status (1=online, 0=offline)
    LastChecked DATETIME DEFAULT CURRENT_TIMESTAMP, -- Última verificação
    EntryCount INTEGER DEFAULT 0,                   -- Quantidade de itens carregados
    SuccessCount INTEGER DEFAULT 0,                 -- Contador de sucessos
    FailCount INTEGER DEFAULT 0,                    -- Contador de falhas
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP    -- Data de criação
);
```

#### Índices
```sql
CREATE INDEX idx_m3uurl_url ON M3uUrlHistory(Url);           -- Busca por URL
CREATE INDEX idx_m3uurl_online ON M3uUrlHistory(IsOnline);   -- Filtro online/offline
CREATE INDEX idx_m3uurl_checked ON M3uUrlHistory(LastChecked); -- Ordenar por data
```

#### Exemplo de Uso
```csharp
// Salvar URL carregada
await _databaseService.M3uUrls.SaveOrUpdateAsync(
    "http://exemplo.com/lista.m3u",
    "Lista IPTV Principal",
    isOnline: true,
    entryCount: 1500
);

// Listar URLs online
var online = await _databaseService.M3uUrls.GetOnlineAsync();

// Listar URLs offline
var offline = await _databaseService.M3uUrls.GetOfflineAsync();

// Remover URLs offline
int deleted = await _databaseService.M3uUrls.DeleteOfflineAsync();
```

#### Campos Importantes
- **IsOnline**: Indica se a URL está acessível
- **SuccessCount**: Quantas vezes carregou com sucesso
- **FailCount**: Quantas vezes falhou
- **EntryCount**: Total de itens na última carga

---

### 5. SchemaVersion - Versão do Schema

Controle de versão para migrações futuras.

#### Schema
```sql
CREATE TABLE IF NOT EXISTS SchemaVersion (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),          -- Sempre 1
    Version INTEGER NOT NULL DEFAULT 1,             -- Versão atual
    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP    -- Última atualização
);
```

#### Exemplo de Uso
```csharp
// Verificar versão
int version = await _databaseService.Entries.GetVersionAsync();

// Futuro: Migrações baseadas na versão
if (version < 2) {
    // Executar migração para v2
}
```

---

## 🔗 Relacionamentos

```
Entries (1) ←── (N) DownloadHistory
    ↑                ↓
    │           [EntryUrl]
    │
    │           Favorites (N) ──→ (1) Entries
    │           [EntryUrl]
    │
    └── M3uUrlHistory (independente)
```

**Nota:** M3uUrlHistory é independente, não tem FK para Entries.

---

## 📊 Diagrama ER (Entidade-Relacionamento)

```
┌─────────────────┐         ┌──────────────────┐
│    Entries      │         │ DownloadHistory  │
├─────────────────┤         ├──────────────────┤
│ PK Id           │◄───────│ FK EntryUrl      │
│    EntryId      │    1:N  │    EntryName     │
│    Name         │         │    DownloadDate  │
│    Url          │         │    FilePath      │
│    GroupTitle   │         │    FileSize      │
│    Category     │         │    Success       │
│    SubCategory  │         │    ErrorMessage  │
│    LogoUrl      │         └──────────────────┘
│    TvgId        │
│    CreatedAt    │         ┌──────────────────┐
│    UpdatedAt    │         │    Favorites     │
└─────────────────┘         ├──────────────────┤
         ▲                  │ PK Id            │
         │                  │ FK EntryUrl ─────┼────► Entries
         │                  │    AddedAt       │
         │                  └──────────────────┘
         │
         │                  ┌──────────────────┐
         │                  │  M3uUrlHistory   │
         │                  ├──────────────────┤
         │                  │ PK Id            │
         │                  │    Url           │
         │                  │    Name          │
         │                  │    IsOnline      │
         │                  │    LastChecked   │
         │                  │    EntryCount    │
         │                  │    SuccessCount  │
         │                  │    FailCount     │
         │                  │    CreatedAt     │
         │                  └──────────────────┘
         │
         │                  ┌──────────────────┐
         │                  │  SchemaVersion   │
         │                  ├──────────────────┤
         │                  │ PK Id            │
         │                  │    Version       │
         └──────────────────│    UpdatedAt     │
                            └──────────────────┘
```

---

## 🔍 Queries Úteis

### Estatísticas Gerais
```sql
-- Total de entradas por categoria
SELECT Category, COUNT(*) as Total 
FROM Entries 
GROUP BY Category 
ORDER BY Total DESC;

-- Total de downloads
SELECT COUNT(*) as TotalDownloads,
       SUM(CASE WHEN Success = 1 THEN 1 ELSE 0 END) as Sucessos,
       SUM(CASE WHEN Success = 0 THEN 1 ELSE 0 END) as Falhas
FROM DownloadHistory;

-- URLs online vs offline
SELECT 
    SUM(CASE WHEN IsOnline = 1 THEN 1 ELSE 0 END) as Online,
    SUM(CASE WHEN IsOnline = 0 THEN 1 ELSE 0 END) as Offline
FROM M3uUrlHistory;
```

### Buscar Entradas
```sql
-- Buscar por nome (case-insensitive)
SELECT * FROM Entries 
WHERE Name LIKE '%ESPN%' 
ORDER BY Name;

-- Buscar por categoria e subcategoria
SELECT * FROM Entries 
WHERE Category = 'Filmes' 
  AND SubCategory LIKE '%Ação%';

-- Entradas sem logo
SELECT * FROM Entries 
WHERE LogoUrl IS NULL OR LogoUrl = '';
```

### Manutenção
```sql
-- Limpar entradas duplicadas (manter mais recente)
DELETE FROM Entries 
WHERE Id NOT IN (
    SELECT MIN(Id) 
    FROM Entries 
    GROUP BY Url
);

-- Limpar histórico antigo (mais de 1 ano)
DELETE FROM DownloadHistory 
WHERE DownloadDate < datetime('now', '-1 year');

-- Limpar URLs offline
DELETE FROM M3uUrlHistory 
WHERE IsOnline = 0;
```

---

## 💡 Boas Práticas

### 1. Sempre use parâmetros (evita SQL Injection)
```csharp
// ❌ Ruim
var sql = $"SELECT * FROM Entries WHERE Name = '{name}'";

// ✅ Bom
var sql = "SELECT * FROM Entries WHERE Name = @Name";
var result = await conn.QueryAsync<Entry>(sql, new { Name = name });
```

### 2. Use índices para campos de busca frequente
```sql
-- Campos que devem ter índice:
-- Url (único)
-- Category (filtros frequentes)
-- Name (buscas)
```

### 3. Transações para operações múltiplas
```csharp
using var transaction = connection.BeginTransaction();
try {
    // Múltiplas operações
    transaction.Commit();
} catch {
    transaction.Rollback();
}
```

### 4. Limite resultados quando possível
```csharp
// Use LIMIT/TOP para grandes consultas
var recent = await conn.QueryAsync<Entry>(
    "SELECT * FROM Entries ORDER BY CreatedAt DESC LIMIT 100"
);
```

---

## 🔧 Manutenção do Banco

### Backup
O arquivo `database.sqlite` é um arquivo único. Para backup:
1. Feche o aplicativo
2. Copie o arquivo `database.sqlite`
3. Armazene em local seguro

### Otimização
```sql
-- Reorganizar banco (SQLite)
VACUUM;

-- Atualizar estatísticas
ANALYZE;
```

### Migração Futura (para PostgreSQL)
Quando migrar para SaaS:
1. Exportar SQLite para SQL
2. Adaptar sintaxe (SQLite → PostgreSQL)
3. Importar para PostgreSQL
4. Alterar `DatabaseService` para usar Npgsql

---

## 📞 Suporte

Em caso de problemas com o banco:
1. Verifique permissões de escrita na pasta
2. Use DB Browser for SQLite para inspecionar
3. Verifique logs do aplicativo
4. Consulte documentação do SQLite: https://sqlite.org/docs.html

---

**Versão do Schema:** 1  
**Última Atualização:** 08/02/2026
