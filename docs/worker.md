# Worker de coleta (`TradingIntel.Worker`)

Host genérico (`Microsoft.Extensions.Hosting`) que agenda e executa jobs de coleta periódica. Cada job é um `BackgroundService` baseado em `ScheduledJob` e vive isoladamente: uma falha em um job **não** derruba o host nem afeta os outros.

## Jobs registrados na V1

| Job | Nome interno | Fonte | Persistência |
| --- | --- | --- | --- |
| SBC collection | `sbc-collection` | `IFutGgSbcClient` | Raw (client via `IRawSnapshotStore`) + normalizado `ISbcChallengeRepository`. |
| Price collection | `price-collection` | `IPlayerMarketClient` (FUT.GG por padrão; ver `Market:Source`) | Raw (client) + `IPlayerPriceSnapshotRepository` / `IMarketListingSnapshotRepository`. |
| Opportunity recompute | `opportunity-recompute` | `IOpportunityRecomputeService` (preços da fonte ativa, SBCs, demanda, `ITradeScoringService`) | `ITradeOpportunityRepository` + marcação `IsStale` por `StaleAfter`. |

> **Invariante "raw antes de normalizado"**: os clients (`FutGgSbcClient`, `FutGgMarketClient`, `FutbinMarketClient`) gravam o payload bruto via `IRawSnapshotStore` **antes** de retornar os objetos parseados. Logo, quando o Worker recebe o snapshot para persistir o normalizado, o raw já está salvo. O Worker não precisa (e não deve) gravar o raw novamente.

### Seleção de fonte de market (`Market:Source`)

A fonte concreta por trás de `IPlayerMarketClient` é escolhida em DI pela chave `Market:Source`:

| Valor | Client registrado | Estado |
| --- | --- | --- |
| `futgg` (default) | `FutGgMarketClient` | Recomendado. API JSON pública, sem Cloudflare no endpoint de preços. |
| `futbin` | `FutbinMarketClient` | Mantido como fallback para quando houver proxy WAF autorizado. |

A configuração do FUT.GG fica em `Market:FutGg` (BaseUrl, Season, Platforms). Detalhes em `docs/source-futgg-market.md`.

> **Importante**: o mesmo valor de `Market:Source` é também o filtro de leitura usado pelo `OpportunityRecomputeService` (ele resolve o prefixo a partir de `MarketSourceOptions.SourcePrefix`, p. ex. `"futgg:"`). Então **a fonte que escreve e a fonte que lê são sempre a mesma** — trocar `Market:Source` exige restart e o pipeline volta a casar escrita ↔ leitura sem código adicional.

## Arquitetura

```
TradingIntel.Application
└── JobHealth/                         # registry de health dos jobs (compartilhado com a API)
    ├── IJobHealthRegistry.cs
    ├── InMemoryJobHealthRegistry.cs
    └── JobHealthSnapshot.cs

TradingIntel.Worker
├── Jobs/
│   ├── JobScheduleOptions.cs          # knobs bound do config (Enabled, Interval, …)
│   ├── ScheduledJob.cs                # base abstrata (PeriodicTimer + backoff + health)
│   ├── TickResult.cs                  # resultado classificado (Success / Failure / Cancelled)
│   ├── SbcCollectionJob.cs            # job 1
│   ├── PriceCollectionJob.cs          # job 2
│   ├── OpportunityRecomputeJob.cs     # job 3 — score + persistência
│   ├── PriceCollectionOptions.cs      # watchlist estática para o job de preço
│   └── OpportunityRecomputeOptions.cs # watchlist + StaleAfter + agendamento
├── WorkerServiceCollectionExtensions.cs  # AddCollectionJobs(IConfiguration)
└── Program.cs                         # Host genérico
```

### Ciclo de vida de um tick

1. `ExecuteAsync` espera o `InitialDelay` e entra em loop enquanto o `stoppingToken` não é cancelado.
2. Para cada iteração:
   - Cria um **scope DI** (`IServiceScopeFactory.CreateAsyncScope`) para resolver dependências *scoped* como `DbContext` e repositórios.
   - Chama `ExecuteTickAsync(scoped, ct)` — implementado pelo job concreto.
   - Classifica o resultado em `TickResult { Success | Failure | Cancelled }`.
   - Em `Success`: reset de `ConsecutiveFailures`, backoff volta para `InitialBackoff`, health é marcado com `LastSuccessUtc` e duração.
   - Em `Failure`: incrementa `ConsecutiveFailures`, multiplica backoff por `BackoffMultiplier` até `MaxBackoff` como teto, registra `LastFailureUtc`, mensagem e duração.
   - Em `Cancelled` (apenas se `stoppingToken` foi cancelado durante o tick): sai limpo, sem log de erro e sem atualizar health.
