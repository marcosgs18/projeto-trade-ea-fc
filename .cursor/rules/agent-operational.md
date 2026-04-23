# Regras operacionais — Cursor Agents (TradingIntel)

Instruções para agentes no Cursor. Detalhes e justificativas: [`docs/development-workflow.md`](../../docs/development-workflow.md), [`CONTRIBUTING.md`](../../CONTRIBUTING.md), [`AGENTS.md`](../../AGENTS.md).

## Antes de editar

- Ler `AGENTS.md` (objetivo, restrições, definição de pronto).
- Confirmar limites em [`docs/architecture.md`](../../docs/architecture.md) e ADRs em [`docs/architecture-decision-records/`](../../docs/architecture-decision-records/).
- Trabalhar em **branch** dedicada; PR para `main` com checklist de `CONTRIBUTING.md`.

## Arquitetura (obrigatório)

- Dependências: **Domain → Application → Infrastructure → hosts** (Api, Worker, Dashboard). Domain não referencia outras camadas.
- **Sem lógica de domínio em controllers**; hosts delegam à Application. Sem chamadas diretas a fontes externas a partir de controllers/UI — usar **Infrastructure** (adapters).
- Integração externa: **interface + client + parser + mapper + testes com fixtures reais** (quando houver fonte externa nova ou alterada).

## Testes

- **xUnit** + **FluentAssertions**.
- Incluir testes que protejam comportamento novo ou contrato público; integração da Api onde fizer sentido (ex.: endpoints estáveis).
- Garantir `dotnet test` passando antes de considerar a tarefa pronta.

## Logs

- Usar **`ILogger<T>`**; níveis adequados (Information vs Debug vs Warning vs Error).
- Mensagens **estruturadas** (placeholders); **não** logar segredos nem PII.
- Em falhas operacionais, registrar exceção com contexto (`LogError(ex, ...)`).

## Documentação e escopo

- Qualquer mudança relevante de fluxo, contrato ou operação: atualizar **`/docs`** (mínimo).
- Decisão arquitetural relevante: novo **ADR** em `docs/architecture-decision-records/`.
- **Não** refatorar arquivos fora do escopo da tarefa; **não** implementar compra/venda automática no mercado.

## Produto e compliance

- Score e oportunidades: saídas **explicáveis** (motivos legíveis), conforme evolução do código.
- Preservar rastreabilidade: **snapshots brutos** para auditoria quando houver ingestão (estrategia de armazenamento na Infrastructure).
