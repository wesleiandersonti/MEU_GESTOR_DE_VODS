# Novas Funcionalidades v1.0.38

## 🎬 Botão Cast para Dispositivos na Rede

### Descrição
Novo botão "📺 Cast" na barra de ferramentas principal que permite enviar conteúdo para reprodução em dispositivos na mesma rede (Smart TVs, Chromecast, Roku, Xbox, etc.).

### Como Usar
1. Selecione um ou mais itens na lista usando os checkboxes
2. Clique no botão "📺 Cast" (ao lado de "Analisar Link")
3. Uma janela mostrará dispositivos disponíveis na rede
4. Selecione o dispositivo desejado
5. Clique em "▶️ Iniciar Cast"

### Recursos
- ✅ Interface visual com lista de dispositivos
- ✅ Suporte múltiplos dispositivos simulados
- ✅ Botão "Escanear Rede" para buscar novos dispositivos
- ✅ Seleção fácil de dispositivos
- ✅ Confirmação antes de iniciar

### Nota Técnica
Na versão atual, a funcionalidade mostra dispositivos de exemplo. Na versão completa, implementará protocolos DLNA/UPnP para descoberta real de dispositivos na rede local.

---

## 🔗 Sistema de Links M3U8 Personalizados no LisoFlix

### Descrição
Sistema completo de gerenciamento de links M3U8 personalizados no LisoFlix, com banco de dados IndexedDB exclusivo, permitindo que o usuário configure seus próprios links de streaming.

### Recursos

#### 1. Banco de Dados IndexedDB (Versão 2)
- **Store `customLinks`**: Armazena links M3U8 configurados pelo usuário
  - Campos: `id`, `name`, `url`, `addedDate`, `lastPlayed`, `playCount`
  - Índices: `name`, `url` (único), `lastPlayed`
  
- **Store `playbackHistory`**: Registra todo histórico de reprodução
  - Campos: `id`, `contentName`, `contentUrl`, `timestamp`, `source`
  - Índices: `contentName`, `contentUrl`, `timestamp`, `source`
  - Fonte pode ser: `'custom'` (links personalizados) ou `'api'` (API do sistema)

#### 2. Interface de Configuração
Acesse clicando no botão "⚙️ Configurar Links" no canto inferior direito do LisoFlix.

**Funcionalidades:**
- ➕ **Adicionar Novo Link**: Nome e URL M3U8
- 📋 **Lista de Links**: Visualização de todos os links configurados
- ▶️ **Reproduzir**: Iniciar reprodução diretamente da lista
- 🗑️ **Excluir**: Remover links indesejados
- 📊 **Estatísticas**: Total de links e reproduções

#### 3. Histórico de Reprodução Automático
Toda vez que um vídeo é reproduzido (tanto da API quanto de links personalizados), é automaticamente salvo no banco de dados:
- Nome do conteúdo
- URL do stream
- Data/hora da reprodução
- Fonte (API ou Custom)

#### 4. Controle de Reprodução
- **playCount**: Contador de quantas vezes cada link foi reproduzido
- **lastPlayed**: Data da última reprodução
- **Persistência**: Dados mantidos mesmo após fechar o navegador

### Como Usar

#### Adicionar Link Personalizado
1. Abra o LisoFlix
2. Clique em "⚙️ Configurar Links" (canto inferior direito)
3. Na seção "Adicionar Novo Link", preencha:
   - **Nome**: Ex: "TV Aberta - Canal 1"
   - **URL**: Ex: "http://exemplo.com/stream.m3u8"
4. Clique em "Adicionar"

#### Reproduzir Link Personalizado
1. Na lista "Meus Links Configurados", encontre o desejado
2. Clique em "▶ Reproduzir"
3. O vídeo abrirá no player automaticamente
4. A reprodução será salva no histórico

#### Ver Estatísticas
- Total de links configurados
- Total de reproduções em todos os links
- Última data de reprodução de cada link

### Segurança e Privacidade
- ✅ Links são armazenados localmente no navegador (IndexedDB)
- ✅ Apenas o usuário tem acesso aos links configurados
- ✅ Nenhum dado é enviado para servidores externos
- ✅ Total privacidade e controle do usuário

### Notas Técnicas
- **Versão do Banco**: 2 (upgrade automático da versão 1)
- **Limite de Armazenamento**: Depende do navegador (geralmente 50MB+)
- **Persistência**: Dados permanecem até usuário limpar dados do navegador
- **Backup**: Links podem ser exportados manualmente (funcionalidade futura)

---

## 📋 Resumo das Mudanças

### Arquivos Modificados
1. **MainWindow.xaml**: Adicionado botão Cast
2. **MainWindow.xaml.cs**: Implementada lógica do Cast
3. **LisoFlix.html**: Sistema completo de links M3U8 personalizados
4. **MeuGestorVODs.csproj**: Versão atualizada para 1.0.38
5. **update.json**: Notas de release atualizadas

### Versões
- **Versão**: 1.0.38
- **Banco LisoFlix**: Versão 2 (IndexedDB)
- **Data**: Fevereiro 2026

### Compatibilidade
- ✅ Windows 10/11
- ✅ .NET 8.0
- ✅ WebView2 (para LisoFlix)
- ✅ Navegadores modernos (Chrome, Edge, Firefox)

---

## 🚀 Próximos Passos Sugeridos

1. **Implementar descoberta real DLNA/UPnP** para o Cast
2. **Adicionar exportação/importação** de links M3U8
3. **Criar categorias** para organizar links personalizados
4. **Adicionar busca** nos links configurados
5. **Implementar favoritos** no histórico de reprodução

---

## 📞 Suporte

Para dúvidas ou sugestões sobre estas funcionalidades, consulte a documentação ou entre em contato através do GitHub.
