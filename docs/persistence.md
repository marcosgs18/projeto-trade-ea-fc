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
```

Os resultados vêm ordenados por `CapturedAtUtc` (crescente em janelas, decrescente em "latest"). As colunas são indexadas por `(PlayerId, CapturedAtUtc)` / `(Source, CapturedAtUtc)`, o que mantém queries eficientes mesmo com milhões de snapshots.

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

Cobertura atual (9 testes):
- Gravação + consulta por janela temporal (raw, price, listing).
- "Latest" por fonte e por player/source.
- Idempotência de `AddRangeAsync` vazio.
- `UTC kind` preservado no round-trip.

## Troca de provider

Para trocar SQLite por outro provider (ex.: Postgres):

1. Adicionar o pacote EF Core correspondente (`Npgsql.EntityFrameworkCore.PostgreSQL`).
2. Trocar `options.UseSqlite(...)` por `options.UseNpgsql(...)` em `DependencyInjection.AddInfrastructure`.
3. Gerar uma nova migration (ou manter migrations específicas por provider numa pasta `Migrations/Postgres`).
4. Ajustar `decimal` columns (em Postgres podem voltar ao tipo nativo `numeric`).

Como os **repositórios nunca retornam entidades EF**, código de domínio/aplicação não precisa de nenhuma mudança.
