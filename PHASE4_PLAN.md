# 📊 Phase 4: Staging & Saneamento (Data Cleansing)

**Objetivo:** Criar camada de staging para validação e limpeza de dados financeiros  
**Prioridade:** Alta (requisito crítico para precisão fiscal)  
**Duração Estimada:** 3-4 dias

---

## 🎯 Objetivos Phase 4

### 1. Criar Entidade StagingData
Representar dados brutos do arquivo antes da classificação:

```csharp
public class StagingData
{
    public int Id { get; set; }
    public int ImportFile_Id { get; set; }
    public string RawLine { get; set; }           // Linha original do CSV
    public int LineNumber { get; set; }
    public string[] ColumnValues { get; set; }   // Valores parseados
    public string Status { get; set; }            // VALID, INVALID, CLEANED, REJECTED
    public string? ValidationErrors { get; set; } // JSON com lista de erros
    public Dictionary<string, object>? CleanedData { get; set; } // JSON com dados sanitizados
    public DateTime ProcessedAt { get; set; }
    public string ProcessedBy_Id { get; set; }
    
    public virtual ImportFile ImportFile { get; set; }
    public virtual User ProcessedBy { get; set; }
}
```

### 2. Implementar Serviço de Validação
**DataValidationService** — Validar cada linha do CSV:

- ✅ Número correto de colunas
- ✅ Tipos de dados corretos (data, número, texto)
- ✅ Campos obrigatórios preenchidos
- ✅ Intervalos válidos (datas, valores)
- ✅ Formato consistente
- ✅ Caracteres especiais detectados

### 3. Implementar Limpeza de Dados
**DataCleansingService** — Sanitizar dados:

- ✅ Remove espaços extras (trim, múltiplos espaços)
- ✅ Normaliza datas (detecta formatos: DD/MM/YYYY, YYYY-MM-DD, etc.)
- ✅ Corrige acentuação (ENEL → ENEL, São Paulo → SAO PAULO)
- ✅ Remove CHAR(160) e caracteres invisíveis
- ✅ Padroniza separadores decimais (. ou ,)
- ✅ Remove caracteres especiais inválidos
- ✅ Converte para caixa padrão (MAIÚSCULA para PAG/REC)

### 4. Mapeamento de Colunas
**ColumnMapping** — Já criado em Phase 2, usar para:

```csharp
// Exemplo: arquivo FAT (Faturas)
{
  "FileType": "FAT",
  "Columns": [
    { "Position": 0, "Name": "InvoiceNumber", "Type": "string", "Required": true },
    { "Position": 1, "Name": "InvoiceDate", "Type": "date", "Required": true },
    { "Position": 2, "Name": "SupplierName", "Type": "string", "Required": true },
    { "Position": 3, "Name": "Amount", "Type": "decimal", "Required": true },
    { "Position": 4, "Name": "Description", "Type": "string", "Required": false }
  ]
}
```

### 5. Criar StagingController
**Endpoints:**

```bash
# Processar arquivo enviado
POST /api/staging/{importFileId}/process

# Listar linhas staging de um arquivo
GET /api/staging?importFileId=123&status=INVALID&limit=100

# Ver detalhes de linha específica
GET /api/staging/{stagingId}

# Corrigir e reprocessar linha
POST /api/staging/{stagingId}/fix
Body: { "correctedValues": [...] }

# Relatório de qualidade
GET /api/staging/report/{importFileId}
```

### 6. QA Report
Gerar relatório de qualidade:

```json
{
  "importFileId": 123,
  "totalLines": 1000,
  "validLines": 980,
  "invalidLines": 20,
  "cleanedLines": 950,
  "rejectedLines": 30,
  "errorRate": "2.0%",
  "commonErrors": [
    { "error": "Invalid date format", "count": 15 },
    { "error": "Missing supplier name", "count": 5 }
  ],
  "columnQuality": {
    "InvoiceNumber": "100%",
    "InvoiceDate": "99.5%",
    "SupplierName": "98%",
    "Amount": "99.8%"
  },
  "readyForClassification": true
}
```

---

## 📁 Estrutura de Arquivos Phase 4

```
Domain/Entities/
├── StagingData.cs                 ← Criar
├── ImportFile.cs                  ✅ (já existe)
├── ColumnMapping.cs               ✅ (já existe)

Application/Services/
├── DataValidationService.cs       ← Criar
├── DataCleansingService.cs        ← Criar
├── TextNormalizationService.cs    ✅ (já existe)

Api/Controllers/
├── StagingController.cs           ← Criar
├── UploadController.cs            ✅ (já existe)

Infrastructure/
├── ApplicationDbContext.cs        ← Atualizar (adicionar DbSet<StagingData>)
├── Migrations/
│   └── 20260508000003_Phase4_StagingAndCleansing.cs  ← Criar
```

---

## 🔄 Fluxo de Processamento

```
1. Upload (Phase 3)
   └─ ImportFile criado (status: PENDING)
   
2. Staging (Phase 4)
   ├─ Ler arquivo ImportFile
   ├─ Parsear CSV conforme ColumnMapping
   ├─ Para cada linha:
   │  ├─ Validar (DataValidationService)
   │  ├─ Se válida: limpar (DataCleansingService)
   │  ├─ Se inválida: marcar erro
   │  └─ Criar StagingData record
   ├─ Gerar QA Report
   └─ Atualizar ImportFile (status: STAGING_COMPLETE)
   
3. Classificação (Phase 5)
   └─ Usar StagingData para classificar em contas
```

