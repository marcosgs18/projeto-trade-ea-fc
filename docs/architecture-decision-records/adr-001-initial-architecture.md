# ADR-001: Arquitetura inicial em camadas (.NET 9)

- **Status:** Aceito  
- **Data:** 2026-04-22  
- **Decisores:** equipe do repositório (contexto inicial do projeto)

## Contexto

O produto é uma plataforma **privada** de trading intelligence para EA FC 26, com ingestão de dados, normalização, histórico, score de oportunidades, API interna e painel operacional. É necessário:

- evoluir o sistema com **baixo acoplamento** e testes;
- isolar **regras de negócio** de detalhes de infraestrutura (HTTP, persistência, fontes externas);
- permitir **hosts** distintos (API síncrona, worker assíncrono, dashboard) sem duplicar lógica de domínio;
- cumprir restrições explícitas: **sem automação de compra/venda** no app; **snapshots brutos** para auditoria; integrações externas tratadas de forma estruturada (não em controllers).

## Decisão

Adotar uma **Clean Architecture pragmática** com projetos .NET 9:

| Projeto | Responsabilidade |
| --- | --- |
| `TradingIntel.Domain` | Modelos e regras puras; sem dependência de Application/Infrastructure/hosts. |
| `TradingIntel.Application` | Casos de uso, orquestração, DTOs e **portas** (interfaces); depende apenas de Domain. |
| `TradingIntel.Infrastructure` | Implementações concretas (clientes, parsers, mappers, persistência); depende de Domain e Application. |
| `TradingIntel.Api` | ASP.NET Core: composição (`Program.cs`), health, endpoints finos; **sem lógica de domínio** em controllers quando introduzidos. |
| `TradingIntel.Worker` | Hosted services para ingestão e jobs. |
| `TradingIntel.Dashboard` | Host web do painel operacional. |
| `TradingIntel.Tests` | xUnit + FluentAssertions; testes unitários e de integração conforme necessidade. |

**Dependências permitidas:** Domain ← Application ← Infrastructure; hosts referenciam Application e Infrastructure para composição.

**Stack:** .NET 9, ASP.NET Core Web API, Worker Service, xUnit, FluentAssertions.

## Consequências

### Positivas

- Limites claros facilitam testes e substituição de adapters (ex.: nova fonte de preço).
- Regras de score e ingestão podem evoluir no Domain/Application sem vazar HTTP ou detalhes de IO.
- Vários executáveis compartilham o mesmo núcleo.

### Negativas / custos

- Mais projetos e cerimônia de referências (mitigado por documentação e CI).
- Curva inicial para novos colaboradores (mitigada por `docs/architecture.md` e este ADR).

## Alternativas consideradas

1. **Monólito único (um projeto Web + tudo inline)** — rejeitado: tende a misturar controllers com parsing e regras, dificultando testes e auditoria.
2. **Vertical slices apenas** — adiado: pode ser revisitado dentro de camadas se o time quiser organizar por feature **sem** quebrar a regra de dependências Domain → Application → Infrastructure.
3. **Microserviços desde o início** — rejeitado no bootstrap: complexidade operacional desproporcional ao estágio do produto.

## Notas de implementação

- Health check na Api com teste de integração garante smoke básico do host.
- Novas integrações externas devem seguir interface + client + parser + mapper + testes com fixtures reais (detalhado nas regras do projeto).