3. Grava `NextTickUtc` no registry, aguarda (`Task.Delay`) o `Interval` (sucesso) ou `CurrentBackoff` (falha) respeitando o `stoppingToken`.

### Garantias

- **Isolamento de falhas**: `try/catch` por tick. Exceções nunca sobem para `ExecuteAsync`, então o host não descarta o hosted service.
- **Cancellation em toda a chain**: todas as chamadas assíncronas recebem o `CancellationToken` do tick/host.
- **Scoped services**: `DbContext` e repositórios são resolvidos dentro do scope do tick; nunca são cacheados em campos do `BackgroundService` (que é singleton).

## Configuração

Cada job lê sua seção em `appsettings.json` (bind via `IOptions<TOptions>`).

```jsonc
{
  "ConnectionStrings": {
    "TradingIntel": "Data Source=tradingintel.db"
  },
  "Jobs": {
    "SbcCollection": {
      "Enabled": true,
      "InitialDelay": "00:00:05",
      "Interval": "00:15:00",
      "InitialBackoff": "00:00:30",
      "MaxBackoff": "00:30:00",
      "BackoffMultiplier": 2.0
    },
    "PriceCollection": {
      "Enabled": true,
      "InitialDelay": "00:00:10",
      "Interval": "00:05:00",
      "InitialBackoff": "00:00:30",
      "MaxBackoff": "00:30:00",
      "BackoffMultiplier": 2.0,
      "Players": [
        { "PlayerId": 21747, "Name": "Kylian Mbappé", "Overall": 94 }
      ]
    },
    "OpportunityRecompute": {
      "Enabled": true,
      "InitialDelay": "00:00:15",
      "Interval": "00:05:00",
      "InitialBackoff": "00:00:30",
      "MaxBackoff": "00:30:00",
      "BackoffMultiplier": 2.0,
      "StaleAfter": "00:15:00",
      "Players": [
        { "PlayerId": 21747, "Name": "Kylian Mbappé", "Overall": 94 }
      ]
    }
  }
}
```

- `Enabled=false` mantém o job instanciado mas ele retorna sem agendar (útil pra desligar só uma fonte sem remover do DI).
- O roster de players do `PriceCollection` é **estático** em V1. Decisão registrada: prioriza determinismo e testabilidade enquanto não há ingestão de catálogo que permita descoberta dinâmica.
- Em `appsettings.Development.json` use intervalos menores para observar o ciclo sem esperar.

## Watchlist de jogadores (`Jobs:*:Players`)

Os jobs `price-collection` e `opportunity-recompute` operam sobre uma lista **estática** de jogadores configurada por hosting app (`Jobs:PriceCollection:Players` no Worker, `Jobs:OpportunityRecompute:Players` no Worker **e** na API — esta última é consumida pelo `POST /api/opportunities/recompute` quando o body vem sem `playerIds`).

### Shape da entrada

```jsonc
{
  "PlayerId": 231747,         // obrigatório: FUT.GG eaId (long). Veja docs/source-futgg-market.md.
  "Name": "Kylian Mbappé",    // opcional: rótulo humano usado apenas em logs
  "Overall": 94               // opcional: overall do card; usado pelo scoring
}
```

- `PlayerId` é a única chave funcional. Desde a migração `Market:Source = futgg` (V1), `PlayerId` é o **`eaId` do FUT.GG**, não o id do FUTBIN — os dois sistemas usam espaços de id incompatíveis. Ex.: Mbappé base = `231747` no FUT.GG vs. `21747` no FUTBIN.
- `Name` não é normalizado nem validado; serve só para debug de logs (`price-collection failed to collect player 231747 (Kylian Mbappé); ...`).
- `Overall` é opcional; quando ausente, `opportunity-recompute` incrementa o contador `skippedOverall` e o jogador é ignorado no tick (a recompute atual exige o rating para casar contra requisitos de SBCs).

