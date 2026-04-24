# Fonte: FUT.GG (market/preços, EA FC 26)

Este documento descreve a superfície pública do **FUT.GG** usada pelo adapter `FutGgMarketClient` (implementação de `IPlayerMarketClient`) e como o payload é normalizado em `PlayerPriceSnapshot` / `MarketListingSnapshot`.

A FUT.GG é a **fonte de market padrão da V1** — selecionada porque expõe um endpoint JSON que responde **sem** desafio de Cloudflare ao `HttpClient` padrão, diferente do FUTBIN (ver `docs/source-futbin.md`).

## Superfícies públicas identificadas

| Superfície | Uso | Observações |
| --- | --- | --- |
| `GET https://www.fut.gg/api/fut/player-prices/<season>/<eaId>/?platform=<p>` | Detalhe completo: `currentPrice`, `liveAuctions`, `completedAuctions`, `history`, `overview`, `momentum`, `priceRange`. | **Fonte primária** do adapter. Retorna 200 OK com `User-Agent` de browser. |
| `GET https://www.fut.gg/api/fut/player-prices/<season>/?ids=<csv>&platform=<p>` | Batch: retorna `currentPrice` resumido por id. | Útil para ticks rápidos com muitos jogadores. Não usado na V1 — o detalhe já resolve preço + listagens. |
| `GET https://www.fut.gg/players/<eaId>-<slug>/` | Página HTML do jogador (catálogo, rating, estatísticas). | Renderizada via TanStack Router (SPA); não serve como fonte de preço. |

Plataformas aceitas em `?platform=`: `pc`, `console`.

## Shape do payload (detail)

```jsonc
{
  "data": {
    "eaId": 231747,
    "currentPrice": {
      "eaId": 231747,
      "platform": "pc",
      "price": 234000,
      "isExtinct": false,
      "isSbc": false,
      "isObjective": false,
      "isUntradeable": false,
      "priceUpdatedAt": "2026-04-23T05:01:20.101886Z"
    },
    "completedAuctions": [
      { "soldDate": "2026-04-23T05:01:29.692630Z", "soldPrice": 235000 }
    ],
    "liveAuctions": [
      { "buyNowPrice": 460000, "endDate": "2026-04-23T05:55:58Z", "startingBid": 459000 },
      { "buyNowPrice": 235000, "endDate": "2026-04-23T05:55:46Z", "startingBid": 24000 }
    ],
    "history": [ { "date": "2026-04-22T00:00:00Z", "price": 220000 } ],
    "priceRange":   { "minPrice": 24000, "maxPrice": 460000 },
    "overview":     { "averageBin": 226000, "cheapestSale": 47500, "discardValue": 646, "yesterdayAverageBin": 222000 },
    "momentum":     { "lowestBin": 47500, "highestBin": 250000, "currentBinMomentum": 92.0, "lastUpdates": [ ... ] }
  }
}
```

Um snapshot real (Mbappé PC) está em `tests/fixtures/futgg/player-prices-mbappe-pc.json` e é consumido pelos testes de regressão.

## Arquitetura do adapter

```
TradingIntel.Application/PlayerMarket/
├── IPlayerMarketClient.cs            # contrato neutro de fonte
└── PlayerMarketSnapshot (record)     # agregado de preços + listagens por fetch

TradingIntel.Infrastructure/FutGg/
├── FutGgApiOptions.cs                # BaseUrl, Season, Platforms, User-Agent
├── FutGgPlayerPricesParser.cs        # JSON  → FutGgPlayerPricesPayload (DTO)
├── FutGgPlayerMapper.cs              # DTO   → PlayerPriceSnapshot / MarketListingSnapshot
└── FutGgMarketClient.cs              # HTTP + raw snapshot + orquestra por plataforma
```

O split (parser vs. mapper vs. client) mantém cada peça testável com fixtures reais sem rede.

## Configuração

Seção `Market:FutGg` em `appsettings.json` (veja também `Market:Source` no `DependencyInjection.cs`):

```jsonc
{
  "Market": {
    "Source": "futgg",                  // "futgg" (default) ou "futbin"
    "FutGg": {
      "BaseUrl": "https://www.fut.gg", // sem trailing slash
      "Season": "26",
      "Platforms": [ "pc" ]             // ["pc"], ["console"] ou ["pc", "console"]
    }
  }
}
```

