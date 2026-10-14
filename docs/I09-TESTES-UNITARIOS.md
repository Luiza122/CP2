# I09 — Testes unitários

Referência oficial: `Racass/checkpoint-csharpracass-expensehub#9`.

## Objetivo

A suíte em `sources/ExpenseHub.UnitTests` cobre as regras centrais do ExpenseHub sem depender de banco de dados, rede ou serviço externo.

## Mapeamento da rubrica

### Estados e transições — 2%

Coberto por:

- `ExpenseWorkflowTests`
  - criação inicia em `Draft`;
  - `Draft -> Submitted`;
  - `Submitted -> Approved`;
  - `Submitted -> Rejected`;
  - `Approved -> Paid`;
  - edição somente em `Draft`.

- `ExpenseInvalidTransitionTests`
  - submit repetido retorna conflito;
  - aprovação a partir de `Draft` é inválida;
  - reprovação a partir de `Draft` é inválida;
  - pagamento antes de `Approved` é inválido;
  - `Rejected` permanece final;
  - `Paid` permanece final.

### Ownership e negações — 3%

Coberto por:

- `ExpenseWorkflowTests.Submit_ByDifferentOwner_IsForbidden`;
- `ExpenseWorkflowTests.Approve_ByOwner_IsForbidden`;
- `ExpenseWorkflowTests.Pay_ByOwner_IsForbidden`;
- `ExpenseValidationAndHistoryTests.Reject_ByOwner_IsForbiddenAndKeepsSubmittedState`;
- `ExpenseInvalidTransitionTests.Edit_DraftByDifferentOwner_IsForbidden`;
- `ExpenseAccessPolicyTests.Employee_CanReadOnlyOwnExpense`;
- negativos de visibilidade para Approver, Finance e Admin.

### Decisões e pagamento — 2%

Coberto por:

- aprovação válida;
- reprovação válida;
- justificativa obrigatória;
- autopagamento proibido;
- pagamento válido;
- pagamento repetido proibido;
- tentativa de pagamento em estado incorreto.

### Histórico e validações — 1%

Coberto por:

- ator do histórico;
- instante UTC;
- estado anterior;
- novo estado;
- justificativa;
- valor mínimo e máximo;
- descrição mínima e máxima;
- data atual aceita;
- data futura rejeitada;
- justificativa mínima e máxima;
- justificativa em branco rejeitada.

### Isolamento e valor contra regressão — 2%

- testes usam apenas regras de domínio e política de acesso;
- não acessam banco, rede ou serviço externo;
- nomes dos testes descrevem a regra;
- assertivas verificam estado, ownership, autorização e limites;
- mudanças simples nas regras de transição, validação ou visibilidade fazem os testes correspondentes falharem.

## Arquivos principais

- `ExpenseWorkflowTests.cs`
- `ExpenseInvalidTransitionTests.cs`
- `ExpenseAccessPolicyTests.cs`
- `ExpenseValidationAndHistoryTests.cs`
- `ExpenseValidationBoundaryTests.cs`

## Validação

Execute na raiz do repositório:

```shell
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
```

A entrega deve manter o workflow oficial `code-quality`, os analisadores, `.editorconfig`, `Directory.Build.props` e scripts sem redução de severidade.
