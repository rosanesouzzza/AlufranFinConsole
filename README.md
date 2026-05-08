# Console Financeiro Alufran

**Stack 100% Gratuito:** .NET 8 + Blazor Server + PostgreSQL + EF Core

---

## ⚡ Startup Rápido (5 minutos)

### Pré-requisitos
- **.NET 8** (Visual Studio Community ou CLI)
- **PostgreSQL** 15+ local ou servidor

### 1. Setup PostgreSQL

```bash
# Windows: Abrir PowerShell como admin
psql -U postgres

# SQL:
CREATE DATABASE alufran_fin_close;
CREATE USER alufran WITH PASSWORD 'alufran';
ALTER ROLE alufran WITH CREATEDB;
GRANT ALL PRIVILEGES ON DATABASE alufran_fin_close TO alufran;
```

Ou rodando o script:
```bash
psql -U postgres < setup_db.sql
```

### 2. Restaurar Dependências
```bash
cd AlufranFinConsole
dotnet restore
```

### 3. Criar Migrations
```bash
cd AlufranFinConsole.Web
dotnet ef database update
```

### 4. Rodar Aplicação
```bash
dotnet run
```

Acesse: `https://localhost:5001`

---

## 📁 Estrutura de Projetos

```
AlufranFinConsole/
├── AlufranFinConsole.Domain/        # Entidades core
├── AlufranFinConsole.Application/   # Lógica de aplicação
├── AlufranFinConsole.Infrastructure # EF Core, Storage, Serviços
├── AlufranFinConsole.Api/           # Controllers REST
├── AlufranFinConsole.Web/           # Blazor Server UI
└── AlufranFinConsole.Tests/         # Unit tests
```

---

## 🗄️ Database Connection

**Arquivo:** `AlufranFinConsole.Web/appsettings.json`

```json
"DefaultConnection": "Host=localhost;Port=5432;Database=alufran_fin_close;Username=alufran;Password=alufran"
```

---

## 🚀 Fases Implementadas

**Fase 1:** ✅ Fundação (Estrutura, Auth, BD)  
**Fase 2-9:** ⏳ Em desenvolvimento

---

## 📝 Próximos Passos

1. ✅ Executar `dotnet build` para validar
2. ✅ Executar migrações (`dotnet ef database update`)
3. ✅ Rodar aplicação (`dotnet run`)
4. ⏳ Implementar cadastros (Fase 2)
5. ⏳ Upload e versionamento (Fase 3)

---

## 🔧 Troubleshooting

**Erro: "connection refused"**
→ Verificar se PostgreSQL está rodando

**Erro: "database does not exist"**
→ Rodar `dotnet ef database update` novamente

---

**Versão:** 1.0  
**Data:** 2026-05-07
