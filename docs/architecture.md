# Arquitetura (visão inicial)

## Camadas

1. **TradingIntel.Domain**  
   Entidades, agregados, enums e regras puras. Sem referência a Application, Infrastructure ou hosts.

2. **TradingIntel.Application**  
   Serviços de aplicação, DTOs de entrada/saída, interfaces para repositórios e clientes externos. Referencia apenas Domain.

3. **TradingIntel.Infrastructure**  
   Adapters: implementações das interfaces da Application, acesso a rede, banco, armazenamento de snapshots brutos, etc. Referencia Domain e Application.

4. **Hosts (Api, Worker, Dashboard)**
   Composição raiz (`Program.cs`), configuração, middleware e endpoints HTTP mínimos. Orquestram chamadas à Application; não contêm regras de negócio.
   - `TradingIntel.Api` expõe os contratos HTTP (REST + Swagger em Development).
   - `TradingIntel.Worker` roda os jobs periódicos (coleta de preços, SBCs, recompute de oportunidades).
   - `TradingIntel.Dashboard` é a UI de operação (Blazor Server) — consome os mesmos repositórios de Application/Infrastructure e compartilha o SQLite. Ver [`dashboard.md`](dashboard.md).

## Políticas

- Fontes externas: interface + client + parser + mapper + testes com fixtures (detalhar por integração nas próximas tarefas).
- Score: explicável, com motivos legíveis para o operador.
- Não implementar execução automática de compra/venda; apenas sugestões e fluxo assistido.

## Health check

A `TradingIntel.Api` expõe `GET /health` via `AddHealthChecks` / `MapHealthChecks`, coberto por teste de integração em `TradingIntel.Tests`.