### Como descobrir o `PlayerId` (eaId do FUT.GG)

1. Abra o jogador em `https://www.fut.gg/players/<eaId>-<slug>/<season>-<eaId>/`.
2. O `<eaId>` na URL é exatamente o valor de `PlayerId`. Exemplos:
   - `https://www.fut.gg/players/231747-kylian-mbappe/26-231747/` → `231747` (carta base).
   - `https://www.fut.gg/players/231747-kylian-mbappe/26-67350350/` → `67350350` (versão especial/promo).
3. Valide com um curl:

   ```bash
   curl -A "Mozilla/5.0" "https://www.fut.gg/api/fut/player-prices/26/<eaId>/?platform=pc"
   ```

   Se retornar `{"data": {"currentPrice": { "price": N, ... }}}` com `N > 0`, o id está correto e tradeável. Ver detalhes e schema completo em `docs/source-futgg-market.md`.

### Exemplo funcional (watchlist V1)

Snippet usado em `appsettings.Development.json` após a migração para FUT.GG:

```jsonc
"PriceCollection": {
  "Players": [
    { "PlayerId": 231747,   "Name": "Kylian Mbappé (base)",        "Overall": 94 },
    { "PlayerId": 67350350, "Name": "Mbappé Alt (FUT.GG special)", "Overall": 94 }
  ]
}
```

A mesma lista serve para `Jobs:OpportunityRecompute:Players` — manter as duas sincronizadas garante que o recompute tem preço para casar com scoring.

### Comportamento esperado por tick

- **`price-collection`**: itera pela lista, chama `IPlayerMarketClient.GetPlayerMarketSnapshotAsync(playerRef)` por entrada. Com FUT.GG, uma coleta por player resulta em: 1 `PlayerPriceSnapshot` por plataforma em `Market:FutGg:Platforms` + N `MarketListingSnapshot` vindos de `liveAuctions` (uma entry por auction viva). Uma falha por player é logada como `Warning` e o tick continua; o tick só falha (com backoff) se **todos** os players falharem.
- **`opportunity-recompute`**: constrói `PlayerReference` a partir das linhas, resolve preço mais recente via `IPlayerPriceSnapshotRepository`, casa contra SBCs ativos e aciona `ITradeScoringService.Score()`. Contadores emitidos no log final do tick:

  ```
  OpportunityRecompute: done. upserted=<n> removedNoEdge=<n> skippedOverall=<n> skippedPrice=<n> staleMarked=<n>
  ```

  - `skippedPrice` aumenta quando não há preço recente para o player (ex.: primeiro run antes do `price-collection` ter rodado).
  - `skippedOverall` aumenta quando `Overall` é `null`.
  - `staleMarked` é o número de `trade_opportunities` que foram marcados `IsStale=true` neste tick por não terem sido re-scored dentro de `StaleAfter`.

### Fonte alternativa: FUTBIN (Cloudflare)

`www.futbin.com` responde `HTTP 403` a clientes HTTP "nus" por causa de Cloudflare. Por isso a V1 **não usa FUTBIN por padrão** — o adapter `FutbinMarketClient` permanece registrado e selecionável via `Market:Source = "futbin"`, mas só é utilizável atrás de um proxy WAF autorizado (ver `docs/source-futbin.md`). O `sbc-collection` **não** é afetado: usa `r.jina.ai` como fachada pública e coleta os SBCs normalmente.

### Watchlist vazia

Com `Players: []` (padrão em `appsettings.Development.json` antes da configuração manual):

- `price-collection` loga `warn: price-collection has no players configured; nothing to collect.` e conclui o tick como `Success` sem tocar o banco.
- `opportunity-recompute` loga `OpportunityRecompute: no players to score.` e conclui `Success` sem tocar `trade_opportunities`.

Esse é o estado "neutro" usado em CI e fica sempre verde — o primeiro valor só aparece depois que a watchlist é populada e o Futbin volta a responder 200.

## Health interno

`IJobHealthRegistry` é singleton em memória; expõe por job:

