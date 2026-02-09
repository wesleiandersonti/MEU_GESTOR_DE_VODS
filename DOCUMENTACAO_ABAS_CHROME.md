# Sistema de Abas Estilo Chrome - Implementação

## Visão Geral
Sistema de abas implementado no `MainWindow.xaml` estilo Google Chrome, permitindo abrir múltiplos módulos simultaneamente.

## O que foi implementado

### 1. Estilos Chrome (Window.Resources)
```xml
- ChromeTabBackground: Cor de fundo das abas (#DEE1E6)
- ChromeTabActiveBackground: Cor da aba ativa (#FFFFFF)
- ChromeTabHoverBackground: Cor do hover (#E8EAED)
- ChromeTabItemStyle: Estilo visual das abas (cantos arredondados, botão X)
- ChromeTabControlStyle: Estilo do container de abas
```

### 2. Header Redesenhado
```xml
- Row 0: Header com sistema de abas
  - Botão Menu (hambúrguer) à esquerda
  - TabControl com abas estilo Chrome
  - Botão "+" para nova aba
  - Botões de controle da janela (min, max, close) à direita
```

### 3. Funcionalidades
- ✅ **Abas visuais** estilo Chrome (cantos arredondados, cores)
- ✅ **Botão X** em cada aba para fechar
- ✅ **Botão Nova Aba (+)** no header
- ✅ **Menu** com opções de módulos
- ✅ **Hover effects** nas abas
- ✅ **Aba ativa destacada** (fundo branco, sem borda inferior)

## Próximos Passos para Funcionalidade Completa

### 1. Mover Conteúdo para dentro das Abas
Atualmente o conteúdo principal está fora do TabControl. Para ter múltiplas abas funcionais:

```xml
<TabControl x:Name="ChromeTabControl">
    <TabItem Header="MEU GESTOR DE VODS">
        <!-- Mover todo o conteúdo atual aqui -->
        <Grid>
            <!-- Config Panel -->
            <!-- Main Content -->
        </Grid>
    </TabItem>
    <TabItem Header="LisoFlix">
        <WebView2 ... />
    </TabItem>
    <TabItem Header="XUI-ONE">
        <!-- Conteúdo do módulo -->
    </TabItem>
</TabControl>
```

### 2. Eventos no Code-Behind (MainWindow.xaml.cs)

Adicionar handlers:

```csharp
private void NewTab_Click(object sender, RoutedEventArgs e)
{
    // Criar nova aba com conteúdo padrão
    var newTab = new TabItem { Header = "Nova Aba" };
    ChromeTabControl.Items.Add(newTab);
    ChromeTabControl.SelectedItem = newTab;
}

private void CloseTab_Click(object sender, RoutedEventArgs e)
{
    // Fechar aba clicada
    if (sender is Button btn && btn.Tag is TabItem tab)
    {
        ChromeTabControl.Items.Remove(tab);
    }
}

private void NewWindow_Click(object sender, RoutedEventArgs e)
{
    // Abrir nova instância do aplicativo
    var newWindow = new MainWindow();
    newWindow.Show();
}
```

### 3. Navegação entre Módulos

```csharp
private void MainMenuLisoFlix_Click(object sender, RoutedEventArgs e)
{
    // Verificar se já existe aba do LisoFlix
    foreach (TabItem tab in ChromeTabControl.Items)
    {
        if (tab.Header.ToString() == "LisoFlix")
        {
            ChromeTabControl.SelectedItem = tab;
            return;
        }
    }
    
    // Criar nova aba
    var newTab = new TabItem { Header = "LisoFlix" };
    var webView = new WebView2();
    // Configurar WebView2...
    newTab.Content = webView;
    ChromeTabControl.Items.Add(newTab);
    ChromeTabControl.SelectedItem = newTab;
}
```

## Estilo Visual Implementado

### Cores
- **Fundo das abas**: `#DEE1E6` (cinza claro)
- **Aba ativa**: `#FFFFFF` (branco)
- **Hover**: `#E8EAED` (cinza mais claro)
- **Borda**: `#DADCE0` (cinza)
- **Texto**: `#5F6368` (cinza escuro)

### Dimensões
- **Altura da aba**: 32px
- **Border radius**: 8px 8px 0 0 (topo arredondado)
- **Margem entre abas**: 2px
- **Padding interno**: 12px horizontal

### Botões de Controle
- **Minimizar**: 45x38px, símbolo "—"
- **Maximizar**: 45x38px, símbolo "□"
- **Fechar**: 45x38px, símbolo "×", vermelho no hover (#E81123)

## Atalhos de Teclado Sugeridos

```csharp
// No construtor ou Loaded
this.KeyDown += (s, e) => {
    if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
    {
        NewTab_Click(null, null); // Ctrl+T = Nova Aba
    }
    else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
    {
        CloseCurrentTab(); // Ctrl+W = Fechar Aba
    }
    else if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control)
    {
        CycleTabs(); // Ctrl+Tab = Próxima Aba
    }
};
```

## Compatibilidade

✅ **Funciona com:**
- WindowChrome (janela customizada sem bordas)
- Temas claro/escuro
- WebView2 (para módulos LisoFlix)
- Windows 10/11

⚠️ **Limitações:**
- Arrastar abas para reordenar requer implementação adicional
- Abas não podem ser "arrastadas" para fora (criar nova janela) sem código extra

## Arquivos Modificados

1. **MainWindow.xaml**
   - Adicionados estilos ChromeTab*
   - Header redesenhado com TabControl
   - Menu expandido com opções de aba/janela

2. **MainWindow.xaml.cs** (próximo passo)
   - Implementar event handlers: `NewTab_Click`, `CloseTab_Click`, `NewWindow_Click`
   - Modificar handlers de menu para abrir em abas

## Notas

- O TabControl está posicionado no header mas seu conteúdo ainda não está sendo usado
- Para funcionalidade completa, o conteúdo atual (Config Panel + Main Content) deve ser movido para dentro do TabItem
- Isso permite que cada módulo (LisoFlix, XUI-ONE, etc.) tenha sua própria aba
- Mantém a interface limpa e organizada, similar ao Chrome

## Próxima Versão (v1.0.35)

Para versão v1.0.35, sugerir:
1. Mover conteúdo atual para dentro do TabItem
2. Implementar handlers de eventos
3. Adicionar suporte a Ctrl+T, Ctrl+W
4. Testar com múltiplos módulos abertos
5. Ajustar performance com WebView2 em múltiplas abas
