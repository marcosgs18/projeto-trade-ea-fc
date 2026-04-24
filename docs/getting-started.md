# Guia de uso passo a passo

Este guia leva o operador do `git clone` até **abrir o painel da API e ler oportunidades de trade** geradas pelo Worker. Foi escrito assumindo que você está no Windows (PowerShell), mas todos os comandos `dotnet` são idênticos no macOS/Linux.

> **O que o sistema faz, em uma frase**: o Worker coleta SBCs ativos e preços de jogadores no FUT.GG em ciclos curtos, persiste tudo em SQLite (com snapshots brutos auditáveis), recalcula um score de oportunidade por jogador da sua *watchlist*, e a API expõe esses dados para você consumir manualmente.

---

## 1. Pré-requisitos

| Ferramenta | Versão | Como verificar |
| --- | --- | --- |
| .NET SDK | **9.0** | `dotnet --version` deve retornar `9.x.x` |
| Git | qualquer recente | `git --version` |
| Acesso à internet | livre para `www.fut.gg` | `curl -I https://www.fut.gg/api/fut/player-prices/26/231747/?platform=pc` deve retornar `200 OK` |

Não é preciso instalar SQLite separadamente — o EF Core embute o provider e o arquivo `tradingintel.db` é criado na raiz do repositório no primeiro start.

## 2. Clonar e compilar

```powershell
git clone https://github.com/marcosgs18/projeto-trade-ea-fc.git
cd projeto-trade-ea-fc
dotnet build TradingIntel.sln
```

Esperado: `Compilação com êxito. 0 Aviso(s) 0 Erro(s)`.

Rodar a suíte de testes para confirmar que o ambiente está saudável:

```powershell
dotnet test TradingIntel.sln --no-build
```

Esperado: `Aprovado: 123, Com falha: 0`.

## 3. Configurar a watchlist (jogadores que você quer monitorar)

A coleta de preços e o cálculo de oportunidades operam sobre uma **lista estática** de jogadores. Edite os dois arquivos abaixo — ambos com a **mesma lista** para que o `price-collection` (Worker) e o `opportunity-recompute` (Worker e API) trabalhem casados.

`src/TradingIntel.Worker/appsettings.Development.json`:

```jsonc
{
  "Jobs": {
    "PriceCollection": {
      "Players": [
        { "PlayerId": 231747, "Name": "Kylian Mbappé (base)", "Overall": 94 }
        // adicione mais entradas aqui
      ]
    },
    "OpportunityRecompute": {
      "Players": [
        { "PlayerId": 231747, "Name": "Kylian Mbappé (base)", "Overall": 94 }
      ]
    }
  }
}
```

`src/TradingIntel.Api/appsettings.Development.json`:

```jsonc
{
  "Jobs": {
    "OpportunityRecompute": {
      "Players": [
        { "PlayerId": 231747, "Name": "Kylian Mbappé (base)", "Overall": 94 }
      ]
    }
  }
}
```

### Como descobrir o `PlayerId` correto

`PlayerId` é o **`eaId` do FUT.GG** (não o id do FUTBIN — eles são incompatíveis):

1. Procure o jogador em `https://www.fut.gg/players/`.
2. Abra a página da carta. A URL terá o formato `/players/<eaId>-<slug>/<season>-<eaId>/`.
3. Copie o `<eaId>`. Exemplos:
   - `https://www.fut.gg/players/231747-kylian-mbappe/26-231747/` → `231747` (carta base)
   - `https://www.fut.gg/players/231747-kylian-mbappe/26-67350350/` → `67350350` (versão especial/promo)
4. (Opcional) Valide via curl:

   ```powershell
   curl -A "Mozilla/5.0" "https://www.fut.gg/api/fut/player-prices/26/231747/?platform=pc"
   ```

   Se voltar `{"data":{"currentPrice":{"price":N,...}}}` com `N > 0`, está pronto pra usar.

`Overall` é o overall do card (0–99). Se você omitir, o jogador é **ignorado** pelo recompute (o scoring depende do overall pra casar com SBCs).

## 4. Subir o Worker (coleta + scoring contínuo)

