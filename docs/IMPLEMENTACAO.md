# Mapeamento da implementação

## I01 — Fundação e Entity Framework Core

- Solução: `sources/ExpenseHub.slnx`, com API e projeto de testes separados.
- Provider relacional: SQLite, pelo pacote `Microsoft.EntityFrameworkCore.Sqlite`.
- Contexto e Identity: `sources/ExpenseHub.Api/Data/ExpenseHubDbContext.cs`.
- Entidades persistidas: usuários/roles do Identity, `Expense`, `ExpenseCategory`, `ExpenseHistory` e `PaymentRecord`.
- Relacionamentos, chaves, campos obrigatórios, limites de tamanho, precisão monetária e exclusões estão configurados em `OnModelCreating`.
- O banco local é inicializado por `Database.EnsureCreatedAsync()` durante a inicialização. Esse mecanismo cria a estrutura a partir do modelo atual; não há migrations versionadas neste projeto.
- String de conexão padrão: `Data Source=expensehub.db`. Pode ser substituída pela configuração `ConnectionStrings:ExpenseHub` (variável de ambiente `ConnectionStrings__ExpenseHub`).
- O arquivo do banco é local e deve permanecer fora do Git.

### Configurar e executar

Na raiz do repositório:

```bash
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Para criar o banco local, basta iniciar a API. O banco é criado em tempo de execução; não é necessário manter um servidor externo ligado para restaurar, compilar ou executar os testes unitários. Os testes de domínio não abrem conexão com o banco.

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

Os testes unitários são executados sem banco, rede ou serviços externos.

## I10 — Qualidade

O workflow, os analisadores, o `.editorconfig`, o `Directory.Build.props` e os scripts oficiais do template foram mantidos sem redução de severidade ou remoção de etapas.
