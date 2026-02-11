# Indicador Visual de Atualização Disponível v1.0.42

## Descrição da Funcionalidade

Agora o campo "Versão atual" no rodapé do aplicativo fica **vermelho e em negrito** quando há uma nova versão disponível para download, alertando visualmente o usuário.

## Como Funciona

### 1. Verificação Automática ao Iniciar
- Ao abrir o aplicativo, ele verifica silenciosamente se há atualizações disponíveis
- Não mostra nenhuma mensagem ou popup durante essa verificação
- O processo é totalmente em background

### 2. Indicador Visual
Quando uma nova versão é detectada:
- ✅ O texto "Versão atual: vX.X.X" fica **vermelho** (cor #E50914)
- ✅ O texto fica em **negrito**
- ✅ A mensagem de status mostra: "Nova versao disponivel: vX.X.X"

Quando o aplicativo está atualizado:
- ✅ O texto fica na cor padrão do tema (preto ou branco)
- ✅ Fonte normal (sem negrito)

### 3. Verificação Manual
- O botão "Verificar Atualizações" continua funcionando normalmente
- Ao verificar manualmente, também atualiza o indicador visual
- Se cancelar a atualização, o indicador permanece vermelho

## Implementação Técnica

### XAML (MainWindow.xaml)

```xml
<TextBlock Grid.Column="1" Text="{Binding CurrentVersionText}" 
           VerticalAlignment="Center" Margin="0,0,8,0" 
           FontWeight="SemiBold" FontSize="11">
    <TextBlock.Style>
        <Style TargetType="TextBlock">
            <Setter Property="Foreground" Value="{DynamicResource BaseTextBrush}"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsUpdateAvailable}" Value="True">
                    <Setter Property="Foreground" Value="#E50914"/>
                    <Setter Property="FontWeight" Value="Bold"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </TextBlock.Style>
</TextBlock>
```

### C# (MainWindow.xaml.cs)

#### Propriedade adicionada:
```csharp
private bool _isUpdateAvailable = false;

public bool IsUpdateAvailable
{
    get => _isUpdateAvailable;
    set { _isUpdateAvailable = value; OnPropertyChanged(nameof(IsUpdateAvailable)); }
}
```

#### Método de verificação silenciosa:
```csharp
private async System.Threading.Tasks.Task CheckForUpdatesSilentAsync()
{
    try
    {
        var currentVersion = GetCurrentAppVersion();
        
        // Verifica no manifesto
        var manifest = await GetLatestUpdateManifestAsync();
        if (manifest != null && IsNewerRelease(manifest.Version, currentVersion))
        {
            IsUpdateAvailable = true;
            StatusMessage = $"Nova versao disponivel: {manifest.Version}";
            return;
        }

        // Verifica nas releases
        var latest = await GetLatestInstallableReleaseAsync();
        if (latest != null && IsNewerRelease(latest.TagName, currentVersion))
        {
            IsUpdateAvailable = true;
            StatusMessage = $"Nova versao disponivel: {latest.TagName}";
            return;
        }

        IsUpdateAvailable = false;
    }
    catch
    {
        IsUpdateAvailable = false;
    }
}
```

#### Chamada no construtor:
```csharp
// Verifica atualizações silenciosamente ao iniciar
_ = CheckForUpdatesSilentAsync();
```

## Fluxo de Funcionamento

```
[Inicia Aplicativo]
    ↓
[Chama CheckForUpdatesSilentAsync()]
    ↓
[Verifica update.json no GitHub]
    ↓
[Compara versões]
    ↓
    ├── Versão nova encontrada?
    │       ├── SIM → IsUpdateAvailable = true
    │       │           → Texto fica VERMELHO
    │       │           → Status: "Nova versao disponivel"
    │       │
    │       └── NÃO → IsUpdateAvailable = false
    │                   → Texto fica NORMAL
    │
    └── [Usuário clica em "Verificar Atualizações"]
                ↓
        [Mesma verificação]
                ↓
        [Atualiza IsUpdateAvailable]
```

## Benefícios

1. **Alerta Visual Imediato:** Usuário sabe assim que abre o app que há novidade
2. **Não Intrusivo:** Não mostra popups ou interrompe o uso
3. **Automático:** Funciona sem intervenção do usuário
4. **Claro:** A cor vermelha chama atenção de forma eficaz

## Versão

**v1.0.42** - Indicador visual de atualização disponível
**Arquivos modificados:**
- MainWindow.xaml (Style do TextBlock)
- MainWindow.xaml.cs (Propriedade IsUpdateAvailable e método CheckForUpdatesSilentAsync)
- MeuGestorVODs.csproj (versão)
- update.json (notas de release)

## Notas

- A verificação é feita em background e não bloqueia a interface
- Se houver erro na verificação (sem internet, etc.), o indicador permanece normal
- O indicador é atualizado tanto na verificação automática quanto na manual
