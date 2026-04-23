# Project Rules

- Stack principal: .NET 9, ASP.NET Core Web API, Worker Service, xUnit, FluentAssertions, Playwright opcional apenas para testes de regressão visual.
- Arquitetura obrigatória: Domain -> Application -> Infrastructure -> Api/Worker/Dashboard.
- Nunca acessar fontes externas diretamente de controllers; usar adapters em Infrastructure.
- Toda fonte externa deve ter:
  - interface
  - client
  - parser
  - mapper
  - testes com fixtures reais
- Toda regra de score deve ser explicável e retornar motivos legíveis.
- Nunca implementar execução automática de compra/venda no mercado; apenas sugestões e workflow assistido.
- Toda task deve atualizar documentação mínima em /docs.
- Toda PR deve incluir:
  - resumo
  - decisões técnicas
  - riscos
  - como testar
- Priorizar tarefas pequenas e isoladas, com baixo acoplamento.