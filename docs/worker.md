# Worker de coleta (`TradingIntel.Worker`)

Host genérico (`Microsoft.Extensions.Hosting`) que agenda e executa jobs de coleta periódica. Cada job é um `BackgroundService` baseado em `ScheduledJob` e vive isoladamente: uma falha em um job **não** derruba o host nem afeta os outros.

## Jobs registrados na V1

| Job | Nome interno | Fonte | Persistência |
| --- | --- | --- | --- |
| SBC collection | `sbc-collection` | `IFutGgSbcClient` | Raw (client via `IRawSnapshotStore`) + normalizado `ISbcChallengeRepository`. |
| Price collection | `price-collection` | `IFutbinMarketClient` | Raw (client) + `IPlayerPriceSnapshotRepository` / `IMarketListingSnapshotRepository`. |
| Opportunity recompute | `opportunity-recompute` | `IOpportunityRecomputeService` (preços Futbin, SBCs, demanda, `ITradeScoringService`) | `ITradeOpportunityRepository` + marcação `IsStale` por `StaleAfter`. |

> **Invariante "raw antes de normalizado"**: os clients `FutGgSbcClient` e `FutbinMarketClient` gravam o payload bruto via `IRawSnapshotStore` **antes** de retornar os objetos parseados. Logo, quando o Worker recebe o snapshot para persistir o normalizado, o raw já está salvo. O Worker não precisa (e não deve) gravar o raw novamente.

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
        { "PlayerId": 12345, "Name": "Example Player" }
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
        { "PlayerId": 12345, "Name": "Example Player", "Overall": 84 }
      ]
    }
  }
}
```

- `Enabled=false` mantém o job instanciado mas ele retorna sem agendar (útil pra desligar só uma fonte sem remover do DI).
- O roster de players do `PriceCollection` é **estático** em V1. Decisão registrada: prioriza determinismo e testabilidade enquanto não há ingestão de catálogo que permita descoberta dinâmica.
- Em `appsettings.Development.json` use intervalos menores para observar o ciclo sem esperar.

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

Hoje o registry não é exposto via HTTP — pode ser consumido em testes de diagnóstico ou numa futura integração com `TradingIntel.Api` (ex.: endpoint `/health/jobs`).

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
