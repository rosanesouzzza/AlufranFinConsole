# Phase 4: Staging & Saneamento de Dados

**Objetivo**: Criar camada intermediária para validação e saneamento de dados antes de processamento

---

## 📋 Tarefas

### 1. Entidade StagingData
```
Domain/Entities/StagingData.cs
├── Id: int
├── ImportFile_Id: int (FK)
├── LineNumber: int
├── RawData: string (linha bruta do arquivo)
├── ParsedData: JSON (campos parseados)
├── ValidationStatus: enum (PENDING, VALID, INVALID, DUPLICATE)
├── ValidationErrors: JSON (erros encontrados)
├── SanitizedData: JSON (dados após saneamento)
├── ProcessedAt: DateTime?
├── CreatedAt: DateTime
├── UpdatedAt: DateTime?
```

### 2. Service: DataValidationService
```
Application/Services/DataValidationService.cs
├── ValidateLine(fileType, rawData) → ValidationResult
├── SanitizeData(parsedData) → SanitizedData
├── DetectDuplicates(fileType, data) → bool
├── ApplyBusinessRules(data, rules) → ValidationResult
└── GenerateValidationReport(stagingId) → Report
```

### 3. EF Migration
```
Infrastructure/Migrations/
└── 20260510xxxxxx_AddStagingData.cs
    └── CreateTable(StagingData)
```

### 4. Controller: StagingController
```
Api/Controllers/StagingController.cs
├── GET /api/staging/{importFileId}
│   └── Listar todas as linhas para um arquivo
│
├── GET /api/staging/{importFileId}/summary
│   └── Resumo: total, válidos, inválidos, duplicados
│
├── GET /api/staging/{importFileId}/validate
│   └── Validar todas as linhas pendentes
│
├── POST /api/staging/{importFileId}/sanitize
│   └── Sanear dados válidos
│
└── GET /api/staging/{importFileId}/report
    └── Relatório detalhado de validação
```

---

## 🗂️ Arquivos a Criar

1. `Domain/Entities/StagingData.cs` — Entidade
2. `Application/Services/DataValidationService.cs` — Serviço
3. `Infrastructure/Migrations/20260510xxxxxx_AddStagingData.cs` — Migração
4. `Api/Controllers/StagingController.cs` — Controller
5. `PHASE4_COMPLETION_LOG.md` — Documentação

---

## ✅ Checklist

- [x] StagingData entity criada
- [x] Migration gerada e testada (20260510000839_AddStagingData)
- [x] DbContext configurado com DbSet<StagingData>
- [x] DataValidationService implementado (PAG, REC types; stubs for FAT, EMITIDAS, COMP, TRANSF, FOPAG)
- [x] StagingController criado (5 endpoints)
- [ ] Testes unitários
- [ ] Documentação detalhada
- [ ] Deploy e testes em staging

## 📝 Status: Fundação Implementada

**Commit**: 139028b - Phase 4: Staging & Data Sanitization

**O que foi entregue**:
- ✅ Camada de staging completa
- ✅ Pipeline de validação (RawData → ParsedData → SanitizedData)
- ✅ Endpoints da API para toda a workflow
- ✅ Suporte para múltiplos tipos de arquivo
- ✅ Validação estruturada com regras de negócio
- ✅ Normalização de dados

**Próximos passos**:
1. Implementar validação e saneamento para tipos restantes (FAT, EMITIDAS, COMP, TRANSF, FOPAG)
2. Testes unitários para DataValidationService
3. Testes de integração da API
4. Testes e2e com arquivos reais

---

## Próximas Fases

- **Phase 5**: Classificação & QA
- **Phase 6**: Consolidado Financeiro
- **Phase 7**: DRE & Análises
- **Phase 8**: Aprovação & Snapshot
- **Phase 9**: Auditoria & Exportação
