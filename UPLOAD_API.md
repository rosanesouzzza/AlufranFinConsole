# 📤 File Upload API — Phase 3

## Overview

The Upload API supports financial file uploads with versioning, integrity checking (MD5 hash), and automatic duplicate detection.

**Base URL:** `https://alufranfinconsole.onrender.com/api/upload`

## Authentication

All endpoints require **JWT Bearer Token**. Obtain token from `/api/auth/login`:

```bash
curl -X POST https://alufranfinconsole.onrender.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@alufran.local","password":"AlufranAdmin@2026"}'
```

## Supported File Types

- **PAG** — Pagamentos
- **REC** — Recebimentos  
- **FAT** — Faturas
- **EMITIDAS** — Notas Emitidas
- **COMP** — Compras
- **TRANSF** — Transferências
- **FOPAG** — Formas de Pagamento

## Endpoints

### Upload File
**POST** `/api/upload`

```bash
curl -X POST https://alufranfinconsole.onrender.com/api/upload \
  -H "Authorization: Bearer {token}" \
  -F "file=@invoice.csv" \
  -F "fileType=FAT" \
  -F "competence=2026-05"
```

### Get File Details
**GET** `/api/upload/{id}`

### List Files
**GET** `/api/upload?fileType=FAT&competence=2026-05&limit=50`

### Get Statistics  
**GET** `/api/upload/stats?competence=2026-05`

## Status Codes

- **PENDING** — File uploaded, awaiting processing
- **PROCESSING** — File is being validated
- **COMPLETED** — File processed successfully
- **FAILED** — File processing failed

## File Hash Verification

All files are stored with MD5 hash for integrity checking and duplicate detection.

---

*Phase 3: Upload & Versioning — Complete*
