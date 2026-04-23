# Persistência

Este documento descreve a camada de persistência inicial do **TradingIntel**: escolhas, schema, repositórios, migrations e convenções de consulta temporal.

## Visão geral

- **Provider atual (dev)**: SQLite (arquivo `tradingintel.db` na pasta de execução).
- **ORM**: Entity Framework Core 9.
- **Estratégia**: contratos definidos na camada `Application` (ports), implementação SQLite na camada `Infrastructure` (adapters). Trocar o provider (Postgres/SQL Server/etc.) é questão de substituir o registro no `DependencyInjection.AddInfrastructure` + rodar migrations contra o novo destino.

## Contratos (camada Application)

Todos em `src/TradingIntel.Application/Persistence/`:

- `IRawSnapshotStore` (write-only, já existente) — usada pelos adapters para gravar o payload bruto.
- `IRawSnapshotRepository` — consulta raw snapshots por `source` + janela temporal, e "último por fonte".
- `IPlayerPriceSnapshotRepository` — grava em lote e consulta por `playerId` + janela temporal, além de "último por player+source".
- `IMarketListingSnapshotRepository` — grava em lote, consulta por `playerId` + janela, e lookup por `ListingId`.
- `ISbcChallengeRepository` — **upsert por `Id`** (GUID do FUT.GG) e consulta composável (`SbcChallengeQuery`): `ActiveAsOfUtc` (SBCs ativos no instante), `CategoryContains` (substring, case-insensitive) e `MatchesOverall` (SBCs que um overall ajuda a cumprir — filtra por keys `min_team_rating` / `squad_rating`).
- `ITradeOpportunityRepository` — última `TradeOpportunity` por jogador (upsert por `PlayerId`), remoção quando não há edge, marcação de obsolescência por TTL de recomputação.
- `StoredRawSnapshot` — record retornado pelas consultas, preservando `SourceSnapshotMetadata` + payload.

Os repositórios recebem e retornam **modelos de domínio** (`PlayerPriceSnapshot`, `MarketListingSnapshot`) — não expõem tipos de infraestrutura.

## Schema inicial

Tabelas criadas pela migration `InitialCreate`:

### `raw_snapshots`

| Coluna | Tipo | Notas |
| --- | --- | --- |
| `Id` | `TEXT` (GUID) | PK |
| `Source` | `TEXT(64)` | indexado |
| `CapturedAtUtc` | `TEXT` (UTC) | indexado em composto com `Source` |
| `RecordCount` | `INTEGER` | |
| `CorrelationId` | `TEXT(64)` | |
| `PayloadHash` | `TEXT(128)` | indexado para deduplicação |
| `RawPayload` | `TEXT` | payload íntegro |

Índices:
- `ix_raw_snapshots_source_captured_at` (`Source`, `CapturedAtUtc`)
- `ix_raw_snapshots_payload_hash` (`PayloadHash`)

### `player_price_snapshots`

| Coluna | Tipo | Notas |
| --- | --- | --- |
| `Id` | `TEXT` (GUID) | PK |
| `PlayerId` | `INTEGER` | |
| `PlayerDisplayName` | `TEXT(256)` | |
| `Source` | `TEXT(64)` | ex.: `futbin:ps`, `futgg` |
| `CapturedAtUtc` | `TEXT` (UTC) | |
| `BuyNowPrice` | `TEXT` (decimal) | SQLite grava decimal como TEXT para preservar precisão |
| `SellNowPrice` | `TEXT?` | opcional |
| `MedianMarketPrice` | `TEXT` | |

Índices:
- `ix_player_price_snapshots_player_captured_at` (`PlayerId`, `CapturedAtUtc`)
- `ix_player_price_snapshots_source_captured_at` (`Source`, `CapturedAtUtc`)

### `market_listing_snapshots`

| Coluna | Tipo | Notas |
| --- | --- | --- |
| `Id` | `TEXT` (GUID) | PK |
| `ListingId` | `TEXT(256)` | id externo (ou sintético), indexado |
| `PlayerId` | `INTEGER` | |
| `PlayerDisplayName` | `TEXT(256)` | |
| `Source` | `TEXT(64)` | |
| `CapturedAtUtc` | `TEXT` (UTC) | |
| `ExpiresAtUtc` | `TEXT` (UTC) | |
| `StartingBid` | `TEXT` (decimal) | |
| `BuyNowPrice` | `TEXT` (decimal) | |

Índices:
- `ix_market_listing_snapshots_player_captured_at` (`PlayerId`, `CapturedAtUtc`)
- `ix_market_listing_snapshots_listing_id` (`ListingId`)

