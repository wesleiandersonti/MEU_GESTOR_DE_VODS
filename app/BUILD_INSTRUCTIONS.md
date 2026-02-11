# 🚀 Instruções de Build - MEU GESTOR DE VODS

## ⚠️ Pré-requisitos

Antes de começar, certifique-se de ter instalado:
- **.NET 8.0 SDK** - [Download aqui](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Git** (opcional, para clonar o repositório)

Para verificar se o SDK está instalado, abra o CMD e execute:
```cmd
dotnet --version
```

Deve aparecer algo como: `8.0.x`

---

## 📋 Etapas do Build

### ✅ Etapa 1: Restaurar Pacotes NuGet

**O que faz:** Baixa os pacotes SQLite e Dapper necessários para o projeto.

**Comando:**
```cmd
dotnet restore
```

**Ou execute:** `etapa1_restore.bat`

**Saída esperada:**
```
  Determinando os projetos a serem restaurados...
  Restaurado C:\...\MeuGestorVODs.csproj (em 1,23 sec)
```

---

### ✅ Etapa 2: Compilar o Projeto (Build)

**O que faz:** Compila o código e verifica se não há erros.

**Comando:**
```cmd
dotnet build
```

**Ou execute:** `etapa2_build.bat`

**Saída esperada:**
```
  Compilacao iniciada...
  MeuGestorVODs -> C:\...\bin\Debug\net8.0-windows\MeuGestorVODs.dll
  
  Compilacao bem-sucedida.
    0 Erro(s)
    0 Aviso(s)
```

---

### ✅ Etapa 3: Testar Migração (Executar o App)

**O que faz:** Executa o aplicativo para testar a migração automática dos TXT.

**Comando:**
```cmd
dotnet run
```

**Ou execute diretamente:**
```cmd
.\bin\Debug\net8.0-windows\MeuGestorVODs.exe
```

**Testes a fazer:**
1. Se tiver arquivos `banco_vod_links.txt` ou `banco_canais_ao_vivo.txt`, o app perguntará sobre migração
2. Clique **"Sim"** para migrar automaticamente para SQLite
3. Carregue uma lista M3U e verifique se salvou no banco
4. Clique no botão **"Estatísticas BD"** para ver o total de entradas
5. Verifique se os arquivos TXT foram atualizados também

---

### ✅ Etapa 4: Publicar Release

**O que faz:** Gera o executável final pronto para distribuição.

**Comando:**
```cmd
dotnet publish -c Release --self-contained true -r win-x64 -o "./publish"
```

**Ou execute:** `etapa4_publish.bat`

**Saída esperada:**
```
  Publicacao iniciada...
  MeuGestorVODs -> C:\...\publish\
  
  Publicacao bem-sucedida.
```

**Arquivos gerados em:** `\publish\`

---

## 🎯 Build Completo (Todas as Etapas)

Para executar todas as etapas de uma vez:

```cmd
build_completo.bat
```

Este script executa:
1. ✅ Restore
2. ✅ Build
3. ✅ Testes
4. ✅ Publish

---

## 📁 Estrutura de Arquivos Gerada

```
M3U_VOD_Downloader-master/
├── publish/                    ← Release final
│   ├── MeuGestorVODs.exe      ← Executável principal
│   ├── *.dll                  ← Bibliotecas
│   └── ...
├── bin/                        ← Compilação Debug
├── obj/                        ← Arquivos temporários
├── Repositories/               ← Código fonte novo
│   ├── Interfaces.cs
│   ├── DatabaseService.cs
│   └── MigrationService.cs
├── etapa1_restore.bat
├── etapa2_build.bat
├── etapa4_publish.bat
└── build_completo.bat
```

---

## 🧪 Testes Após o Build

### Teste 1: Migração de Dados
1. Coloque arquivos TXT antigos na pasta de downloads
2. Execute o app
3. Aceite a migração
4. Verifique se `database.sqlite` foi criado

### Teste 2: Persistência
1. Carregue uma lista M3U
2. Verifique a mensagem: "SQLite: +X VOD, +X canais"
3. Clique em "Estatísticas BD" - deve mostrar o total
4. Verifique se os arquivos TXT também foram atualizados

### Teste 3: Busca
1. Use o campo "Filtrar" para buscar por nome
2. Verifique se a busca é rápida (SQLite é muito rápido!)

---

## ❌ Erros Comuns

### "No .NET SDKs were found"
**Solução:** Instale o .NET 8.0 SDK do site oficial

### "error NU1101: Unable to find package"
**Solução:** Execute `dotnet restore` novamente

### "error CS0246: The type or namespace name 'Repositories' could not be found"
**Solução:** Verifique se os arquivos em `Repositories/` existem

---

## 📦 Para Criar o Instalador

Após o `publish`, você pode criar um instalador com:

- **Inno Setup** (recomendado)
- **WiX Toolset**
- **NSIS**

Ou simplesmente compactar a pasta `publish/` em um ZIP.

---

## 🚀 Pronto para Distribuir!

Após executar todas as etapas com sucesso, o aplicativo está pronto para:
- ✅ Uso local
- ✅ Distribuição no GitHub
- ✅ Instalação em outros PCs

**Arquivo principal:** `publish/MeuGestorVODs.exe`

---

## 💡 Dicas

- Sempre execute `dotnet restore` após clonar o repositório
- Use `dotnet build --verbosity quiet` para menos mensagens
- Use `dotnet watch run` durante desenvolvimento (hot reload)
- O SQLite cria o arquivo `database.sqlite` automaticamente na primeira execução

---

## 📞 Suporte

Em caso de problemas, verifique:
1. Versão do .NET: `dotnet --version`
2. Arquivos do projeto estão inteiros
3. Permissões de escrita na pasta

Ou abra uma issue no GitHub!