| Campo | Significado |
| --- | --- |
| `LastSuccessUtc` | Último instante em que o tick concluiu sem erro. |
| `LastSuccessDuration` | Duração wall-clock do último sucesso. |
| `LastFailureUtc` | Último instante em que o tick falhou. |
| `LastFailureDuration` | Duração até o erro ser disparado. |
| `LastFailureMessage` | Mensagem da exceção (sem stack). |
| `ConsecutiveFailures` | Reseta em 0 a cada sucesso. |
| `NextTickUtc` | Instante calculado do próximo tick (`Interval` ou backoff atual). |

A API expõe esse registry em `GET /api/jobs/health` (ver `docs/api.md`). **Ressalva importante**: `InMemoryJobHealthRegistry` é `Singleton` **em memória por processo**, então a API só vê os jobs registrados **no próprio processo da API** (hoje, apenas o tick que `POST /api/opportunities/recompute` dispara — veja observação abaixo). Os jobs do Worker rodam em outro processo e o registry deles não é compartilhado. Um backend persistido (Redis / tabela `job_health`) é o próximo passo se/quando a API precisar enxergar a saúde do Worker em tempo real.

## Logs estruturados

Todos os logs usam `ILogger<T>` com placeholders nomeados (sem Serilog — ver AGENTS.md).

Exemplos:

- `"{Job} starting. interval={IntervalMs}ms initialDelay={InitialDelayMs}ms maxBackoff={MaxBackoffMs}ms"`
- `"{Job} tick succeeded in {ElapsedMs}ms."`
- `"{Job} tick failed in {ElapsedMs}ms. consecutiveFailures={ConsecutiveFailures} currentBackoffMs={BackoffMs}"`
- `"{Job} next tick in {DelayMs}ms at {NextTickUtc:O} (status={Status})."`
- `"price-collection tick summary. collectedPlayers={CollectedPlayers}/{TotalPlayers} prices={PriceCount} listings={ListingCount} perPlayerFailures={PerPlayerFailures}"`

Erros por player no `PriceCollectionJob` saem como `Warning` (o tick como um todo ainda pode ser bem-sucedido). Só disparamos `Error` do tick quando **todos** os players da watchlist falharam.

## Rodando localmente

```bash
dotnet run --project src/TradingIntel.Worker
```

- Garanta que existe a base SQLite (`tradingintel.db`); se não existir, rode as migrations em `TradingIntel.Infrastructure` ou ajuste `ConnectionStrings:TradingIntel`.
- Para dev rápido: use `appsettings.Development.json` com `Interval` curto e `Players: []` para não bater em produção externa.
- Para ver um job rodando isolado, desabilite o outro com `Jobs:<Name>:Enabled=false`.

## Testes

Os testes cobrem (ver `tests/TradingIntel.Tests/Worker/`):

- Tick bem-sucedido persiste dados normalizados e marca health como sucesso.
- Falha do client é capturada, incrementa `ConsecutiveFailures`, avança o backoff e registra health.
- Cancelamento durante o fetch devolve `TickStatus.Cancelled`, sem log de erro e sem marcar failure/success.
- Falhas consecutivas param no teto `MaxBackoff` (não crescem indefinidamente).
- Sucesso após falhas reseta backoff e `ConsecutiveFailures` (mas preserva `LastFailureUtc` para diagnóstico histórico).
- `PriceCollectionJob`: uma falha por player é logada como warning e o tick continua com os demais; só vira falha do tick se todos falharem.
- `InMemoryJobHealthRegistry` persiste estado correto e retorna snapshot isolado por job.

Os testes não sobem o `Host`: chamam `ScheduledJob.RunTickAsync(ct)` diretamente, usando `IServiceScopeFactory` construído a partir de um `ServiceCollection` mínimo e mocks manuais das interfaces (padrão do repo — nada de Moq).

## Extensão

Para adicionar um novo job:

1. Criar `FooCollectionOptions : JobScheduleOptions` (adicione campos específicos).
2. Criar `FooCollectionJob : ScheduledJob` e implementar `ExecuteTickAsync(IServiceProvider scoped, ct)`.
3. Registrar em `WorkerServiceCollectionExtensions.AddCollectionJobs`:
   - `services.AddOptions<FooCollectionOptions>().Bind(configuration.GetSection("Jobs:Foo"))`
   - `services.AddHostedService<FooCollectionJob>()`
4. Adicionar a seção correspondente em `appsettings.json`.
5. Cobrir com testes no mesmo padrão (tick feliz, falha, cancelamento, backoff cap).
