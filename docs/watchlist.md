# Watchlist de jogadores (`tracked_players`)

A watchlist é a lista de jogadores que o pipeline rastreia:

- `price-collection` chama `IPlayerMarketClient.GetPlayerMarketSnapshotAsync(p)` para cada player ativo a cada tick.
- `opportunity-recompute` lê o mesmo conjunto para construir `OpportunityRecomputePlayer` e rodar o scoring.

Antes deste PR a lista era um array estático em `appsettings.json`. Agora ela mora na tabela **`tracked_players`** e tem três fontes de povoamento com precedência bem definida.

## Fontes e precedência

Precedência (menor → maior) no boot:

1. **Seed JSON versionado** (`data/players-catalog.seed.json`) — fica no repo, copiado para o output de Api/Worker. `Source = Seed`.
2. **`appsettings.Jobs.*.Players`** (legacy) — importado apenas para compatibilidade. `Source = AppSettings`. **Deprecated**: mantido no boot só para migrar instalações existentes.
3. **`POST /api/watchlist`** — addicionado por operadores em runtime. `Source = Api`.

Regras aplicadas pelo `WatchlistSeedService`:

- Linhas **`Api`** **nunca** são sobrescritas pelo seed. Apenas `DisplayName` / `Overall` podem ser atualizados pelo seed se estiverem ausentes — `Source`, `AddedAtUtc`, `LastCollectedAtUtc` e `IsActive` são preservados.
- `AppSettings` ganha de `Seed` na deduplicação por `PlayerId` (operador sempre manda).
- Seed é **idempotente**: rodar de novo não re-insere, apenas atualiza `DisplayName`/`Overall` onde fizer sentido.

## Endpoints HTTP

Todos sob `/api/watchlist` (sem auth na V1). Contratos em `src/TradingIntel.Api/Contracts/WatchlistContracts.cs`.

| Método | Rota | Corpo / Query |
| --- | --- | --- |
| `GET` | `/api/watchlist` | `page`, `pageSize` (default 50, max 200), `includeInactive` (default `false`), `source` (`Seed`/`AppSettings`/`Api`), `minOverall` |
| `GET` | `/api/watchlist/{playerId:long}` | — (retorna inativos também) |
| `POST` | `/api/watchlist` | `{ "playerId": long, "displayName": string?, "overall": int? }` → marca como `Source=Api` |
| `DELETE` | `/api/watchlist/{playerId:long}` | — (soft-delete: `IsActive=false`) |

Respostas usam o envelope paginado padrão e o mapper em `src/TradingIntel.Api/Mapping/WatchlistMapper.cs`.

## Seed JSON

O arquivo default fica em `data/players-catalog.seed.json` (copiado para `bin/.../data/` por `<Content>` nos `.csproj` de Api e Worker). Shape:

```jsonc
{
  "version": 1,
  "players": [
    { "playerId": 231747, "displayName": "Kylian Mbappé (base)", "overall": 94 }
  ]
}
```

Configuração (seção `Watchlist`):

| Chave | Default | Efeito |
| --- | --- | --- |
| `Watchlist:CatalogSeedPath` | `data/players-catalog.seed.json` | Caminho relativo ao `AppContext.BaseDirectory` ou absoluto. Vazio = pula o seed JSON. |
| `Watchlist:RequireCatalogSeed` | `false` | Se `true`, boot falha quando o arquivo não existe ou JSON é inválido (útil em produção). |

Comentários e trailing commas são aceitos (`JsonCommentHandling.Skip`, `AllowTrailingCommas = true`).

## Ciclo de boot

1. `Program.cs` (Api/Worker) chama `MigrateTradingIntelDatabase()` (EF Core migrations).
2. Em seguida, `host.Services.SeedWatchlistAsync(configuration)`:
   - Lê as duas seções legacy de `Jobs:*:Players` e converte em `WatchlistSeedEntry(Source=AppSettings)`.
   - `WatchlistSeedService.SeedAsync(legacyEntries)` carrega o JSON, deduplica por `PlayerId`, preserva entradas `Api`, faz upsert em `tracked_players`.
   - Resultado logado como `WatchlistSeed: done. catalog=X appSettings=Y inserted=A updated=B skipped=C`.
3. Jobs do Worker / endpoint de recompute leem `IWatchlistRepository.GetActiveAsync()` a cada tick.

No **Testing environment** (`WebApplicationFactory`) migrate + seed são executados pelo próprio factory depois que o host inicia, porque a conexão SQLite in-memory só existe após `ConfigureTestServices`.

## Descobrindo o `PlayerId`

É o mesmo `eaId` usado pelo FUT.GG (ver `docs/source-futgg-market.md` e `docs/worker.md`). Exemplo rápido:

```bash
# Abra https://www.fut.gg/players/231747-kylian-mbappe/26-231747/
# O número antes do slug é o eaId.
curl -A "Mozilla/5.0" "https://www.fut.gg/api/fut/player-prices/26/231747/?platform=pc"
```

Se o endpoint retornar `currentPrice.price > 0`, o card é tradeável e pode entrar na watchlist.

## Testes

- `tests/TradingIntel.Tests/Infrastructure/Persistence/WatchlistRepositoryTests.cs` — cobre upsert com preservação de `Source`/`AddedAtUtc`, soft-delete, `TouchLastCollectedAsync` e `QueryPagedAsync` contra SQLite real.
- `tests/TradingIntel.Tests/Application/Watchlist/WatchlistSeedServiceTests.cs` — cobre precedência, idempotência, proteção da fonte `Api`, arquivo ausente com `RequireCatalogSeed=false`/`true`, entradas inválidas.
- `PriceCollectionJobTests` e `OpportunityRecomputeJobTests` usam `InMemoryWatchlistRepository` (fake) para cobrir o read-path dos jobs sem SQLite.
- `ApiEndpointsIntegrationTests.Opportunities_recompute_returns_summary` valida que o `POST /api/opportunities/recompute` usa a watchlist persistida (ele depende do seed de `Jobs:OpportunityRecompute:Players` rodado pelo factory).

## Deprecation plan para `Jobs:*:Players`

- Hoje: lidas no boot, importadas idempotentemente para `tracked_players` com `Source=AppSettings`.
- Próximo passo razoável: emitir `warn` no boot quando houver entradas — encorajando a migração para `POST /api/watchlist` ou para o seed JSON.
- Depois: remover o import no boot e deletar as seções dos `appsettings.*.json`. A tabela `tracked_players` continua sendo a fonte de verdade.