### `sbc_challenges`

Representa o **estado canônico atual** dos SBCs ativos (upsert por `Id`). O histórico temporal vive nas tabelas `raw_snapshots` (fonte `futgg`), de onde pode ser reconstruído sob demanda.

| Coluna | Tipo | Notas |
| --- | --- | --- |
| `Id` | `TEXT` (GUID) | PK — id estável do FUT.GG |
| `Title` | `TEXT(256)` | |
| `Category` | `TEXT(128)` | indexado |
| `ExpiresAtUtc` | `TEXT?` (UTC) | `NULL` = SBC permanente; indexado |
| `RepeatabilityKind` | `INTEGER` | enum `SbcRepeatabilityKind` |
| `RepeatabilityMaxCompletions` | `INTEGER?` | `NULL` para Unlimited/Unknown |
| `SetName` | `TEXT(256)` | |
| `ObservedAtUtc` | `TEXT` (UTC) | quando o tick do worker observou o SBC |

Índices:
- `ix_sbc_challenges_expires_at` (`ExpiresAtUtc`) — acelera a consulta "ativos agora".
- `ix_sbc_challenges_category` (`Category`).

### `sbc_challenge_requirements`

Tabela filha normalizada (uma linha por requirement). Permite filtrar SBCs por chave + limite direto em SQL.

| Coluna | Tipo | Notas |
| --- | --- | --- |
| `Id` | `TEXT` (GUID) | PK |
| `ChallengeId` | `TEXT` (GUID) | FK → `sbc_challenges.Id`, `ON DELETE CASCADE` |
| `Key` | `TEXT(128)` | ex.: `min_team_rating`, `min_squad_chemistry` |
| `Minimum` | `INTEGER` | |
| `Maximum` | `INTEGER?` | opcional (teto) |
| `Order` | `INTEGER` | preserva a ordem vinda do FUT.GG |

Índices:
- `ix_sbc_challenge_requirements_challenge_id` (`ChallengeId`)
- `ix_sbc_challenge_requirements_key_minimum` (`Key`, `Minimum`) — usado pelo filtro `MatchesOverall`.

A cada tick do `SbcCollectionJob`, os requirements do challenge são **substituídos integralmente** (delete cascateado + reinsert) para refletir fielmente a forma atual do SBC.

### `trade_opportunities`

Última oportunidade de trade conhecida por jogador (uma linha por `PlayerId`). `Reasons` e `Suggestions` são JSON (domínio serializado). O job `OpportunityRecomputeJob` e o endpoint `POST /api/opportunities/recompute` alimentam esta tabela via `IOpportunityRecomputeService`.

| Coluna | Tipo | Notas |
| --- | --- | --- |
| `PlayerId` | `INTEGER` | PK |
| `OpportunityId` | `TEXT` (GUID) | id da `TradeOpportunity` no domínio |
| `PlayerDisplayName` | `TEXT(256)` | |
| `DetectedAtUtc` | `TEXT` (UTC) | |
| `ExpectedBuyPrice` / `ExpectedSellPrice` | `TEXT` (decimal) | |
| `Confidence` | `TEXT` (decimal) | score `[0, 1]` |
| `ReasonsJson` | `TEXT` | array de `OpportunityReason` |
| `SuggestionsJson` | `TEXT` | array de `ExecutionSuggestion` |
| `LastRecomputedAtUtc` | `TEXT` (UTC) | último tick que atualizou a linha |
| `IsStale` | `INTEGER` (0/1) | `true` quando `LastRecomputedAtUtc` fica mais antigo que `StaleAfter` configurado |

Índices: `ix_trade_opportunities_last_recomputed`, `ix_trade_opportunities_is_stale`.

## Consulta temporal

Todos os repositórios oferecem uma janela `[fromUtc, toUtc]` inclusiva para histórico:

```csharp
await playerPriceRepository.GetByPlayerAsync(playerId, fromUtc, toUtc, cancellationToken);
await marketListingRepository.GetByPlayerAsync(playerId, fromUtc, toUtc, cancellationToken);
await rawSnapshotRepository.GetBySourceAsync("futbin", fromUtc, toUtc, cancellationToken);
```

E um atalho para o estado mais recente:

```csharp
await rawSnapshotRepository.GetLatestAsync("futbin", cancellationToken);
await playerPriceRepository.GetLatestForPlayerAsync(playerId, "futbin:ps", cancellationToken);
await playerPriceRepository.GetLatestFutbinPriceForPlayerAsync(playerId, cancellationToken);
```

