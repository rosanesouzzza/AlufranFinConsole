# Console Financeiro Alufran

**Plataforma de Fechamento Financeiro Governado**

**Stack:** .NET 8 + ASP.NET Core + SQLite + EF Core + Razor Pages + JWT

---

## ⚡ Como Acessar (Startup em 2 minutos)

### Pré-requisitos
- **.NET 8** (Visual Studio 2022 Community ou CLI)
- Nenhuma dependência externa (SQLite embutido)

### 1️⃣ Clonar/Abrir Projeto
```bash
cd "path\to\AlufranFinConsole"
dotnet restore
```

### 2️⃣ Criar/Atualizar Banco de Dados
```bash
# Aplicar todas as migrations
dotnet ef database update --project AlufranFinConsole.Infrastructure --startup-project AlufranFinConsole.Api
```

### 3️⃣ Rodar API (porta 5001)
```bash
cd AlufranFinConsole.Api
dotnet run
# Acesso: https://localhost:5001/swagger (Swagger UI)
```

### 4️⃣ Em outro terminal: Rodar Web Frontend (porta 5002)
```bash
cd AlufranFinConsole.Web
dotnet run
# Acesso: https://localhost:5002
```

---

## 🔐 Credenciais Padrão

**Login automático criado:**
- **Email:** `admin@alufran.local`
- **Senha:** `AlufranAdmin@2026`

Encontrado em: `Program.cs` (linhas 171-172)

---

## 📊 Acesso ao Staging (Phase 4)

1. Acesse **https://localhost:5002**
2. Faça login com as credenciais acima
3. Clique em **📋 Staging** no navbar
4. Selecione um arquivo de importação
5. Clique em **Validar** → **Sanitizar** → **Relatório**

---

## 📁 Arquitetura de Projetos

```
AlufranFinConsole/
├── AlufranFinConsole.Domain/              # Entidades & interfaces
│   └── Entities/
│       ├── ImportFile.cs
│       ├── StagingData.cs
│       └── ...
│
├── AlufranFinConsole.Application/         # Serviços & lógica
│   └── Services/
│       ├── DataValidationService.cs       # Validação CSV
│       ├── TextNormalizationService.cs    # Normalização
│       └── FileUploadService.cs
│
├── AlufranFinConsole.Infrastructure/      # EF Core & BD
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   └── Migrations/
│   └── Storage/
│
├── AlufranFinConsole.Api/                 # API REST
│   ├── Program.cs                         # JWT setup, DI
│   └── Controllers/
│       ├── AuthController.cs              # Login/Register
│       ├── StagingController.cs           # Workflow validação
│       └── ImportController.cs            # Lista arquivos
│
├── AlufranFinConsole.Web/                 # Frontend Razor Pages
│   ├── Pages/
│   │   ├── Index.cshtml                   # Home
│   │   ├── Staging.cshtml                 # Phase 4 UI
│   │   └── ...
│   └── Controllers/
│       └── UploadController.cs            # Upload de arquivos
│
└── AlufranFinConsole.Tests/               # Testes unitários
```

---

## 🗄️ Banco de Dados

**Tipo:** SQLite (arquivo: `alufran_console.db`)

**Localização:** `bin/Debug/net8.0/` ou `bin/Release/net8.0/`

**Connection String:**
```json
// appsettings.json
"DefaultConnection": "Data Source=alufran_console.db"
```

---

## 🚀 Fases Implementadas

| Fase | Status | Descrição |
|------|--------|-----------|
| **1** | ✅ | Fundação (Estrutura, Auth, BD, Migrations) |
| **2** | ✅ | Cadastros (Empresas, Unidades, Fornecedores, Clientes) |
| **3** | ✅ | Upload & Versionamento (ImportFiles, FileUploadService) |
| **4** | ✅ | **Staging & Saneamento (DataValidation, Staging UI)** |
| **5** | ⏳ | Classificação & QA |
| **6-9** | ⏳ | Consolidado, DRE, Aprovação, Auditoria |

---

## 🔑 Endpoints Principais (API)

### Autenticação
```
POST   /api/auth/login           - Login com email/senha
POST   /api/auth/register        - Registrar novo usuário
```

### Staging (Phase 4)
```
GET    /api/staging/{id}         - Listar registros de staging
GET    /api/staging/{id}/summary - Resumo de estatísticas
POST   /api/staging/{id}/validate - Validar dados pendentes
POST   /api/staging/{id}/sanitize - Sanitizar dados válidos
GET    /api/staging/{id}/report  - Relatório detalhado
```

### Importação
```
GET    /api/import/list          - Listar arquivos de importação
GET    /api/import/{id}          - Detalhes do arquivo
```

---

## 🧪 Testes

```bash
# Rodar todos os testes
dotnet test

# Com cobertura
dotnet test /p:CollectCoverage=true
```

---

## 🔧 Troubleshooting

| Erro | Solução |
|------|---------|
| **"Connection refused"** | API não está rodando. Execute `dotnet run` em AlufranFinConsole.Api |
| **"404 Not Found" ao acessar staging** | Certifique-se de estar logado e que a API está rodando |
| **"JWT signature validation failed"** | Token expirado ou inválido. Faça login novamente |
| **Migrations não aplicadas** | Execute: `dotnet ef database update` na pasta raiz |

---

## 📝 Próximos Passos

1. ✅ **Phase 4 Backend & Frontend** (concluído)
2. ⏳ Implementar validação para tipos FAT, EMITIDAS, COMP, TRANSF, FOPAG
3. ⏳ Testes unitários para DataValidationService
4. ⏳ Phase 5: Classificação & QA

---

## 📚 Documentação Adicional

- `PHASE4_PLAN.md` — Detalhes de Phase 4
- `ARCHITECTURE.md` — Decisões arquiteturais
- `SETUP_GUIDE.md` — Guia detalhado de instalação

---

**Versão:** 2.0  
**Última Atualização:** 2026-05-10  
**Status:** Phase 4 ✅ | Phase 5+ ⏳
