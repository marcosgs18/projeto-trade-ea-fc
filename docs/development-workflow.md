# Fluxo de desenvolvimento (uso com Cursor Agents)

Este documento descreve **como um agente (ou colaborador humano)** deve trabalhar neste repositório para manter arquitetura, testes, logs e documentação alinhados ao produto.

## Leitura obrigatória antes de mudar código

1. [`AGENTS.md`](../AGENTS.md) na raiz — objetivo do produto, restrições e definição de pronto.
2. [`CONTRIBUTING.md`](../CONTRIBUTING.md) — branches, PRs e checklist.
3. [Arquitetura em camadas](architecture.md) — limites entre projetos.
4. [Regras do projeto (Cursor)](../.cursor/rules/project-rule.md) e [regras operacionais para agentes](../.cursor/rules/agent-operational.md).

Para decisões já registradas, consulte [ADRs](architecture-decision-records/).

## Papel do agente

O agente deve:

- **Respeitar as camadas**: Domain sem dependências externas de aplicação/infra; Application só orquestra e define portas; Infrastructure implementa integrações; Api/Worker/Dashboard apenas compõem DI e expõem interfaces (HTTP, jobs, UI), **sem lógica de domínio em controllers** (quando existirem, devem delegar à Application).
- **Manter mudanças pequenas**: uma preocupação por PR quando possível; evitar refatorações amplas não solicitadas.
- **Atualizar documentação mínima em `/docs`** quando o comportamento, contrato público ou fluxo operacional mudar (incluir link ou nota no PR).
- **Não expandir escopo** além da tarefa (sem “limpezas” não pedidas em arquivos não relacionados).

## Fluxo sugerido (tarefa → PR)

1. **Planejar**: identificar camadas afetadas e interfaces necessárias (especialmente integrações externas).
2. **Implementar**: código mínimo para o critério de aceite; preferir funções e tipos coerentes com o estilo existente.
3. **Testar**: adicionar ou ajustar testes automatizados (ver seção Convenções de testes).
4. **Observabilidade**: logs e erros mínimos onde o fluxo falha ou é relevante para operação (ver Convenções de logs).
5. **Documentar**: `docs/` (e ADR se houver decisão arquitetural relevante).
6. **Abrir PR**: usar o checklist de [`CONTRIBUTING.md`](../CONTRIBUTING.md).

## Integrações externas

Para **cada fonte externa**, a implementação em Infrastructure deve seguir o padrão acordado:

- interface (porta na Application, quando aplicável);
- client HTTP/outro;
- parser;
- mapper para modelos de domínio/DTOs;
- **testes com fixtures reais** (ex.: JSON/HTML de exemplo versionado em `tests/` ou obtidos de forma reproduzível), não só mocks genéricos onde o risco de regressão for alto.

Nunca acoplar chamadas diretas a fontes externas dentro de controllers ou UI sem passar por Infrastructure.

## Score e explicabilidade

Regras de score devem ser **explicáveis**: saídas devem permitir motivos legíveis ao operador (estruturas de “reasons”, mensagens estáveis, etc.), conforme evolução do domínio.

## Restrições de produto (reforço)

- **Não** automatizar compra/venda no Web App; apenas sugestões e fluxo assistido.
- **Não** depender de scraping frágil em controllers.
- **Não** quebrar contratos públicos sem testes que protejam o contrato.
- Manter **snapshots brutos** para auditoria quando houver ingestão (detalhes de armazenamento evoluem na Infrastructure).

## Convenções de testes

| Aspecto | Convenção |
| --- | --- |
| Framework | **xUnit** (`[Fact]`, `[Theory]` quando fizer sentido). |
| Asserções | **FluentAssertions** para legibilidade (`actual.Should().Be(expected)`). |
| Nomenclatura | Nomes de teste descrevem comportamento: `Metodo_Cenario_ResultadoEsperado` ou frase clara em inglês/português **consistente com o arquivo**. |
| Camadas | **Domain/Application**: testes unitários rápidos, sem IO real quando possível. **Infrastructure**: testes com fixtures reais para parsers/mappers; mocks para limites de rede quando necessário. **Api**: testes de integração para endpoints críticos (ex.: health, contratos públicos) usando `WebApplicationFactory` ou equivalente. |
| CI | `dotnet test` em Release deve passar localmente antes do PR (o workflow de CI na `main` também valida). |
| Novos contratos HTTP | Incluir teste que falhe se o status code, shape mínimo ou regra acordada mudar sem intenção. |

## Convenções de logs

| Aspecto | Convenção |
| --- | --- |
| API | `ILogger<T>` injetado; categorias claras (`T` = tipo que gera o log). |
| Worker | Mesmo padrão; logs em início/fim de job ou em falhas relevantes; evitar poluir **Information** em loops apertados. |
| Níveis | **Debug**: detalhe diagnóstico. **Information**: fluxo normal útil em produção. **Warning**: degradado ou retry. **Error**: falha que exige atenção. |
| Estruturado | Preferir placeholders (`logger.LogInformation("Ingestão concluída para {Source} em {ElapsedMs} ms", source, elapsed)`) em vez de interpolação de strings com dados grandes. |
| Dados sensíveis | Não logar tokens, cookies, credenciais ou PII; mascarar identificadores se necessário. |
| Correlação | Quando existir `TraceIdentifier` ou correlation id no host, incluir em logs de requisição quando fizer sentido (evolução futura na Api). |
| Exceções | Logar exceção com contexto (`LogError(ex, "...")`); não engolir exceções sem registro em hosts. |

## Definição de pronto (checklist rápido)

Alinhado ao [`AGENTS.md`](../AGENTS.md):

- Compila (`dotnet build`).
- Testes relevantes passam (`dotnet test`).
- Documentação em `/docs` atualizada quando aplicável.
- Logs e tratamento de erro mínimo onde há fluxo novo ou falha esperada.
- Arquitetura em camadas respeitada.

## CI e branches

- Integração contínua: ver [README na pasta docs](README.md).
- Trabalho em **branch** fora da `main`; merge via **PR** seguindo [`CONTRIBUTING.md`](../CONTRIBUTING.md).