Em um terminal:

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project src/TradingIntel.Worker
```

Os três jobs sobem em paralelo. Em ~10 segundos você vê:

```
sbc-collection starting. interval=300000ms ...
price-collection starting. interval=120000ms ...
opportunity-recompute starting. interval=60000ms ...
Application started. Press Ctrl+C to shut down.
price-collection tick summary. collectedPlayers=2/2 prices=2 listings=17 perPlayerFailures=0
sbc-collection collected 49 SBC challenges from futgg. ...
OpportunityRecompute: done. upserted=1 removedNoEdge=0 skippedOverall=0 skippedPrice=0 staleMarked=0
opportunity-recompute tick succeeded in 220ms.
```

### Como interpretar o log do recompute

O resumo `OpportunityRecompute: done. upserted=A removedNoEdge=B skippedOverall=C skippedPrice=D staleMarked=E` diz exatamente o que aconteceu no tick:

| Contador | Significado | Ação esperada |
| --- | --- | --- |
| `upserted` | Oportunidades gravadas/atualizadas em `trade_opportunities`. | Quanto maior, mais sinais para o operador inspecionar. |
| `removedNoEdge` | Cards onde o scoring rodou mas a margem líquida (após taxa EA de 5%) foi ≤ 0. | Comportamento normal em mercado eficiente; só vira problema se for **sempre** todos. |
| `skippedOverall` | Entradas da watchlist sem `Overall`. | Adicione o `Overall` no JSON. |
| `skippedPrice` | Não há preço recente para o jogador na fonte ativa. | Cheque se o `price-collection` está rodando e se o `PlayerId` está correto. |
| `staleMarked` | Oportunidades antigas marcadas `IsStale=true` por terem ultrapassado `Jobs:OpportunityRecompute:StaleAfter`. | Sinaliza no painel. |

### Falhas pontuais não derrubam o Worker

`FUT.GG` retorna `429`/`503` com frequência por causa de rate-limiting nos endpoints de **detalhe de SBC**. Você verá `Warning` nesses casos; isso afeta apenas o detalhamento de alguns SBCs, **não** a coleta de listagem nem a coleta de preços. Cada job tem isolamento de falha + backoff exponencial.

## 5. Subir a API (em outro terminal)

```powershell
$env:DOTNET_ENVIRONMENT="Development"
dotnet run --project src/TradingIntel.Api
```

A API sobe em `http://localhost:5xxx` (a porta exata aparece no log: `Now listening on: http://localhost:5xxx`).

### Endpoints disponíveis (sem autenticação — V1, uso em localhost)

| Método | Rota | Para quê |
| --- | --- | --- |
| GET | `/health` | Healthcheck (deve responder `200 Healthy`). |
| GET | `/swagger` | UI interativa do OpenAPI (apenas em `Development`). |
| GET | `/api/jobs/health` | Snapshot do registry de saúde dos jobs **dentro da API**. |
| GET | `/api/sbcs/active` | Lista SBCs ativos. Filtros: `category`, `expiresBefore`, `requiresOverall`, `includeExpired`, `page`, `pageSize`. |
| GET | `/api/market/prices?playerId=<id>` | Histórico de preços. `from`/`to` em UTC, paginação, `source` opcional. |
| GET | `/api/market/listings?playerId=<id>` | Listagens (auctions) por janela. |
| GET | `/api/opportunities` | Oportunidades de trade. Filtros: `minConfidence`, `minNetMargin`, `playerId`, `detectedAfter`. |
| GET | `/api/opportunities/{id}` | Detalhe da oportunidade (com razões e sugestões de execução). |
| POST | `/api/opportunities/recompute` | Dispara recompute **síncrono** com a watchlist da API. Body opcional: `{ "playerIds": [231747] }` para limitar a um subconjunto. |

### Exemplo: ver a oportunidade calculada para um jogador

```powershell
# substitua 5078 pela porta que aparece no log
curl http://localhost:5078/api/opportunities?playerId=231747&page=1&pageSize=10
```

Resposta esperada (resumo):

```json
{
  "items": [
    {
      "opportunityId": "0a2e1b...",
      "playerId": 231747,
      "playerDisplayName": "Kylian Mbappé (base)",
      "expectedBuyPrice": 224000,
      "expectedSellPrice": 235000,
      "expectedNetMargin": 1250,
      "confidence": 0.62,
      "isStale": false,
      "lastRecomputedAtUtc": "2026-04-23T23:55:46Z"
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalItems": 1,
  "totalPages": 1
}
```

Para o detalhe (com **razões** legíveis e **sugestões de execução**):

```powershell
curl http://localhost:5078/api/opportunities/0a2e1b...
```

```json
{
  "opportunityId": "0a2e1b...",
  "playerId": 231747,
  "playerDisplayName": "Kylian Mbappé (base)",
  "expectedBuyPrice": 224000,
  "expectedSellPrice": 235000,
  "expectedNetMargin": 1250,
  "confidence": 0.62,
  "reasons": [
    { "code": "TRADE_SCORE",        "message": "...", "weight": 0.62 },
    { "code": "DEMAND_OVERALL",     "message": "...", "weight": 0.45 },
    { "code": "NET_SPREAD",         "message": "...", "weight": 0.28 },
    { "code": "MARKET_LIQUIDITY",   "message": "...", "weight": 0.68 },
    { "code": "PRICE_STABILITY",    "message": "...", "weight": 0.50 },
    { "code": "SBC_EXPIRY_WINDOW",  "message": "...", "weight": 0.75 }
  ],
  "suggestions": [
    { "action": "Buy",         "price": 224000, "validUntilUtc": "..." },
    { "action": "ListForSale", "price": 235000, "validUntilUtc": "..." }
  ]
}
```

> **Como o operador usa isso na prática**: você abre o EA FC 26 Web/Companion App **manualmente** e usa as `suggestions` (`Buy @ X` por 15min, `ListForSale @ Y` por 24h) como guia. A V1 **nunca** automatiza compra/venda — é só *intelligence assistida*.

### Forçar uma rodada de recompute pela API

Útil quando você acabou de adicionar um jogador na watchlist da API e não quer esperar o próximo tick:

```powershell
$body = '{"playerIds":[231747]}'
curl -Method POST -Uri http://localhost:5078/api/opportunities/recompute `
     -ContentType "application/json" -Body $body
```

A resposta traz o mesmo `OpportunityRecomputeSummary` que você vê nos logs do Worker.

## 6. Inspecionar SBCs e mercado diretamente

Listar SBCs com filtros:

```powershell
# todos os ativos da categoria "upgrades", primeira página
curl "http://localhost:5078/api/sbcs/active?category=upgrades&page=1&pageSize=20"

# SBCs ativos onde uma carta overall 84 ajuda a cumprir
curl "http://localhost:5078/api/sbcs/active?requiresOverall=84&page=1&pageSize=20"

# SBCs que expiram nas próximas 24h (UTC)
curl "http://localhost:5078/api/sbcs/active?expiresBefore=$(([DateTime]::UtcNow).AddHours(24).ToString('o'))"
```

Histórico de preços de um jogador na última hora:

```powershell
$now = [DateTime]::UtcNow
$from = $now.AddHours(-1).ToString('o')
$to = $now.ToString('o')
curl "http://localhost:5078/api/market/prices?playerId=231747&from=$from&to=$to&page=1&pageSize=50"
```

## 7. Ajustar comportamento (opcional)

Tudo é configurável via `appsettings.json` sem rebuild. Os knobs mais usados:

| Chave | Onde | Default | Para que serve |
| --- | --- | --- | --- |
| `Market:Source` | Worker e API (`appsettings.json`) | `"futgg"` | Troca a fonte de mercado. `"futbin"` exige proxy WAF (ver `docs/source-futbin.md`). |
| `Market:FutGg:Platforms` | Worker | `["pc"]` | Lista de plataformas a coletar (`["pc","ps","xbox"]` etc.). |
| `Jobs:PriceCollection:Interval` | Worker | `"00:02:00"` em dev | Intervalo entre coletas de preços. |
| `Jobs:OpportunityRecompute:Interval` | Worker | `"00:01:00"` em dev | Intervalo entre recomputes. |
| `Jobs:OpportunityRecompute:StaleAfter` | Worker e API | `"00:15:00"` | Após esse tempo sem re-scoring, a oportunidade é marcada `IsStale=true`. |
| `Logging:LogLevel:Default` | Worker e API | `"Information"` | Suba para `"Debug"` quando precisar diagnosticar. |

Detalhes adicionais por seção: `docs/worker.md`, `docs/api.md`, `docs/persistence.md`, `docs/trade-scoring-v1.md`.

## 8. Resetar a base local

Sem migrations destrutivas envolvidas — basta apagar os arquivos e reiniciar o Worker (que vai aplicar as migrations e seguir):

```powershell
Remove-Item tradingintel.db, tradingintel.db-shm, tradingintel.db-wal -ErrorAction SilentlyContinue
```

Na próxima execução do Worker o esquema é recriado e a coleta começa do zero.

## 9. Solução de problemas rápidos

| Sintoma | Causa provável | Como resolver |
| --- | --- | --- |
| `OpportunityRecompute: done. upserted=0 removedNoEdge=0 skippedOverall=0 skippedPrice=N`, com `N` igual ao tamanho da watchlist | Watchlist do recompute não casa com o que o `price-collection` está coletando, ou `Market:Source` está em uma fonte sem snapshots persistidos. | Verifique que `Market:Source` é o mesmo no Worker e na API e que existem snapshots: `SELECT Source, COUNT(*) FROM player_price_snapshots GROUP BY Source;`. |
| `OpportunityRecompute: done. ... removedNoEdge=N` sempre que roda | Mercado realmente sem edge (taxa de 5% engole o spread). | Esperado; revise a watchlist incluindo cards com spread bid/buy maior. |
| `price-collection has no players configured; nothing to collect.` | `Jobs:PriceCollection:Players` está vazio. | Popule a watchlist (passo 3). |
| `Failed to fetch FUT.GG SBC detail. ... 429 (Too Many Requests)` | Rate-limit do FUT.GG no detalhamento dos SBCs. | Sem ação; o backoff dos jobs cuida e o restante do pipeline segue. |
| API responde `404` em `/api/opportunities/<id>` mesmo com a oportunidade existindo no banco | Você passou o `PlayerId` em vez do `OpportunityId` (GUID). | Use o `opportunityId` retornado por `GET /api/opportunities`. |
| Worker e API não enxergam a mesma base | Cada host está usando um `Data Source=` relativo a um `bin/` diferente. | Use o default (`tradingintel.db` resolvido para a raiz do repositório, conforme `DependencyInjection.AddInfrastructure`). |

## 10. Próximos passos sugeridos

- Popular a watchlist com 20–50 cards onde você quer alertas; o pipeline lida bem com listas dessa ordem.
- Consumir `/api/opportunities?minConfidence=0.6&minNetMargin=500` direto de uma planilha ou de um cliente HTTP simples para acompanhar.
- Se quiser dashboard visual, `TradingIntel.Dashboard` está como shell inicial — é o ponto natural para evoluir o painel operacional acima da API.