### Troca de fonte em runtime

`Market:Source` é resolvido uma vez em `AddInfrastructure(IConfiguration)`. Mudanças exigem restart — por design, porque trocar de fonte também troca o shape do `Source` dos snapshots e isso precisa ser consciente.

- `Market:Source = "futgg"` (default) → `FutGgMarketClient` registrado como `IPlayerMarketClient`.
- `Market:Source = "futbin"` → `FutbinMarketClient` registrado (mantido como fallback para quando houver proxy WAF autorizado).

O mesmo valor alimenta `MarketSourceOptions` (em `TradingIntel.Application.PlayerMarket`), que expõe `SourcePrefix` (ex.: `"futgg:"`). O `OpportunityRecomputeService` usa esse prefixo para filtrar `player_price_snapshots` / `market_listing_snapshots` na hora de re-scorar — assim a leitura sempre casa com o que o adapter da fonte ativa escreveu.

## Fluxo por player (tick do `price-collection`)

1. Para cada `platform` em `Market:FutGg:Platforms`:
   1. Monta `GET {BaseUrl}/api/fut/player-prices/{Season}/{eaId}/?platform={platform}` (User-Agent de browser).
   2. Falhas HTTP são logadas como `Warning` e a plataforma é **pulada** (outras continuam, outros players continuam).
   3. Em sucesso, o payload bruto é gravado via `IRawSnapshotStore` com `Source = "futgg:<platform>"` e `PayloadHash` SHA-256.
   4. Parser converte para DTO; mapper deriva um `PlayerPriceSnapshot` (se válido) e N `MarketListingSnapshot` (uma por `liveAuctions[i]` não-expirada e válida).
2. Retorna `PlayerMarketSnapshot` agregando **todas** as plataformas; o Worker persiste price/listings como hoje.

## Normalização

### `PlayerPriceSnapshot`

| Campo | Origem |
| --- | --- |
| `Player` | `PlayerReference` passado pelo chamador (`PlayerId = eaId`, `DisplayName` vindo da watchlist). |
| `Source` | `futgg:<platform>` (ex.: `futgg:pc`, `futgg:console`). |
| `CapturedAtUtc` | UTC do início do fetch (compartilhado entre plataformas do mesmo tick). |
| `BuyNowPrice` | `data.currentPrice.price`. |
| `SellNowPrice` | **null** na V1 (evita inferir "2ª menor BIN" a partir de `liveAuctions` com anúncios spam/outlier; o domínio exige `SellNow >= BuyNow` e ficamos conservadores). |
| `MedianMarketPrice` | `data.overview.averageBin` quando `> 0`; fallback para `currentPrice.price`. |

Skipped (retorna `null` do mapper, log em `Debug`):

- `currentPrice` ausente.
- `isUntradeable == true` (cards SBC/objetivo sem mercado).
- `currentPrice.price <= 0` (geralmente indica extinto/sem oferta).

### `MarketListingSnapshot` (live auctions reais)

Diferente do adapter do FUTBIN (que emite **uma** listagem sintética representando a menor BIN), o FUT.GG entrega auctions reais e o adapter emite **uma por auction viva**:

| Campo | Origem |
| --- | --- |
| `ListingId` | `SHA-256(eaId:platform:endDate.Ticks:buyNowPrice)` truncado em 16 bytes hex → id determinístico no tick. |
| `Player` | mesmo do price snapshot. |
| `Source` | `futgg:<platform>`. |
| `CapturedAtUtc` | UTC do fetch. |
| `StartingBid` | `liveAuctions[].startingBid`. |
| `BuyNowPrice` | `liveAuctions[].buyNowPrice`. |
| `ExpiresAtUtc` | `liveAuctions[].endDate` (parseado como UTC). |

Filtros defensivos antes de materializar (nunca quebra o tick — apenas pula a linha):

- `buyNowPrice <= 0` ou `startingBid <= 0`.
- `buyNowPrice < startingBid` (violaria invariante de domínio).
- `endDate <= capturedAtUtc` (auction já expirada; provavelmente cache stale do upstream).
- Qualquer `ArgumentException` lançada pelo constructor do `MarketListingSnapshot` é capturada e logada em `Debug`.

