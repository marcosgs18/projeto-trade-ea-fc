# Trade Scoring V1

Serviço de aplicação que transforma o contexto de mercado de um jogador (preço, histórico, listagens e demanda de SBCs) em uma `TradeOpportunity` com score explicável — **sem machine learning** nesta fase.

## Contrato

- Interface: `TradingIntel.Application.Trading.ITradeScoringService`
- Implementação: `TradingIntel.Application.Trading.TradeScoringService`
- Entrada: `TradeScoringInput`
- Saída: `TradeOpportunity?` (nulo quando não há edge líquido após taxa)

```csharp
TradeOpportunity? Score(TradeScoringInput input, TradeScoringWeights? weights = null);
```

O serviço é puro (sem I/O) e determinístico: mesmo `TradeScoringInput` → mesmo resultado.

## Inputs

`TradeScoringInput` agrega tudo que o serviço precisa em um único objeto:

| Campo | Papel |
| --- | --- |
| `Player` | `PlayerReference` alvo. |
| `OverallRating` | Overall da carta (0-99). Define a faixa consultada em `DemandByBand`. |
| `CurrentPrice` | `PlayerPriceSnapshot` atual (BIN, sell-now e mediana). |
| `PriceHistory` | `IReadOnlyList<PlayerPriceSnapshot>` usado no cálculo de volatilidade. |
| `RecentListings` | `IReadOnlyList<MarketListingSnapshot>` usado como proxy de liquidez. |
| `DemandByBand` | Scores de `IRatingBandDemandService.ComputeDemand(...)`. |
| `NearestSbcExpiryUtc` | Expiração do SBC ativo mais próximo que demanda a faixa (opcional). |
| `NowUtc` | Momento de referência (UTC). |

> O serviço **não** recebe a lista crua de `SbcChallenge`. O caller é responsável por resolver `DemandByBand` e `NearestSbcExpiryUtc` — isso mantém o serviço puro e os inputs já normalizados.

## Fórmula

O score final é uma soma linear de cinco fatores normalizados em `[0, 1]`:

```
score = w.Demand     * demandFactor
      + w.Spread     * spreadFactor
      + w.Liquidity  * liquidityFactor
      + w.Stability  * stabilityFactor
      + w.Urgency    * urgencyFactor
```

Como os cinco pesos somam exatamente `1,0` (validado no construtor), **o score também está em `[0, 1]`** sem normalização posterior. Ele é usado como `ConfidenceScore`.

### Fatores

| Fator | Fórmula | Saturação |
| --- | --- | --- |
| `demandFactor` | `max(demandByBand[b].Score)` para bandas `b` que contêm `OverallRating`; `0` se nenhuma. | Já em `[0, 1]` por construção do `RatingBandDemandService`. |
| `spreadFactor` | `clamp01(relSpread / SpreadSaturation)` com `relSpread = netMargin / buyPrice`. | Padrão `SpreadSaturation = 0,20` (20% líquido satura). |
| `liquidityFactor` | `clamp01(count(RecentListings) / LiquiditySaturation)`. | Padrão `LiquiditySaturation = 25` listagens. |
| `stabilityFactor` | `1 - clamp01(CV / VolatilitySaturation)` onde `CV = stddev / mean` do `BuyNowPrice` no histórico dentro de `VolatilityWindow`. | Padrão `VolatilitySaturation = 0,25`, `VolatilityWindow = 24h`. Histórico com menos de 2 pontos → fator neutro `0,5`. |
| `urgencyFactor` | Mesmos buckets do `RatingBandDemandService`: `≤ 24h → 1,0`, `≤ 72h → 0,75`, `≤ 7d → 0,6`, `> 7d → 0,45`, ausente → `0,5`, expirado → `0,0`. | — |

### Valores monetários

- `suggestedBuyPrice = CurrentPrice.BuyNowPrice` — alvo de entrada (BIN floor observado).
- `suggestedSellPrice = CurrentPrice.SellNowPrice ?? CurrentPrice.MedianMarketPrice` — alvo de saída conservador.
- Taxa do EA: `TradeOpportunity.EaMarketTaxRate = 0,05` (5%).
- `netSell = floor(sellPrice * 0,95)` — arredondamento para baixo, conservador por design.
- `expectedNetMargin = max(0, netSell - buyPrice)`.

Se `expectedNetMargin <= 0` **ou** `sellPrice <= buyPrice`, o serviço retorna `null` — não há oportunidade, nada é sugerido.

Quando a oportunidade existe:

- `TradeOpportunity.ExpectedProfit` = `sellPrice - buyPrice` (**bruto**, útil para ordenação rápida).
- `TradeOpportunity.ExpectedNetMargin` = `netSell - buyPrice` (**líquido**, após taxa).
- Duas `ExecutionSuggestion`s são anexadas: `Buy @ buyPrice` (válida por 15min) e `ListForSale @ sellPrice` (válida por 24h).

