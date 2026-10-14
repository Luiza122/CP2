# Mapeamento da implementação

## I04 — Criação e edição de Draft

- A criação define o proprietário a partir da identidade autenticada e inicia em `Draft`.
- A edição exige o proprietário correto e o estado `Draft`.
- `ExpenseDraftOwnershipTests.cs` valida criação, alteração autorizada, tentativa por outro usuário e edição após envio.

## I05 — Envio, listagem e consulta

- `POST /api/expenses/{id}/submit` executa a transição `Draft → Submitted`.
- Listagem e detalhe aplicam a visibilidade por perfil antes de materializar os resultados.
- Recursos inexistentes ou fora do escopo de leitura retornam `404`; transições incompatíveis retornam `409`.
- `ExpenseServiceSubmitAndQueryTests.cs` cobre envio, ownership, duplicidade, filtros por perfil e detalhes protegidos.

## I06 — Ownership e matriz de acesso

- As permissões são verificadas na camada de serviço combinando role, proprietário e estado.
- Approver não decide sobre o próprio reembolso; Finance não paga o próprio reembolso.
- Auditor consulta o histórico sem receber capacidade de alteração; Admin não ganha acesso funcional apenas por ser Admin.
- `ExpenseServiceAuthorizationTests.cs` valida decisões sobre a própria despesa, roles insuficientes e visibilidade do histórico.

## I07 — Aprovação e reprovação

- Aprovação executa `Submitted → Approved`; reprovação executa `Submitted → Rejected`.
- Somente Approver pode decidir, sem aprovar ou reprovar a própria despesa.
- Justificativa da reprovação deve ter de 10 a 500 caracteres.
- `ExpenseServiceDecisionTests.cs` cobre permissões, justificativa, ator, timestamp, transição e prevenção de histórico duplicado.

## I08 — Pagamento e histórico

- Pagamento executa `Approved → Paid` somente por Finance que não seja proprietário.
- `PaymentRecord` e `ExpenseHistory` registram ator e horário definidos pelo servidor.
- A unicidade do pagamento por despesa e a persistência conjunta previnem duplicidade e divergência quando há falha de banco.
- `ExpenseServicePaymentAndHistoryTests.cs` cobre pagamento válido, estado inválido, pagamento repetido, histórico ordenado e falha de persistência.

## I09 — Testes

Os testes adicionados usam SQLite em memória para validar as consultas e gravações do serviço sem depender de um banco externo ou serviço remoto. Os testes de domínio existentes continuam cobrindo as regras centrais.

## I10 — Qualidade

O workflow, os analisadores, `.editorconfig`, `Directory.Build.props` e scripts oficiais são mantidos. Antes da entrega, confirmar CI verde e registrar o SHA final da branch principal.
