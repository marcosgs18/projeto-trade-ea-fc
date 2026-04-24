# Fonte: FUTBIN (EA FC 26) — fallback

Este documento descreve as superfícies públicas do **FUTBIN** utilizadas pelo adapter `FutbinMarketClient` (implementação de `IPlayerMarketClient`) e como a coleta é estruturada para produzir snapshots normalizados.

> **Status na V1**: `FutbinMarketClient` é **fallback**. A fonte padrão de market passou a ser **FUT.GG** (`Market:Source = "futgg"`) porque o endpoint JSON da FUT.GG retorna 200 sem Cloudflare. O FUTBIN só fica operacional atrás de um proxy WAF autorizado — ver "Cloudflare e estratégia de coleta" abaixo. Para trocar de fonte, ajuste `Market:Source` (detalhes em `docs/source-futgg-market.md`).

## Superfícies públicas identificadas

| Superfície | Uso | Observações |
| --- | --- | --- |
| `GET https://www.futbin.com/<year>/player/<id>/<slug>` | Página HTML do jogador (LCPrice, faixas, recentes). | Protegida por Cloudflare; payload via JS hidratado. |
| `GET https://www.futbin.com/<year>/playerPrices?player=<id>` | Preços por plataforma, em JSON. | Fonte primária deste adapter. Cloudflare exige cliente autorizado. |
| `GET https://www.futbin.com/<year>/market` | Market Index e tendências agregadas. | Útil para visões macro futuras. |
| `GET https://www.futbin.com/<year>/popular` | Populares por versão. | Útil para universo de coleta. |

O adapter atual foca na superfície `playerPrices` por ser a representação mais estável/normalizada de preço por plataforma.

## Shape do payload `playerPrices`

Resposta JSON indexada pelo `playerId`:

```json
{
  "<playerId>": {
    "prices": {
      "ps":   { "LCPrice": "1,500,000", "LCPrice2": "...", "MinPrice": "...", "MaxPrice": "...", "PRP": "85", "updated": "3 minutes ago" },
      "xbox": { "LCPrice": "1,480,000", "...": "..." },
      "pc":   { "LCPrice": "1,650,000", "...": "..." }
    }
  }
}
```

Convenções:

- `LCPrice` (Lowest BIN) e `LCPrice2` são a menor e a 2ª menor BIN ativas observadas.
- `MinPrice` / `MaxPrice` são os limites do intervalo de preços regulamentados.
- `PRP` (Percent Recent Price) indica tendência curta; `updated` é human-readable.
- Plataformas ausentes ou com `LCPrice` zerado devem ser tratadas como sem preço válido.

## Cloudflare e estratégia de coleta

- O host `www.futbin.com` responde com **Cloudflare Challenge (HTTP 403)** a clientes não autorizados.
- O adapter **não** inclui solver de desafio. Em produção, o `HttpClient` deve ser roteado para:
  - um proxy autorizado (WAF-solver/captcha-bypass legítimo), ou
  - um cache interno alimentado por um componente externo, ou
  - um pipeline headless isolado fora do host da API.
- O `FutbinMarketClient` é intencionalmente simples: `HttpClient` + `User-Agent` + `Accept: application/json`. Parser e mapper são **independentes de transporte** e testados isoladamente com fixtures reais.

## Normalização

### `PlayerPriceSnapshot`

Um snapshot por plataforma com `LCPrice` válido:

| Campo | Origem |
| --- | --- |
| `Player` | `PlayerReference` fornecido pelo chamador |
| `Source` | `futbin:<platform>` (ex.: `futbin:ps`) |
| `CapturedAtUtc` | UTC no momento da coleta |
| `BuyNowPrice` | `LCPrice` |
| `SellNowPrice` | `LCPrice2` (se presente e `>= LCPrice`) |
| `MedianMarketPrice` | mediana de `{LCPrice, LCPrice2, MinPrice, MaxPrice}` disponíveis |

### `MarketListingSnapshot` (lowest BIN observation)

Para cada plataforma com preço válido, o adapter emite **uma** listagem sintética representando a menor BIN ativa no instante da coleta:

| Campo | Origem |
| --- | --- |
| `ListingId` | `SHA-256(playerId:platform:capturedAtBinary)` |
| `Source` | `futbin:<platform>` |
| `StartingBid`, `BuyNowPrice` | `LCPrice` |
| `ExpiresAtUtc` | `CapturedAtUtc + 10min` (janela conservadora de validade do snapshot) |

Essa listagem **não** representa uma auction real do EA Market — é uma projeção auditável do menor BIN público observado no FUTBIN. O `MarketListingSnapshot` real do EA Market é responsabilidade de outro adapter.

## Source metadata e auditabilidade

Toda coleta registra um **snapshot bruto** (via `IRawSnapshotStore`) com:

- `Source = "futbin"`
- `CapturedAtUtc`
- `RecordCount` (1 para a resposta do endpoint)
- `CorrelationId` (GUID da coleta)
- `PayloadHash` (SHA-256 do corpo)

Esses dados garantem que, mesmo quando o FUTBIN muda markup ou layout, qualquer snapshot normalizado no histórico seja reproduzível a partir do payload original salvo.

## Contrato compartilhado

O `FutbinMarketClient` implementa `TradingIntel.Application.PlayerMarket.IPlayerMarketClient` — o mesmo contrato neutro usado pelo `FutGgMarketClient`. O Worker (`PriceCollectionJob`) resolve sempre `IPlayerMarketClient` e não tem conhecimento de fonte; a escolha vem da configuração (`Market:Source`). Isso permite trocar FUTBIN ↔ FUT.GG sem tocar no job.

## Resiliência do parser

- Aceita payload vazio, JSON inválido e shape inesperado (retorna `null` com `LogWarning`).
- Aceita ausência de plataformas (`ps`, `xbox`, `pc`) sem falhar.
- Aceita campos ausentes (`LCPrice2`, `MinPrice`, `PRP`, `updated`).
- Trata `LCPrice = "0"` como ausência de preço válido.

## Cobertura de testes

Fixtures em `tests/fixtures/futbin/`:

- `player-prices-full.json` — três plataformas com dados completos.
- `player-prices-partial.json` — plataforma com `LCPrice = 0` e plataforma apenas com subconjunto de campos.
- `player-prices-empty.json` — objeto vazio.
- `player-prices-malformed.json` — JSON com shape fora do contrato.

Testes em `tests/TradingIntel.Tests/Infrastructure/Futbin/`:

- `FutbinPlayerPricesParserTests` — parsing e resiliência.
- `FutbinPlayerMapperTests` — mapeamento para `PlayerPriceSnapshot` e `MarketListingSnapshot`.