## `OpportunityReason`s emitidas

Sempre na mesma ordem e com códigos estáveis (facilita dashboard e telemetria):

| Código | Peso | Mensagem resumida |
| --- | --- | --- |
| `TRADE_SCORE` | `confidence` | Resumo: confiança, compra/venda sugeridas, líquido e margem percentual. |
| `DEMAND_OVERALL` | `demandFactor` | Faixa matched + total de cartas demandadas + nº de SBCs. |
| `NET_SPREAD` | `spreadFactor` | Margem em coins e percentual sobre a compra. |
| `MARKET_LIQUIDITY` | `liquidityFactor` | Quantidade de listagens recentes consideradas. |
| `PRICE_STABILITY` | `stabilityFactor` | Pontos usados no histórico dentro da janela. |
| `SBC_EXPIRY_WINDOW` | `urgencyFactor` | Bucket em que cai a expiração do SBC mais próximo. |

Peso de cada razão corresponde ao valor do fator em `[0, 1]` — não multiplicado pelo peso — para que o operador leia cada componente diretamente.

## `TradeScoringWeights`

| Parâmetro | Padrão | Descrição |
| --- | --- | --- |
| `Demand` | `0,30` | Peso da demanda agregada. |
| `Spread` | `0,30` | Peso do spread líquido. |
| `Liquidity` | `0,15` | Peso da liquidez observada. |
| `Stability` | `0,10` | Peso da estabilidade (inverso da volatilidade). |
| `Urgency` | `0,15` | Peso da janela de expiração de SBC. |
| `SpreadSaturation` | `0,20` | Spread líquido relativo que satura o fator em `1,0`. |
| `LiquiditySaturation` | `25` | Listagens recentes que saturam o fator em `1,0`. |
| `VolatilitySaturation` | `0,25` | CV (stddev/mean) que satura a penalidade em `1,0`. |
| `VolatilityWindow` | `24h` | Janela usada no histórico para volatilidade. |

> Os cinco pesos principais **precisam somar exatamente `1,0`** (validado no construtor). Isso garante comparabilidade entre execuções.

## Registro no DI

Em `TradingIntel.Application.DependencyInjection.AddApplication`:

```csharp
services.AddSingleton(TradeScoringWeights.Default);
services.AddScoped<ITradeScoringService, TradeScoringService>();
```

Para sintonizar em runtime, substitua `TradeScoringWeights.Default` por uma instância customizada antes de chamar `AddApplication`.

## Observabilidade

- `LogInformation` quando nenhuma oportunidade é emitida (edge líquido ≤ 0): loga `buy`, `sell`, `netMargin` e o jogador.
- `LogInformation` quando há oportunidade: loga os cinco fatores, a confiança final e os preços sugeridos.
- Sem exceções silenciadas — erros inesperados são propagados para a camada chamadora.

## Testes

`tests/TradingIntel.Tests/Application/Trading/TradeScoringServiceTests.cs` cobre, entre outros cenários sintéticos:

- Retorno `null` quando a margem líquida não é positiva (inclui o caso em que o spread bruto é consumido pela taxa).
- Retorno `null` quando `buy == sell`.
- Happy path: `ExpectedProfit` bruto, `ExpectedNetMargin` líquido, razões e sugestões de execução corretas.
- Uso de `SellNowPrice` quando disponível, caindo para a mediana quando não.
- Demanda alta > demanda baixa aumenta a confiança.
- Liquidez alta > liquidez zero aumenta a confiança.
- Histórico volátil penaliza a estabilidade.
- SBC expirando em poucas horas supera expiração distante.
- Bandas fora do overall não contribuem para a demanda.
- Entre bandas sobrepostas, usa-se o **maior** score que cobre o overall.
- Pesos customizados mudam a confiança final.
- Pesos não-normalizados são rejeitados.
- Entrada `null`, `nowUtc` não-UTC e mismatch de jogador em `currentPrice` são rejeitados.
- Histórico vazio → estabilidade neutra `0,5`.
- `NearestSbcExpiryUtc` ausente → urgência neutra `0,5`.
- `NearestSbcExpiryUtc` no passado → urgência `0,0`.
- `TradeOpportunity.ExpectedNetMargin` bate com a fórmula `floor(sell * 0,95) - buy`.
- `TradeOpportunity.ExpectedNetMargin` satura em `0` quando a taxa apaga o edge.

## Evoluções futuras

- Agregar `TradeOpportunity` com spread de bid (`StartingBid`) para lances sniping.
- Ajustar `suggestedSellPrice` usando percentil do histórico em vez da mediana.
- Ajuste dinâmico de `SpreadSaturation` por faixa (cartas mais caras toleram margens percentuais menores).
- Versão V2 com features adicionais (tendência, sazonalidade) antes de considerar qualquer modelo estatístico.
