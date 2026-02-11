# Correção v1.0.41 - Carregamento de Módulos nas Abas

## Problema Identificado

**Data:** Fevereiro 2026
**Versão Afetada:** v1.0.39 - v1.0.40
**Sintoma:** Ao abrir LisoFlix ou DARK M3U CHECKER pelo menu, a aba era criada mas ficava vazia (sem conteúdo)

## Causa Raiz

O problema estava na função `OpenModuleInTab()` que criava o WebView2 mas não inicializava corretamente o `CoreWebView2` antes de tentar navegar para o arquivo HTML.

### Problemas Específicos:

1. **Inicialização Assíncrona Incorreta:** O código chamava `EnsureCoreWebView2Async()` mas não aguardava o evento de inicialização completa
2. **Definição Prematura de Source:** Tentava definir `webView.Source` antes do CoreWebView2 estar pronto
3. **Código Duplicado:** `CreationProperties` estava sendo definido duas vezes
4. **Navegação Local:** URL do arquivo local não estava formatada corretamente para o protocolo `file:///`

## Solução Implementada

### Mudanças na Função `OpenModuleInTab()`:

```csharp
// REMOVIDO: Código duplicado de CreationProperties
// REMOVIDO: Configuração prematura do NavigationStarting

// ADICIONADO: Chamada simplificada para inicialização
InitializeWebView(webView, htmlPath);
```

### Nova Função `InitializeWebView()`:

```csharp
private void InitializeWebView(WebView2 webView, string htmlPath)
{
    try
    {
        // Configura o WebView2
        webView.CreationProperties = new CoreWebView2CreationProperties
        {
            UserDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MeuGestorVODs",
                "WebView2Data",
                DateTime.Now.Ticks.ToString())
        };

        // Evento quando o CoreWebView2 estiver pronto
        webView.CoreWebView2InitializationCompleted += (s, e) =>
        {
            if (e.IsSuccess && webView.CoreWebView2 != null)
            {
                // Configurações para permitir conteúdo HTTP e conteúdo misto
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webView.CoreWebView2.Settings.AreHostObjectsAllowed = true;
                
                // Navega para o arquivo HTML usando file:///
                var fullPath = Path.GetFullPath(htmlPath);
                webView.CoreWebView2.Navigate($"file:///{fullPath.Replace("\\", "/")}");
            }
            else if (e.InitializationException != null)
            {
                // Mostra erro se houver exceção na inicialização
                MessageBox.Show($"Erro: {e.InitializationException.Message}", "Erro");
            }
        };

        // Inicializa o CoreWebView2 (assíncrono)
        webView.EnsureCoreWebView2Async();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Erro: {ex.Message}", "Erro");
    }
}
```

## Pontos-Chave da Correção

1. **Evento `CoreWebView2InitializationCompleted`:** Aguarda o WebView2 estar totalmente inicializado antes de navegar
2. **Uso de `Navigate()` em vez de `Source`:** Método mais confiável para carregar conteúdo local
3. **Formatação correta do caminho:** Converte `C:\path\file.html` para `file:///C:/path/file.html`
4. **UserDataFolder único:** Usa `DateTime.Now.Ticks` para evitar conflitos entre abas

## Arquivos Modificados

- **MainWindow.xaml.cs:** Funções `OpenModuleInTab()` e `InitializeWebView()`
- **MeuGestorVODs.csproj:** Versão atualizada para 1.0.41
- **update.json:** Notas de release atualizadas

## Testes Realizados

✅ Build compila com sucesso (0 erros)
✅ LisoFlix abre corretamente na aba interna
✅ DARK M3U CHECKER abre corretamente na aba interna
✅ Múltiplas abas podem ser abertas simultaneamente
✅ Conteúdo carrega completamente (não fica vazio)

## Versão

**v1.0.41** - Correção do carregamento de módulos nas abas
**Commit:** e78a088
**Tag:** v1.0.41

## Próximos Passos Sugeridos

1. Testar com outros módulos HTML
2. Adicionar indicador de carregamento enquanto o WebView2 inicializa
3. Implementar tratamento de erro mais robusto (retry em caso de falha)
