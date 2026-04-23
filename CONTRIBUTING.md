# Contribuindo com o TradingIntel

Este documento padroniza branches, pull requests e o que revisores e agentes devem verificar. Complementa [`AGENTS.md`](AGENTS.md) e [`docs/development-workflow.md`](docs/development-workflow.md).

## Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Git
- Familiaridade com a [arquitetura em camadas](docs/architecture.md)

## Nomenclatura de branches

Use prefixo em **inglês** (minúsculas, **kebab-case** na descrição) + escopo curto:

| Prefixo | Uso |
| --- | --- |
| `feature/` | Nova funcionalidade ou caso de uso |
| `fix/` | Correção de bug |
| `docs/` | Apenas documentação |
| `chore/` | Manutenção (CI, dependências, tooling) sem mudança de comportamento do produto |
| `refactor/` | Refatoração sem mudança funcional intencional |

**Formato:** `<prefixo>/<descrição-curta>`

**Exemplos:**

- `feature/ingest-sbc-snapshot`
- `fix/health-check-failure`
- `docs/cursor-operational-workflow`
- `chore/bump-test-packages`

Evite branches genéricas como `test` ou `wip` sem contexto.

## Commits

- Mensagens claras no imperativo (pt ou en), primeira linha até ~72 caracteres.
- Opcional: [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, …) se o time adotar de forma consistente.

## Pull requests

### Conteúdo obrigatório no corpo do PR

Todo PR deve incluir as quatro seções abaixo (pode copiar o template).

```markdown
## Resumo
(O que mudou em 1–3 frases.)

## Decisões técnicas
(Abordagem escolhida e por quê.)

## Riscos
(O que pode dar errado, impacto, mitigação se houver.)

## Como testar
(Passos concretos: comandos, URLs, dados de exemplo.)
```

### Checklist do PR (explícito)

Marque o que se aplica. Itens não aplicáveis: marque N/A e justifique numa linha.

- [ ] **`dotnet build`** passa localmente na solution.
- [ ] **`dotnet test`** passa localmente (Release recomendado: `dotnet test TradingIntel.sln -c Release`).
- [ ] **Arquitetura**: mudanças respeitam Domain → Application → Infrastructure → hosts; sem lógica de domínio nova em controllers.
- [ ] **Integrações externas** (se houver): interface + client + parser + mapper; testes com **fixtures reais** onde o risco de regressão for alto.
- [ ] **Testes**: novos comportamentos ou contratos têm cobertura automatizada relevante (unitário e/ou integração).
- [ ] **Logs**: fluxos novos ou falhas relevantes usam `ILogger<T>` com níveis adequados; sem segredos/PII em log.
- [ ] **Documentação**: `/docs` atualizado quando o comportamento, fluxo operacional ou contrato público mudou.
- [ ] **ADR**: decisão arquitetural relevante registrada em `docs/architecture-decision-records/` (ou N/A).
- [ ] **Restrições de produto**: não há automação de compra/venda; não há scraping frágil acoplado a controllers; contratos públicos não quebram sem testes.

### Revisão

- Pelo menos uma aprovação humana antes do merge na `main` (ajuste se a política do time mudar).
- CI (GitHub Actions) deve estar verde no PR.

## Depois do merge

- Branch remota pode ser removida (`git push origin --delete <branch>`).
- Se a decisão foi importante, garantir que o ADR e `docs/` reflitam o estado atual.

## Referências

- [Fluxo de desenvolvimento para agentes](docs/development-workflow.md)
- [ADR-001 — Arquitetura inicial](docs/architecture-decision-records/adr-001-initial-architecture.md)
- [Regras do projeto (.cursor)](.cursor/rules/project-rule.md)
