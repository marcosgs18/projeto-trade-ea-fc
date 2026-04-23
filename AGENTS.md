# AGENTS.md

## Objetivo do produto
Construir uma plataforma privada de trading intelligence para EA FC 26 focada em:
- ingestão de SBCs ativos e superfícies públicas de preço
- normalização e histórico temporal
- score de oportunidades de trade
- API interna
- painel operacional para execução manual assistida

## Restrições
- Não automatizar compra/venda no Web App
- Não depender de scraping frágil em controllers
- Não quebrar contratos públicos sem testes
- Sempre armazenar snapshots brutos para auditoria

## Definição de pronto
Uma task só está pronta quando:
- compila
- tem testes automatizados relevantes
- atualiza docs
- possui logs claros
- possui tratamento de erro mínimo
- respeita a arquitetura

## Prioridades
1. confiabilidade da coleta
2. clareza dos modelos
3. explicabilidade do score
4. observabilidade
5. UI operacional

## Entrega da task
Ao concluir uma feature/task (compilando, com testes e docs atualizadas), o agente deve **sempre perguntar ao operador**, antes de encerrar a execução:

1. Se deve criar uma **branch dedicada** (`feature/<slug>` por padrão) a partir de `main`.
2. Se deve abrir um **Pull Request** para `main` via `gh pr create`, usando o template `## Summary` + `## Test plan`.

O agente nunca deve criar branch/commit/PR sem confirmação explícita, exceto se o operador pedir na mensagem inicial. Quando o operador confirmar, seguir o fluxo de `CONTRIBUTING.md` (branch name, mensagem de commit em Conventional Commits, PR com checklist de critérios de aceite).