---

## 💾 Database Migration

```csharp
// 20260508000003_Phase4_StagingAndCleansing.cs

migrationBuilder.CreateTable(
    name: "staging_data",
    columns: table => new
    {
        id = table.Column<int>(nullable: false)
            .Annotation("Sqlite:Autoincrement", true),
        import_file_id = table.Column<int>(nullable: false),
        raw_line = table.Column<string>(nullable: false),
        line_number = table.Column<int>(nullable: false),
        column_values = table.Column<string>(nullable: true), // JSON array
        status = table.Column<string>(nullable: true),        // VALID, INVALID, CLEANED, REJECTED
        validation_errors = table.Column<string>(nullable: true), // JSON
        cleaned_data = table.Column<string>(nullable: true),  // JSON
        processed_at = table.Column<DateTime>(nullable: false),
        processed_by_id = table.Column<string>(nullable: false),
        created_at = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
    },
    constraints: table =>
    {
        table.PrimaryKey("pk_staging_data", x => x.id);
        table.ForeignKey("fk_staging_data_import_files", 
            x => x.import_file_id, "import_files", "id", onDelete: ReferentialAction.Cascade);
        table.ForeignKey("fk_staging_data_users", 
            x => x.processed_by_id, "AspNetUsers", "Id", onDelete: ReferentialAction.Restrict);
    });

// Índices
migrationBuilder.CreateIndex("ix_staging_data_import_file_id", "staging_data", "import_file_id");
migrationBuilder.CreateIndex("ix_staging_data_status", "staging_data", "status");
migrationBuilder.CreateIndex("ix_staging_data_processed_at", "staging_data", "processed_at");
```

---

## 🧪 Testes Necessários

```csharp
[TestClass]
public class DataValidationServiceTests
{
    [TestMethod]
    public void ValidateLine_WithAllValidColumns_ReturnsValid() { }
    
    [TestMethod]
    public void ValidateLine_WithMissingRequired_ReturnErrors() { }
    
    [TestMethod]
    public void ValidateLine_WithInvalidDate_ReturnError() { }
}

[TestClass]
public class DataCleansingServiceTests
{
    [TestMethod]
    public void CleanSupplierName_RemovesAccents_Success() { }
    
    [TestMethod]
    public void CleanDate_DetectsMultipleFormats_Success() { }
    
    [TestMethod]
    public void CleanAmount_FixesDecimalSeparator_Success() { }
}

[TestClass]
public class StagingControllerTests
{
    [TestMethod]
    public async Task ProcessFile_WithValidCSV_CreatesStaging() { }
    
    [TestMethod]
    public async Task ProcessFile_WithErrors_GeneratesReport() { }
}
```

---

## 📝 Exemplo de Uso

### Upload e Processar Arquivo

```bash
# 1. Login
TOKEN=$(curl -s -X POST "https://api.alufran.local/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@alufran.local","password":"AlufranAdmin@2026"}' | jq -r '.token')

# 2. Upload (Phase 3)
IMPORT_ID=$(curl -s -X POST "https://api.alufran.local/api/upload" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@faturas_2026-05.csv" \
  -F "fileType=FAT" \
  -F "competence=2026-05" | jq -r '.id')

# 3. Processar staging (Phase 4)
curl -X POST "https://api.alufran.local/api/staging/$IMPORT_ID/process" \
  -H "Authorization: Bearer $TOKEN"

# 4. Ver relatório de qualidade
curl -X GET "https://api.alufran.local/api/staging/report/$IMPORT_ID" \
  -H "Authorization: Bearer $TOKEN" | jq '.errorRate, .commonErrors'

# 5. Corrigir linha específica
curl -X POST "https://api.alufran.local/api/staging/456/fix" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"correctedValues": ["FAT-001", "2026-05-01", "ENEL", "1500.00"]}'
```

---

## ✅ Definição de Pronto (Definition of Done)

- [ ] StagingData entity criado com todas as propriedades
- [ ] Migration criada e testada em SQLite
- [ ] DataValidationService implementado e testado
- [ ] DataCleansingService implementado e testado
- [ ] StagingController com endpoints GET/POST/DELETE
- [ ] QA Report gerado em JSON
- [ ] Testes unitários com cobertura > 80%
- [ ] Documentação atualizada (UPLOAD_API.md)
- [ ] Deployado em Render com sucesso
- [ ] Validação manual com arquivo real (FAT, REC, PAG, etc.)

---

## 🗺️ Timeline Estimada

```
2026-05-08 05:00 — Phase 4 início
2026-05-08 08:00 — StagingData + Migration
2026-05-08 12:00 — DataValidationService + Tests
2026-05-08 15:00 — DataCleansingService + Tests
2026-05-08 17:00 — StagingController + Endpoints
2026-05-08 19:00 — QA Report + Integration
2026-05-08 20:00 — Render deployment
2026-05-08 20:30 — Manual testing + Sign-off
```

---

## 📚 Referências

- **CSV Parsing:** Use CsvHelper (Nuget package)
- **Date Parsing:** Use DateTime.TryParseExact() com múltiplos formatos
- **Decimal Normalization:** Use decimal.Parse() com CultureInfo
- **Text Normalization:** TextNormalizationService (já implementado)
- **JSON Storage:** System.Text.Json para serialização

---

*Phase 4: Staging & Saneamento — PRONTO PARA INICIAR*