Os resultados vêm ordenados por `CapturedAtUtc` (crescente em janelas, decrescente em "latest"). As colunas são indexadas por `(PlayerId, CapturedAtUtc)` / `(Source, CapturedAtUtc)`, o que mantém queries eficientes mesmo com milhões de snapshots.

## Consulta de SBCs

`ISbcChallengeRepository.QueryAsync` aceita um `SbcChallengeQuery` com três filtros opcionais combinados com AND (resultados ordenados por `ExpiresAtUtc` crescente, nulls ao fim, desempate por `Title`):

```csharp
// SBCs ativos agora que um card overall 85 ajuda a cumprir,
// filtrando pela categoria "upgrades":
await sbcRepository.QueryAsync(new SbcChallengeQuery
{
    ActiveAsOfUtc = DateTime.UtcNow,
    CategoryContains = "upgrades",
    MatchesOverall = 85,
}, cancellationToken);
```

- `ActiveAsOfUtc`: retorna apenas challenges com `ExpiresAtUtc IS NULL` **ou** `ExpiresAtUtc > nowUtc` (permanentes contam como ativos).
- `CategoryContains`: substring case-insensitive via SQL `LIKE`.
- `MatchesOverall`: SBCs com pelo menos um requirement com chave ∈ `SbcChallengeQuery.TeamRatingRequirementKeys` (`min_team_rating`, `squad_rating`, case-insensitive) e `Minimum <= overall`.

## Migrations

- **Tool**: `dotnet-ef` 9.0.0 (instalar globalmente: `dotnet tool install --global dotnet-ef --version 9.0.0`).
- **Gerar**: `dotnet ef migrations add <Nome> --project src/TradingIntel.Infrastructure --startup-project src/TradingIntel.Infrastructure --output-dir Persistence/Migrations`.
- **Aplicar em dev**: `dotnet ef database update --project src/TradingIntel.Infrastructure --startup-project src/TradingIntel.Infrastructure`.
- **Reprodutibilidade**: o design-time é via `TradingIntelDbContextFactory` (connection string isolada `tradingintel-design.db`), então gerar migrations nunca depende do banco real.
- **Runtime**: apps (Api, Worker) leem a connection string de `ConnectionStrings:TradingIntel` na `IConfiguration`; sem config, caem para `Data Source=tradingintel.db` (dev).

Para aplicar migrations automaticamente no startup de um host, chamar `dbContext.Database.Migrate()` dentro do `Program.cs` (intencionalmente não é feito por padrão para manter separação entre esquema e aplicação).

## Testes de integração

Os testes ficam em `tests/TradingIntel.Tests/Infrastructure/Persistence/` e usam **SQLite in-memory com conexão compartilhada** (`PersistenceTestFixture`):

- Uma `SqliteConnection("DataSource=:memory:")` é aberta e mantida viva durante a fixture, garantindo que o banco persiste entre `DbContext`s.
- A fixture roda `Database.Migrate()` para aplicar a mesma migration do runtime — validando o schema real, não só o model.
- Cada teste cria um `DbContext` novo pela fixture, simulando o ciclo de vida scoped de produção.

Cobertura atual:
- Gravação + consulta por janela temporal (raw, price, listing).
- "Latest" por fonte e por player/source.
- Idempotência de `AddRangeAsync` vazio.
- `UTC kind` preservado no round-trip.
- SBCs: upsert insere, upsert é idempotente por `Id` e substitui requirements; `QueryAsync` filtra por `ActiveAsOfUtc`, `CategoryContains` e `MatchesOverall`; `UpsertRangeAsync` vazio é no-op.
- Oportunidades: `ITradeOpportunityRepository` upsert/delete/`MarkStaleWhereLastRecomputedBeforeAsync`; integração cobre remoção após recompute sem edge.

## Troca de provider

Para trocar SQLite por outro provider (ex.: Postgres):

1. Adicionar o pacote EF Core correspondente (`Npgsql.EntityFrameworkCore.PostgreSQL`).
2. Trocar `options.UseSqlite(...)` por `options.UseNpgsql(...)` em `DependencyInjection.AddInfrastructure`.
3. Gerar uma nova migration (ou manter migrations específicas por provider numa pasta `Migrations/Postgres`).
4. Ajustar `decimal` columns (em Postgres podem voltar ao tipo nativo `numeric`).

Como os **repositórios nunca retornam entidades EF**, código de domínio/aplicação não precisa de nenhuma mudança.
