# Sistema de IndexedDB - LisoFlix

## Visão Geral

O LisoFlix agora utiliza **IndexedDB**, um banco de dados NoSQL embutido no navegador, para armazenar dados localmente. Isso permite que o aplicativo funcione **offline** após o primeiro carregamento e melhora significativamente a performance.

## Funcionalidades

### 1. Armazenamento Local de Dados

O sistema armazena em IndexedDB:
- **Filmes (VODs)**: Lista completa de filmes com metadados
- **Séries**: Lista completa de séries com metadados  
- **Metadados**: Data da última atualização e configurações

### 2. Modo Offline

Após o primeiro carregamento bem-sucedido:
- Os dados são salvos automaticamente no IndexedDB
- Aplicação funciona **sem internet** nas próximas vezes
- Exibe badge "OFFLINE" quando usando dados em cache

### 3. Interface de Atualização

No canto inferior direito da tela:
- **Data da última atualização**: Mostra quando os dados foram buscados da API
- **Botão "Atualizar"**: Permite buscar dados frescos da API a qualquer momento

## Como Funciona

### Fluxo de Carregamento

```
1. Usuário abre o LisoFlix
2. Sistema tenta carregar do IndexedDB primeiro
3. Se tiver dados em cache:
   - Exibe imediatamente (carregamento instantâneo)
   - Mostra data da última atualização
4. Se não tiver dados ou for forçado:
   - Busca da API
   - Salva no IndexedDB
   - Atualiza timestamp
```

### Estrutura do Banco de Dados

**Nome do banco**: `LisoFlixDB`
**Versão**: `1`

#### Object Stores:

1. **filmes**
   - Key: `stream_id`
   - Indexes: `category_name`, `name`
   - Armazena: Lista completa de VODs

2. **series**
   - Key: `series_id`
   - Indexes: `category_name`, `name`
   - Armazena: Lista completa de séries

3. **metadata**
   - Key: `key`
   - Armazena: Configurações e timestamps

## APIs do Sistema

### initDB()
Inicializa a conexão com o IndexedDB.
```javascript
await initDB();
```

### saveToDB(storeName, data)
Salva dados em uma store.
```javascript
await saveToDB('filmes', listaDeFilmes);
```

### loadFromDB(storeName)
Carrega todos os dados de uma store.
```javascript
const filmes = await loadFromDB('filmes');
```

### saveMetadata(key, value)
Salva metadados (ex: timestamp).
```javascript
await saveMetadata('lastUpdate', new Date().toISOString());
```

### loadMetadata(key)
Carrega metadados.
```javascript
const lastUpdate = await loadMetadata('lastUpdate');
```

### atualizarDados()
Força atualização da API (ignora cache).
```javascript
await atualizarDados(); // Chama renderTelaPrincipal(true)
```

## Uso

### Carregamento Normal (automático)
```javascript
// Na inicialização
renderTelaPrincipal(); // Usa cache se disponível
```

### Forçar Atualização
Clique no botão "Atualizar" no canto inferior direito ou:
```javascript
await renderTelaPrincipal(true); // true = força update
```

### Tratamento de Erros

Se a API falhar mas houver cache:
- Exibe dados salvos
- Mostra banner "Modo offline"
- Permite navegação normal

Se não houver cache e falhar:
- Exibe mensagem de erro
- Instruções para verificar conexão

## Benefícios

1. **Performance**: Carregamento instantâneo após primeiro uso
2. **Offline**: Funciona sem internet
3. **Economia de dados**: Não precisa buscar da API toda vez
4. **Resiliência**: Continua funcionando se API cair
5. **UX melhor**: Usuário vê conteúdo imediatamente

## Limitações

1. **Limite de espaço**: Navegador pode limitar (geralmente 50MB+)
2. **Privado por navegador**: Dados não sincronizam entre dispositivos
3. **Limpeza**: Dados podem ser apagados se usuário limpar cache
4. **Primeiro carregamento**: Ainda precisa de internet na primeira vez

## Manutenção

### Limpar Dados
Para resetar o banco de dados:
1. Abrir DevTools (F12)
2. Aba Application/Storage
3. IndexedDB → LisoFlixDB
4. Delete database

### Debug
```javascript
// Ver dados no console
console.log('Filmes:', await loadFromDB('filmes'));
console.log('Séries:', await loadFromDB('series'));
console.log('Última atualização:', await loadMetadata('lastUpdate'));
```

## Compatibilidade

IndexedDB é suportado por:
- ✅ Chrome/Edge (versões recentes)
- ✅ Firefox (versões recentes)
- ✅ Safari (versões recentes)
- ✅ WebView2 (usado no app WPF)

## Notas de Implementação

- Dados são salvos automaticamente após fetch bem-sucedido
- Cache não expira automaticamente (manual via botão Atualizar)
- Favoritos continuam usando localStorage (dados pequenos)
- Imagens (posters) não são salvas em IndexedDB (apenas URLs)

## Versão

**Implementado em**: v1.0.34+  
**Última atualização**: Fevereiro 2026
