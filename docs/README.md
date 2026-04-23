# Documentação

- [Arquitetura e camadas](architecture.md) — visão inicial alinhada ao código.
- [Fluxo de desenvolvimento e uso com Cursor Agents](development-workflow.md) — convenções de trabalho, testes e logs.
- [ADRs](architecture-decision-records/) — decisões arquiteturais.
- [Contribuindo (branches, PR, checklist)](../CONTRIBUTING.md).

## Integração contínua

O repositório usa GitHub Actions (workflow `CI`): em cada push ou pull request para `main`, roda `dotnet restore`, `dotnet build` e `dotnet test` em **Release** no Ubuntu com SDK .NET 9.

Atualize estes arquivos quando mudar fluxos, integrações externas ou contratos públicos.

