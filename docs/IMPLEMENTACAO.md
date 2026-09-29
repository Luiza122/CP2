# Mapeamento da implementação

## I01 — Fundação e EF Core

- `sources/ExpenseHub.Api/Data/ExpenseHubDbContext.cs`
- SQLite e Entity Framework Core;
- entidades `Expense`, `ExpenseCategory`, `ExpenseHistory` e `PaymentRecord`;
- criação reproduzível do banco com `EnsureCreatedAsync`.

## I02 — Identity, Admin e autenticação

- ASP.NET Core Identity persistido;
- autenticação Bearer com `AddIdentityApiEndpoints` e `MapIdentityApi`;
- roles obrigatórias;
- seed idempotente e senha inicial fora do código-fonte.

## I03 — Cadastro e roles

- `/register` não recebe role;
- endpoints Admin em `Program.cs`;
- somente roles conhecidas são aceitas;
- Admin não remove a própria role Admin.

## I04–I08 — Fluxo corporativo

- regras de estado em `Domain/Expense.cs`;
- validações em `Domain/ExpenseRules.cs`;
- autorização contextual e filtros em `Services/ExpenseService.cs`;
- pagamento e histórico persistidos junto com a alteração correspondente.

## I09 — Testes

- `ExpenseWorkflowTests.cs`;
- `ExpenseAccessPolicyTests.cs`;
- `ExpenseValidationAndHistoryTests.cs`.

Os testes são unitários e não dependem de banco ou rede.

## I10 — Qualidade

O workflow, os analisadores, o `.editorconfig`, o `Directory.Build.props` e os scripts oficiais do template foram mantidos sem redução de severidade ou remoção de etapas.
