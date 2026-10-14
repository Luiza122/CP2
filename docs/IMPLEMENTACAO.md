# Mapeamento da implementação

## I01 — Fundação e Entity Framework Core

- SQLite como banco relacional, com Entity Framework Core em `Data/ExpenseHubDbContext.cs`.
- Identity, despesas, categorias, histórico e pagamentos persistidos no mesmo contexto.
- Chaves, relacionamentos, obrigatoriedade, limites de tamanho, precisão monetária e unicidade do pagamento configurados no modelo.
- Inicialização reproduzível com `EnsureCreatedAsync()`; a solução atual não possui migrations versionadas.
- String de conexão padrão `Data Source=expensehub.db`, configurável por `ConnectionStrings__ExpenseHub`.
- `ExpenseHubDbContextModelTests.cs` valida os mapeamentos sem precisar iniciar a API.

## I02 — Identity, Admin e autenticação

- ASP.NET Core Identity com persistência relacional e autenticação Bearer por `AddIdentityApiEndpoints` e `MapIdentityApi`.
- Roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor`.
- Seed idempotente: cria as roles e a conta Admin apenas quando necessário.
- A senha inicial vem de `ExpenseHub__AdminPassword`; não deve ser armazenada no repositório.
- `IdentitySeedTests.cs` verifica senha configurada, roles obrigatórias e execução repetida sem duplicações.

## I03 — Cadastro e gerenciamento de roles

- O cadastro padrão do Identity não recebe role enviada pelo cliente.
- `GET /api/admin/users` e `PUT /api/admin/users/{id}/roles` exigem a role Admin.
- `RoleManagementRules` restringe alterações às roles conhecidas e impede Admin de remover sua própria role.
- `RoleManagementRulesTests.cs` cobre roles válidas/desconhecidas, entrada nula e tentativa de auto-remoção da role Admin.

## I04–I08 — Fluxo de reembolso

- Regras de estado em `Domain/Expense.cs` e validações em `Domain/ExpenseRules.cs`.
- Ownership, roles e visibilidade contextual na camada de serviço.
- Histórico registra ator, horário, estado anterior/novo e justificativa quando aplicável.
- Pagamento simulado é persistido com `PaymentRecord` e evento de histórico.

## I09 — Testes

Os testes da solução cobrem regras de domínio, mapeamento do EF Core, identidade/seed e regras de autorização. Os testes que usam SQLite em memória não dependem de banco externo ou serviços remotos.

## I10 — Qualidade

Workflow, analisadores, `.editorconfig`, `Directory.Build.props` e scripts oficiais do template são mantidos. Antes da entrega, confirmar o resultado final do GitHub Actions e registrar o SHA final da branch principal.
