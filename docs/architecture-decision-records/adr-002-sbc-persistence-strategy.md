# ADR-002: Persistência de SBCs — upsert por Id + requirements normalizados

- **Status:** Aceito
- **Data:** 2026-04-23
- **Decisores:** operador + agente (janela Task 9.5-A)
- **Substitui / relacionado:** complementa [ADR-001](adr-001-initial-architecture.md).

## Contexto

O `SbcCollectionJob` passou a receber, a cada tick, um `FutGgSbcListingSnapshot` contendo uma lista normalizada de `SbcChallenge` (domínio). Antes desta janela, a única persistência que havia era o payload **bruto** em `raw_snapshots` (fonte `futgg`) — suficiente para auditoria, mas insuficiente pra responder perguntas operacionais como:

- "Quais SBCs estão **ativos agora**?"
- "Quais SBCs na categoria X aceitam um card overall 85?"
- "Qual é o SBC com `Id = …` que foi referenciado por um trade sugerido?"

Duas restrições de produto guiam a decisão:

1. **Invariante raw-before-normalized** (AGENTS.md → Restrições): toda integração externa precisa armazenar o bruto; o normalizado pode ser recomputado.
2. **Confiabilidade da coleta > performance** (AGENTS.md → Prioridades): o esquema precisa sobreviver a mudanças de shape do FUT.GG sem perder histórico.

Há três subdecisões envolvidas.

## Decisão

### 1. Estratégia de persistência: **upsert por `Id`** (GUID FUT.GG)

`sbc_challenges` guarda apenas o **estado canônico atual** dos SBCs (uma linha por `Id`). A cada tick, `SbcChallengeRepository.UpsertRangeAsync` sobrescreve os campos do registro existente com o mesmo `Id` e registra um `ObservedAtUtc` atualizado; linhas de SBCs que sumirem do snapshot **não** são deletadas automaticamente (a expiração natural via `ExpiresAtUtc` já remove-os das queries ativas).

Histórico temporal — "como esse SBC estava há 3 horas" — é **reconstruível a partir de `raw_snapshots`** (fonte `futgg`, `CapturedAtUtc`). O raw é a fonte única de verdade histórica.

### 2. Requirements: **tabela filha normalizada** (`sbc_challenge_requirements`)

Uma linha por requirement, com FK `ChallengeId → sbc_challenges.Id` e `ON DELETE CASCADE`. Colunas: `Key`, `Minimum`, `Maximum?`, `Order`.

A cada upsert, os requirements do SBC são **substituídos integralmente** (drop via `ExecuteDelete` + reinsert) — tratados como uma *value list* do agregado `SbcChallenge`, não como entidades com identidade própria.

### 3. Semântica de `MatchesOverall(overall)`: **whitelist de keys**

`SbcChallengeQuery.MatchesOverall` conta apenas requirements cuja `Key` está na constante pública `SbcChallengeQuery.TeamRatingRequirementKeys = { "min_team_rating", "squad_rating" }` (case-insensitive), com `Minimum <= overall`. O teto (`Maximum`) não é filtrado nesta janela: tratamos *matching* como "esse card ajuda a cumprir", não "esse card sozinho resolve".

## Consequências

### Positivas

- **Consulta "ativos agora" é barata**: `WHERE ExpiresAtUtc IS NULL OR ExpiresAtUtc > nowUtc` + índice `ix_sbc_challenges_expires_at`; não varre série histórica.
- **Filtro por requirement é SQL-native**: `ix_sbc_challenge_requirements_key_minimum (Key, Minimum)` acelera `MatchesOverall` sem carregar todos os requirements em memória.
- **Shape resiliente**: se o FUT.GG introduzir uma nova chave de requirement, ela entra na tabela filha como mais uma linha — nenhum schema change necessário.
- **Sem risco de "histórico perdido"**: o raw continua sendo gravado antes do normalizado (invariante raw-before-normalized preservada nos clients).

### Negativas / custos

- Queries temporais ("como estava ontem às 18h?") **não** batem em `sbc_challenges` — precisam ir ao raw. Aceitável porque esse caso é de auditoria, não de loop de decisão.
- `UpsertRangeAsync` faz `ExecuteDelete` nos requirements a cada tick mesmo quando o requirement não mudou. Custo aceitável em volume atual (~dezenas a centenas de SBCs ativos); se virar gargalo, migramos para diff-based.
- A whitelist de keys em `TeamRatingRequirementKeys` precisa ser mantida manualmente quando o FUT.GG introduzir novas chaves equivalentes. Mitigação: constante pública, testes unitários cobrem casos positivos/negativos.

## Alternativas consideradas

1. **Append-only por `(Id, ObservedAtUtc)`** — tabela temporal, cresce a cada tick. Permite "SBC há 3h atrás" sem ir ao raw, mas exige índices extras, *compaction*, consultas mais caras ("último por Id") e duplica o papel do raw. Rejeitado: o raw já cobre histórico; o normalizado deve ser pequeno e rápido.
2. **Requirements em coluna JSON** — menos tabelas, menos migrações. Rejeitado: força filtros em memória (ou JSON1 SQLite-specific) e inviabiliza o índice `(Key, Minimum)` que é a hotpath de `MatchesOverall`.
3. **`MatchesOverall` contra qualquer `Key` contendo "rating"** — mais genérico, mas daria falso positivo em chaves futuras tipo `chem_rating` que não representam "rating do time". Rejeitado em favor da whitelist explícita + teste.
4. **`MatchesOverall` contra qualquer requirement (qualquer `Key`) onde `overall >= Minimum`** — mais solto, fácil de implementar, mas o filtro perde significado semântico. Rejeitado.

## Notas de implementação

- `ISbcChallengeRepository` vive em `TradingIntel.Application.Persistence` (porta); a implementação SQLite vive em `TradingIntel.Infrastructure.Persistence.Repositories`.
- `UpsertRangeAsync` deduplica o batch por `Id` (preservando a última ocorrência) antes de mexer no DB — defensivo contra o cenário em que o listing e o detail fallback tragam o mesmo `Id` no mesmo tick.
- `PersistenceTestFixture` continua aplicando `Database.Migrate()` em SQLite in-memory, então testes de integração validam o schema real gerado pela migration `AddSbcChallenges`.
- Detalhes de schema e uso estão em [`docs/persistence.md`](../persistence.md).
