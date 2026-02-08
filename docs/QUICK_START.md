# 🚀 GUIA RÁPIDO - DESENVOLVEDOR

Guia rápido para operações comuns no projeto.

---

## 🎯 Comandos Essenciais

### Build Local
```cmd
# Restaurar pacotes
dotnet restore

# Compilar
dotnet build

# Executar
dotnet run

# Publicar Release
dotnet publish -c Release --self-contained true -r win-x64 -o "./publish"
```

### Scripts de Automação
```cmd
# Build completo (todas as etapas)
build_completo.bat

# Ou passo a passo:
etapa1_restore.bat    # Restaurar pacotes
etapa2_build.bat      # Compilar
etapa4_publish.bat    # Publicar
```

---

## 🔄 Fluxo Git

### Enviar alterações
```bash
# Verificar status
git status

# Adicionar arquivos
git add -A

# Criar commit
git commit -m "tipo: descrição curta

descrição detalhada do que foi feito

BREAKING CHANGE: se houver"

# Enviar para GitHub
git push origin main
```

### Criar Nova Release
```bash
# Criar tag
git tag -a v1.0.10 -m "Versão 1.0.10 - Descrição"

# Enviar tag (dispara CI/CD)
git push origin v1.0.10
```

### Atualizar tag existente
```bash
# Deletar tag local
git tag -d v1.0.9

# Recriar tag
git tag -a v1.0.9 -m "Nova descrição"

# Forçar push da tag
git push origin v1.0.9 --force
```

---

## 💾 Operações no Banco SQLite

### Conexão
```csharp
using var connection = _databaseService.CreateConnection();
connection.Open();
// Operações...
```

### Consultas Dapper
```csharp
// SELECT simples
var entries = await connection.QueryAsync<Entry>(
    "SELECT * FROM Entries WHERE Category = @Category",
    new { Category = "Filmes" }
);

// INSERT
var id = await connection.ExecuteAsync(
    "INSERT INTO Entries (Name, Url) VALUES (@Name, @Url)",
    new { Name = "Nome", Url = "http://..." }
);

// UPDATE
await connection.ExecuteAsync(
    "UPDATE Entries SET Name = @Name WHERE Id = @Id",
    new { Name = "Novo Nome", Id = 1 }
);

// DELETE
await connection.ExecuteAsync(
    "DELETE FROM Entries WHERE Id = @Id",
    new { Id = 1 }
);
```

### Transações
```csharp
using var transaction = connection.BeginTransaction();
try
{
    await connection.ExecuteAsync("INSERT...", obj, transaction);
    await connection.ExecuteAsync("UPDATE...", obj, transaction);
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

---

## 🎨 Padrões de UI

### Adicionar Novo Botão
```xml
<Button Content="Nome" 
        Click="Metodo_Click" 
        Padding="12,5" 
        Margin="5,0,0,0"
        Background="#COR" 
        Foreground="White"/>
```

### Cores Padrão
- Azul: `#2196F3` - Ações principais
- Verde: `#4CAF50` - Sucesso/download
- Vermelho: `#F44336` - Perigo/remover
- Laranja: `#FF9800` - Alerta/atualização
- Roxo: `#673AB7` - Estatísticas/info

### MessageBox
```csharp
// Informação
MessageBox.Show("Texto", "Título", MessageBoxButton.OK, MessageBoxImage.Information);

// Confirmação
var result = MessageBox.Show("Texto", "Título", MessageBoxButton.YesNo, MessageBoxImage.Question);
if (result == MessageBoxResult.Yes) { }

// Erro
MessageBox.Show("Texto", "Título", MessageBoxButton.OK, MessageBoxImage.Error);
```

---

## 🧪 Testes Rápidos

### Testar Carregamento M3U
1. Abra o app
2. Cole URL: `http://exemplo.com/lista.m3u`
3. Clique "Carregar"
4. Verifique mensagem de status

### Testar Banco de Dados
1. Clique "Estatísticas BD"
2. Deve mostrar contagem de entradas
3. Verifique se `database.sqlite` foi criado

### Testar Histórico de URLs
1. Carregue uma lista M3U
2. Clique "Histórico"
3. Deve mostrar a URL com status ✅

### Testar Limpar Offline
1. Clique "Limpar Offline"
2. Se houver URLs offline, confirme
3. Verifique se foram removidas

---

## 🐛 Debugging

### Ver Logs do Build
https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/actions

### Abrir Banco SQLite
1. Baixe [DB Browser for SQLite](https://sqlitebrowser.org/)
2. Abra arquivo `database.sqlite`
3. Navegue pelas tabelas

### Breakpoints Úteis
- `MainWindow.xaml.cs:LoadM3U_Click` - Carregamento M3U
- `MainWindow.xaml.cs:PersistLinkDatabases` - Persistência
- `DatabaseService.cs:InitializeDatabase` - Inicialização do banco

### Status Comuns
```csharp
StatusMessage = "Carregando...";     // Operação em andamento
StatusMessage = "Concluído!";         // Sucesso
StatusMessage = $"Erro: {ex.Message}"; // Erro
```

---

## 📦 Estrutura de Arquivos Importantes

### Código Fonte
```
MainWindow.xaml          # Interface XAML
MainWindow.xaml.cs       # Lógica principal (cuidado, arquivo grande!)
Services.cs              # M3UService, DownloadService
```

### Banco de Dados
```
Repositories/
├── Interfaces.cs        # Todas as interfaces
├── DatabaseService.cs   # Implementação SQLite
└── MigrationService.cs  # Migração TXT → SQLite
```

### Configuração
```
MeuGestorVODs.csproj     # Projeto e dependências
.github/workflows/
└── build.yml            # CI/CD
```

### Documentação
```
docs/
├── ARCHITECTURE.md      # Arquitetura XUI One
├── DATA_MODEL.md        # Modelos de dados
├── IMPLEMENTATION_PLAN.md # Plano de implementação
└── PROJECT_ARCHITECTURE.md # Arquitetura completa
```

---

## ⚠️ Cuidados Importantes

### Nunca Faça
- ❌ Commit de arquivos `.exe` ou `.dll`
- ❌ Commit do `database.sqlite` (dados locais)
- ❌ Alterar diretamente a branch `main` sem testar
- ❌ Esquecer de atualizar a tag ao criar release

### Sempre Faça
- ✅ Testar localmente antes de commitar
- ✅ Usar mensagens de commit descritivas
- ✅ Atualizar CHANGELOG.md em releases
- ✅ Verificar build no GitHub Actions

---

## 🔧 Solução de Problemas

### Erro: "No .NET SDKs were found"
**Solução:** Instalar .NET 8.0 SDK

### Erro: "Unable to find package"
**Solução:** `dotnet restore`

### Erro de build no GitHub Actions
**Solução:** Verificar warnings tratados como erros

### Banco não inicializa
**Solução:** Verificar permissões de escrita na pasta

---

## 📞 Links Úteis

- **GitHub:** https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS
- **Dapper:** https://github.com/DapperLib/Dapper
- **SQLite:** https://www.sqlite.org/
- **WPF:** https://docs.microsoft.com/pt-br/dotnet/desktop/wpf/

---

**Dica:** Mantenha este guia aberto enquanto desenvolve!
