# API HTTP (`TradingIntel.Api`)

Host ASP.NET Core minimal (`WebApplication`). **Sem autenticação** na V1 (uso previsto em `localhost` / rede privada).

## OpenAPI e Swagger

- **Swashbuckle** (`Swashbuckle.AspNetCore`): documento OpenAPI 3 + UI em `/swagger` e JSON em `/swagger/v1/swagger.json`.
- Em **Development**, `Program.cs` registra `UseSwagger` / `UseSwaggerUI`. Em outros ambientes o Swagger fica desligado.

## Problem Details

`AddProblemDetails()` e `UseExceptionHandler()` estão habilitados. Validações de query/body respondem com **Problem Details** (HTTP 400/404) em vez de corpo ad-hoc.

## Paginação

Resposta paginada com envelope comum:

| Campo | Tipo | Notas |
| --- | --- | --- |
| `items` | array | itens da página |
| `page` | int | base 1 |
| `pageSize` | int | default **50**, máximo **200** |
| `totalItems` | int | total após filtros |
| `totalPages` | int | `ceil(totalItems / pageSize)` |

Parâmetros de query: `page`, `pageSize`.

## Contratos HTTP

Os DTOs de resposta vivem em `src/TradingIntel.Api/Contracts/` e os mapeamentos em `src/TradingIntel.Api/Mapping/`. **Nenhum tipo de `TradingIntel.Domain` aparece no JSON** — apenas DTOs estáveis para o painel/consumidores.

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/health` | Health checks ASP.NET (liveness). |
| GET | `/api/jobs/health` | Snapshot em memória de `IJobHealthRegistry` (mesmo contrato usado pelo Worker; em processo só-API as entradas ficam vazias até existir produtor). |
| GET | `/api/sbcs/active` | SBCs com filtros: `category` (substring), `expiresBefore` (UTC), `requiresOverall` (int), `includeExpired` (default `false`), paginação. |
| GET | `/api/market/prices` | `playerId` **obrigatório**; `source` opcional; `from` / `to` (UTC, inclusivos); paginação. |
| GET | `/api/market/listings` | `playerId` **obrigatório**; `from` / `to` (UTC); paginação. |
| GET | `/api/opportunities` | Filtros: `minConfidence`, `minNetMargin`, `playerId`, `detectedAfter` (UTC); paginação. |
| GET | `/api/opportunities/{id}` | Detalhe por `OpportunityId` (GUID) com `reasons` e `suggestions`. |
| POST | `/api/opportunities/recompute` | Corpo opcional `{ "playerIds": [ ... ] }` para filtrar a watchlist em `Jobs:OpportunityRecompute:Players`. **Síncrono** (200 + resumo); sem fila dedicada na V1. |

## Configuração

- `ConnectionStrings:TradingIntel` — SQLite (ou outro provider configurado na Infrastructure).
- `Jobs:OpportunityRecompute` — `Players` (watchlist com `PlayerId`, `Name`, `Overall`) e `StaleAfter` usados pelo recompute e pelo endpoint POST.

## Testes de integração

`WebApplicationFactory<Program>` com SQLite in-memory compartilhado (`TradingIntel.Tests/Api/TradingIntelApiFactory.cs`): migrations aplicadas no startup do host de teste, seed por `IServiceScopeFactory` antes das chamadas HTTP.