> Observação: o `ListingId` não é estável **entre ticks** — o mesmo auction pode ser capturado de novo no próximo tick com um `ListingId` diferente se o upstream mudar o `endDate` (raro, mas acontece com re-listagens). Tratamos cada captura como um evento-observação independente, não como atualização de uma entidade viva. Isso é intencional na V1: simplifica a persistência e preserva auditabilidade.

## Auditabilidade (raw-before-normalized)

Para cada plataforma de cada player, antes de retornar o snapshot normalizado o client grava o payload bruto:

- `Source = "futgg:<platform>"` no metadata (diferente do `Source` "futbin" que não quebrava por plataforma).
- `RecordCount = 1`.
- `CorrelationId` = mesmo GUID para todas as plataformas do mesmo player no mesmo tick.
- `PayloadHash` = SHA-256 do corpo.

Se os campos upstream mudarem de nome/shape no futuro, os snapshots normalizados antigos continuam reproduzíveis a partir dos payloads salvos.

## Cobertura de testes

Fixtures em `tests/fixtures/futgg/`:

- `player-prices-mbappe-pc.json` — payload real completo (Mbappé, PC) salvo via `curl` contra o endpoint oficial.
- `player-prices-mbappe-console.json` — mesma resposta para `platform=console`.
- `player-prices-batch-pc.json` — exemplo do endpoint batch (para documentação; não consumido por testes na V1).

Testes em `tests/TradingIntel.Tests/Infrastructure/FutGg/`:

- `FutGgPlayerPricesParserTests` — parsing feliz, payload vazio, shape fora do contrato, resiliência a seções faltantes.
- `FutGgPlayerMapperTests` — mapeamento para `PlayerPriceSnapshot`, uso de `overview.averageBin`, fallback pra `currentPrice.price`, skip de untradeable, emissão de `MarketListingSnapshot` com respeito às invariantes de domínio, filtro de expiradas e filtro de `BIN < startingBid`.

## Limitações conhecidas

- **Sem rate-limit explícito**: o adapter faz 1 request por (player, plataforma) por tick. Com a watchlist pequena da V1 isso é trivial; se escalar, o próximo passo é adotar o endpoint **batch** (`?ids=<csv>`) para resolver preços em uma só chamada e usar o detail só num job secundário (enriquecimento).
- **`SellNowPrice` sempre `null`**: simplificação consciente; `liveAuctions` contém spam (vendedores pedindo `minPrice * 2` para forçar compra). Derivar "2ª menor BIN estável" exige heurística (ex.: 2ª menor BIN com volume mínimo nos últimos N segundos) que fica fora do escopo da V1.
- **Platform per-card**: a watchlist é `PlayerId` + `Overall`, sem `Platform`. O adapter coleta **todas** as plataformas em `Market:FutGg:Platforms` para todos os players da watchlist. Se no futuro uma entrada precisar de plataforma específica, o shape da watchlist precisa evoluir.

## Como descobrir um `eaId`

1. Pesquise o jogador em `https://www.fut.gg/players/` (UI SPA — não há API pública de search hoje).
2. Abra o card desejado. A URL do card especial contém o `eaId`:
   - `https://www.fut.gg/players/231747-kylian-mbappe/26-231747/` → `eaId = 231747` (carta base).
   - `https://www.fut.gg/players/231747-kylian-mbappe/26-67350350/` → `eaId = 67350350` (versão especial — IDs em `67...`, `84...`, `134...` costumam ser TOTS/promos).
3. Valide chamando direto:

   ```bash
   curl -A "Mozilla/5.0" "https://www.fut.gg/api/fut/player-prices/26/<eaId>/?platform=pc"
   ```

   Se retornar `{"data": {...}}` com `currentPrice.price > 0`, o id está correto e tradeável.

> **Importante**: o `PlayerId` usado antes pela V0 era o id do **FUTBIN** (ex.: `21747` = Mbappé no FUTBIN). Esses ids **não** são compatíveis com o `eaId` do FUT.GG. Ao migrar de fonte, troque a watchlist inteira.
