# Checkpoint 2 — ExpenseHub

API corporativa de reembolsos desenvolvida em C# com ASP.NET Core, Identity, Entity Framework Core e SQLite.

## Implementado

- ASP.NET Core Identity persistido em banco relacional;
- autenticação Bearer pelos endpoints `POST /register` e `POST /login`;
- roles `Admin`, `Employee`, `Approver`, `Finance` e `Auditor`;
- seed idempotente das roles e de uma conta Admin inicial;
- cadastro sem aceitar role do cliente;
- administração segura de roles;
- criação e edição de reembolso em `Draft`;
- fluxo `Draft -> Submitted -> Approved -> Paid`;
- reprovação `Submitted -> Rejected`;
- proibição de autoaprovação e autopagamento;
- filtros de leitura por role, ownership e estado;
- pagamento simulado e histórico auditável;
- testes unitários sem banco, rede ou serviços externos.

## Banco de dados

Provider: SQLite, pacote `Microsoft.EntityFrameworkCore.Sqlite`.

Configuração padrão:

```text
Data Source=expensehub.db
```

A inicialização usa `Database.EnsureCreatedAsync()`, fornecendo um procedimento reproduzível para criar a estrutura local. Arquivos `.db` não são versionados.

## Admin inicial

A senha não é armazenada no repositório. Configure antes da primeira execução:

PowerShell:

```powershell
$env:ExpenseHub__AdminPassword="SuaSenhaForteAqui123!"
```

Bash:

```bash
export ExpenseHub__AdminPassword='SuaSenhaForteAqui123!'
```

E-mail padrão:

```text
admin@expensehub.local
```

O e-mail pode ser alterado com `ExpenseHub__AdminEmail`.

## Executar e validar

Na raiz:

```bash
dotnet restore ./sources/ExpenseHub.slnx
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
dotnet run --project ./sources/ExpenseHub.Api/ExpenseHub.Api.csproj
```

Health check:

```text
GET /health
```

## Endpoints

| Método | Rota |
|---|---|
| POST | `/register` |
| POST | `/login` |
| GET | `/api/admin/users` |
| PUT | `/api/admin/users/{id}/roles` |
| POST | `/api/expenses` |
| PUT | `/api/expenses/{id}` |
| GET | `/api/expenses` |
| GET | `/api/expenses/{id}` |
| POST | `/api/expenses/{id}/submit` |
| POST | `/api/expenses/{id}/approve` |
| POST | `/api/expenses/{id}/reject` |
| POST | `/api/expenses/{id}/pay` |
| GET | `/api/expenses/{id}/history` |

## Regras principais

- descrição: 10 a 500 caracteres;
- valor: R$ 0,01 a R$ 2.147.483.647,00 usando `decimal`;
- data da despesa: válida e não futura;
- justificativa de reprovação: 10 a 500 caracteres;
- proprietário, estado, atores e horários são definidos pelo servidor;
- `Rejected` e `Paid` são estados finais;
- transição incompatível ou repetida retorna `409`;
- recurso inexistente ou fora do escopo de leitura retorna `404`;
- `Admin` sozinho não recebe acesso funcional a reembolsos.

Após alteração de roles, o usuário deve fazer login novamente para obter uma credencial atualizada.

## Testes

Os testes em `sources/ExpenseHub.UnitTests` cobrem transições válidas e inválidas, ownership, autoaprovação, autopagamento, validações, visibilidade por perfil e histórico.

## Qualidade

Os arquivos oficiais de `.editorconfig`, `Directory.Build.props`, scripts e workflow `code-quality` foram preservados. Antes da entrega, confirme que o workflow final está verde e registre o SHA final da `main`.
