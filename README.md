# MEU GESTOR DE VODS v1.0

Aplicativo Windows para carregar listas IPTV no formato M3U, filtrar VODs e baixar os itens selecionados.

## Proposta do projeto

Esta versao segue a proposta de manter a experiencia simples da ferramenta original e evoluir com base moderna em C#/.NET 8:

- carregar URL M3U rapidamente
- listar e filtrar VODs
- selecionar em lote e baixar
- acompanhar progresso dos downloads
- verificar atualizacoes no GitHub

## Status atual (v1.0)

Funcionalidades implementadas:

- carregamento de playlist M3U por URL
- parse de `#EXTINF` com nome, grupo e link
- busca/filtro em tempo real
- selecao em massa (selecionar/desmarcar)
- download dos itens selecionados com barra de progresso
- escolha da pasta de destino
- botao "Verificar Atualizacoes" abrindo `releases/latest`

## Como usar

1. Informe a URL M3U no campo `URL M3U`.
2. Clique em `Carregar`.
3. Use o filtro para localizar os VODs.
4. Marque os itens desejados.
5. Clique em `Baixar Selecionados`.
6. Acompanhe o andamento no painel de downloads.

## Download da versao

- Releases: `https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases`
- Ultima versao: `https://github.com/wesleiandersonti/MEU_GESTOR_DE_VODS/releases/latest`

## Requisitos

- Windows 10/11
- .NET 8 (na execucao local)

## Build local

```bash
dotnet publish -c Release -r win-x64 --self-contained -o output
```

Executavel gerado em `output/`.

## CI/CD

O projeto usa GitHub Actions em `.github/workflows/build.yml` para compilar no Windows e publicar artifact automaticamente.

## Estrutura principal

- `MainWindow.xaml`: interface
- `MainWindow.xaml.cs`: logica da UI
- `Services.cs`: parse M3U e servicos de download
- `MeuGestorVODs.csproj`: configuracao do projeto
