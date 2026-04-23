# Rating band demand

Serviço de aplicação que transforma SBCs ativos em demanda agregada por faixas de overall (`RatingBand`), produzindo um score explicável com motivos legíveis para o operador.

## Contrato

- Interface: `TradingIntel.Application.Sbc.IRatingBandDemandService`
- Implementação: `TradingIntel.Application.Sbc.RatingBandDemandService`
- Saída: `IReadOnlyList<RatingBandDemandScore>` ordenada por score decrescente.

```csharp
IReadOnlyList<RatingBandDemandScore> ComputeDemand(
    IReadOnlyList<SbcChallenge> challenges,
    DateTime nowUtc,
    RatingBandDemandWeights? weights = null);
```

O serviço é puro (sem I/O) e determinístico: para a mesma entrada e o mesmo `nowUtc`, o resultado é idêntico. Isso facilita testes, replays e auditoria.

## Modelo de saída

### `RatingBandDemandScore`

| Campo | Descrição |
| --- | --- |
| `Band` | Faixa de overall (ex.: `84-86`). |
| `Score` | Score normalizado em `[0, 1]`. |
| `TotalRequiredCards` | Soma de cartas requisitadas por todos os SBCs ativos naquela faixa. |
| `ContributingChallengeIds` | Ids dos SBCs que contribuíram para a faixa. |
| `Reasons` | Lista de `DemandReason` (código estável + mensagem em PT-BR + peso em `[0, 1]`). |

### `DemandReason`

Um motivo sempre começa com um resumo (`AGGREGATED_DEMAND`) e é seguido por até `MaxReasonsPerBand` motivos específicos por desafio, ordenados por relevância. Exemplos de códigos:

- `AGGREGATED_DEMAND` — resumo da faixa.
- `CHALLENGE_REPEATABLE_UNLIMITED` — SBC repetível sem limite.
- `CHALLENGE_REPEATABLE_LIMITED` — SBC com `MaxCompletions` finito.
- `CHALLENGE_ONE_SHOT` — SBC de uso único.
- `CHALLENGE_REPEATABILITY_UNKNOWN` — repetibilidade não conhecida.

Mensagens usam títulos reais de SBC, categoria e tempo até expirar — prontas para exibição em um painel operacional.

## Interpretação das `SbcRequirement`

A interpretação de rating é feita por heurística conservadora, baseada no `Key` já normalizado pelo `FutGgSbcMapper`:

1. O requisito precisa conter uma das palavras-chave: `team_rating`, `squad_rating`, `overall`, `rating` ou `rated`. Caso contrário, é ignorado (ex.: química, "Exactly 11 Players: Rare").
2. O rating-alvo é o maior inteiro presente no `Key` no intervalo `[60, 99]`. Se o `Key` não contém número de rating, usa-se `Minimum` se estiver nesse intervalo.
3. O número de cartas demandadas vem do maior inteiro no intervalo `[1, 23]` presente no `Key` (diferente do rating). Para requisitos como "Min. Team Rating: 83" (sem contagem explícita) assume-se **1 carta marginal** puxando a média da squad.
4. Cada rating-alvo é mapeado para uma faixa `[rating, min(99, rating + 2)]` — quase todo operador compra alguns overalls acima do mínimo para ter folga.

> Requisitos com expiração no passado (`ExpiresAtUtc <= nowUtc`) são descartados com log em nível `Debug`. Os demais são somados por faixa.

## Fórmula do score

Para cada contribuição `(challenge, rating, requiredCards)`, calcula-se:

```
contribution =
    w.CardVolume     * min(1, requiredCards / cardVolumeSaturation)
  + w.Repeatability  * repeatabilityFactor
  + w.ExpiryUrgency  * urgencyFactor
  + w.Category       * categoryFactor
```

O score da faixa é a soma das contribuições dividida por `ChallengeSaturation` e clampeado em `[0, 1]`:

```
score = clamp01( sum(contributions) / challengeSaturation )
```

### Fatores

| Fator | Escala | Valores |
| --- | --- | --- |
| `repeatabilityFactor` | `[0, 1]` | `Unlimited = 1.0`, `Limited(n) = min(n, 5)/5`, `NotRepeatable = 0.35`, `Unknown = 0.5` |
| `urgencyFactor` | `[0, 1]` | `≤ 24h → 1.0`, `≤ 72h → 0.75`, `≤ 7d → 0.6`, `> 7d → 0.45`, sem expiração → `0.5`, expirado → `0.0` |
| `categoryFactor` | `[0, 1]` | `upgrades = 1.0`, `icons = 0.9`, `players/heroes = 0.85`, `challenges = 0.7`, `foundations = 0.65`, demais → `0.6` |

## Pesos configuráveis (`RatingBandDemandWeights`)

| Parâmetro | Padrão | Descrição |
| --- | --- | --- |
| `CardVolume` | `0.45` | Peso do volume bruto de cartas requisitadas. |
| `Repeatability` | `0.25` | Peso da repetibilidade do SBC. |
| `ExpiryUrgency` | `0.15` | Peso da urgência da expiração. |
| `Category` | `0.15` | Peso da categoria do SBC. |
| `CardVolumeSaturation` | `22` | Quantidade de cartas que satura o fator de volume em 1,0 (~2 squads). |
| `ChallengeSaturation` | `3` | Contribuições de SBC que saturam o score agregado em 1,0. |
| `MaxReasonsPerBand` | `6` | Limite superior de motivos por faixa (além do resumo). |

Os quatro pesos principais devem **somar exatamente 1,0** (validado no construtor). Isso permite ajuste fino no futuro (ex.: priorizar urgência em janelas de evento) sem perder comparabilidade entre execuções.

## Registro no DI

O serviço é registrado em `AddApplication`:

```csharp
services.AddSingleton(RatingBandDemandWeights.Default);
services.AddScoped<IRatingBandDemandService, RatingBandDemandService>();
```

Para sintonizar pesos em runtime, substitua o registro por uma instância customizada antes de chamar `AddApplication`.

## Observabilidade

- `LogInformation` ao iniciar e concluir cada chamada com totais (`challenges`, `skippedChallenges`, `skippedRequirements`, `bands`).
- `LogDebug` por SBC descartado por expiração.
- Exceções são propagadas — a camada chamadora decide como reagir.

## Testes

`tests/TradingIntel.Tests/Application/Sbc/RatingBandDemandServiceTests.cs` cobre, entre outros cenários:

- input vazio e `nowUtc` não-UTC;
- descarte de SBC expirado;
- interpretação de variantes de `Key` (`min_team_rating_*`, `min_squad_rating_*`, `exactly_N_players_R_overall`, `min_rating_*`);
- requisitos sem sinal de rating são ignorados;
- SBC repetível > SBC de uso único para a mesma faixa;
- SBC com expiração próxima > SBC com expiração distante;
- agregação por faixa com múltiplos SBCs;
- contagem de cartas derivada de "Exactly N players";
- ordenação do resultado por score;
- motivos legíveis com pesos dentro de `[0, 1]`;
- pesos customizados alteram o score;
- pesos não-normalizados são rejeitados;
- `MaxReasonsPerBand` limita o número de motivos por faixa.

## Evoluções futuras

- Categorias adicionais (eventos sazonais, reward SBCs) via tabela injetável.
- Incorporar custo de mercado atual para priorizar faixas "baratas" em termos de coins/score.
- Expor endpoint HTTP na `TradingIntel.Api` quando o painel operacional estiver pronto.
