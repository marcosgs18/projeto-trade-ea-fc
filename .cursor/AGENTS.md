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