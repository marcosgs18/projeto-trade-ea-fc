# Dashboard

Interface web para navegar a watchlist e as oportunidades do TradingIntel sem
precisar do Swagger UI ou `curl`. Implementada em Blazor Server
(`src/TradingIntel.Dashboard`) e consome a mesma camada de `Application` +
`Infrastructure` que a Api e o Worker — ou seja, lê e escreve no mesmo SQLite
(`tradingintel.db`) e não depende da Api HTTP estar no ar.

> Esta é a V1 do dashboard. A prioridade foi entregar uma tela de operação
> enxuta, sem dependências novas (sem libs de UI/charts). Gráficos, job health
> visual e métricas agregadas ficam para iterações seguintes.

## Como rodar

Porta padrão: **5290**.

```bash
dotnet run --project src/TradingIntel.Dashboard
```

Depois abra <http://localhost:5290>.

Requisitos:

- `tradingintel.db` presente no diretório raiz do repositório (o Worker ou a
  Api normalmente já o criam). Se o arquivo não existir, o Dashboard aplica as
  migrations na primeira subida e semeia a watchlist a partir de
  `data/players-catalog.seed.json`, igual ao comportamento da Api.
- .NET 9 SDK.

Pode ser rodado em paralelo com Api (`5280`) e Worker (processo de fundo).
Todos compartilham o mesmo SQLite via connection string
(`ConnectionStrings:TradingIntel`).

## Rotas

| Rota | Conteúdo |
|---|---|
| `/` | Visão geral com contagens (watchlist ativa, oportunidades, margem > 0, última detecção). |
| `/watchlist` | Tabela de jogadores ativos, formulário de adicionar/reativar, ação de desativar. |
| `/opportunities` | Tabela de oportunidades com filtro por margem líquida mínima, confiança mínima e ocultar stale. |
| `/opportunities/{Id:guid}` | Detalhe de uma oportunidade com `Reasons` e `Suggestions`. |
| `/players/{PlayerId:long}` | Histórico de `PlayerPriceSnapshot` do jogador, com janela configurável (3 h / 24 h / 3 d / 7 d) e filtro por `Source`. |

## Arquitetura e DI

O `Program.cs` do Dashboard é análogo ao da Api:

- `AddRazorComponents().AddInteractiveServerComponents()` — modelo Blazor Web
  do .NET 8/9, render interativo por circuito WebSocket.
- `AddApplication()` + `AddInfrastructure(configuration)` — os mesmos serviços
  usados pela Api e pelo Worker. O Dashboard injeta `IWatchlistRepository`,
  `ITradeOpportunityRepository` e `IPlayerPriceSnapshotRepository` diretamente
  nas páginas Razor via `@inject`.
- `MigrateTradingIntelDatabase()` + `SeedWatchlistAsync()` — migra e semeia na
  boot quando `Environment != "Testing"`. No ambiente `Testing` quem faz isso
  é o `DashboardHostFactory` (ver seção de testes abaixo).

## Comportamento das páginas

### Watchlist (`/watchlist`)

- Formulário de adicionar grava com `WatchlistSource.Api` quando o jogador é
  novo e preserva `Source`/`AddedAtUtc` originais ao reativar uma entrada
  existente — idêntico ao `POST /api/watchlist`. Origem (`Seed`,
  `AppSettings`, `Api`) é mostrada como tag colorida.
- Desativar faz soft-delete (`IsActive = false`). A entrada continua no banco
  para preservar trilha de auditoria.

### Oportunidades (`/opportunities`)

- Filtros:
  - **Margem líquida mínima** e **confiança mínima** são empurrados para o
    `TradeOpportunityListFilter`.
  - **Ocultar stale** é aplicado em memória porque o filtro do repositório
    não expõe `IsStale` (a flag vem do próprio `TradeOpportunityStoredView`).
- Ordena por `DetectedAtUtc` DESC (convenção do repositório). Cada linha tem
  um link para `/opportunities/{Id}`.

### Detalhe da oportunidade (`/opportunities/{Id}`)

- Cards com jogador, compra/venda esperadas, margem líquida (já com taxa de
  5% do EA aplicada), confiança e status `fresca`/`stale`.
- Tabelas com `Reasons` (`Code`, `Message`, `Weight`) e `Suggestions`
  (`Action`, `TargetPrice`, `ValidUntilUtc`), lidas do próprio domínio.
- Link para o histórico do jogador (`/players/{PlayerId}`).

### Histórico do jogador (`/players/{PlayerId}`)

- `IPlayerPriceSnapshotRepository.GetByPlayerPagedAsync` retorna ordenado por
  `CapturedAtUtc` ASC; a UI inverte a lista em memória para mostrar o mais
  recente no topo.
- Campo de `Source` aceita match exato (`futgg:pc`, `futbin:pc`). Deixar
  vazio traz todos os sources da janela selecionada.
- Janela máxima (7 d) com `take: 5000` no repositório cobre até ~2016
  snapshots com o tick padrão do Worker (5 min) — suficiente para V1.

## Testes

`tests/TradingIntel.Tests/Dashboard/` tem:

- `DashboardHostFactory` — `WebApplicationFactory<TradingIntel.Dashboard.Program>`
  com SQLite in-memory, migrations e seed aplicados em `CreateHost`. Mesmo
  padrão do `TradingIntelApiFactory`.
- `DashboardSmokeTests` — 3 rotas felizes (`/`, `/watchlist`,
  `/opportunities`) retornando 200 com o layout renderizado e uma rota
  inexistente retornando 404.

Esses testes são pré-renderização apenas (HTTP GET puro). Interações
client-side (submits, clicks) ficam fora do escopo porque exigiriam um
cliente WebSocket e/ou bUnit.

## Decisões de escopo (V1)

- **Sem libs de UI externas** (MudBlazor, Radzen). CSS próprio mantém o PR
  enxuto e a superfície de dependências pequena.
- **Sem gráficos**. Histórico de preços é uma tabela ordenada por data. Um PR
  futuro pode adicionar uma lib de chart (ApexCharts/Plotly) sem refatorar as
  páginas.
- **Sem job health visual**. `IJobHealthRegistry` é singleton in-memory no
  processo do Worker, logo não é observável a partir do Dashboard. Um
  próximo PR pode consumir `GET /api/jobs/health` via `HttpClient` quando a
  Api estiver rodando lado a lado.
- **Sem autenticação/autorização**. O dashboard é pensado para
  `localhost` enquanto o sistema roda em dev. Expor fora de `localhost`
  exige um PR dedicado (auth, CSRF para POST, HTTPS).

## Solução de problemas

- **"Page not found" ao abrir rotas** — confirme que o Dashboard subiu em
  `5290` e que você está acessando as rotas relativas listadas acima.
- **"Failed to load /app.css" no console do navegador** — `app.UseStaticFiles()`
  deve estar presente no `Program.cs`; verifique se o projeto está sendo
  publicado com `wwwroot/app.css`.
- **Watchlist vazia em qualquer página** — verifique em `/watchlist`. Se
  estiver realmente vazia, rode o seed pela Api/Dashboard (POST
  `/api/watchlist` ou formulário) ou atualize
  `data/players-catalog.seed.json` e reinicie o processo.
- **Histórico de preços sem linhas** — confira o `Source` (vazio mostra
  todos) e aumente a janela. Se mesmo assim ficar vazio, o Worker ainda não
  coletou esse jogador; veja `docs/worker.md`.
