# ⚡ Quick Start — Alufran Financial Console

**Target Audience:** Developers testing Phase 3 & Phase 4  
**Last Updated:** 2026-05-08

---

## 🔑 Quick Commands

### Check Render Deployment Status
```bash
# Open dashboard
open https://dashboard.render.com

# Or test API directly
curl https://alufranfinconsole.onrender.com/api/auth/login \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@alufran.local","password":"AlufranAdmin@2026"}'

# Expected: { "token": "eyJ..." }
```

### Test Upload Workflow (Once API is Live)
```bash
# 1. Save as test.csv in project root
cat > test.csv << 'EOF'
Invoice,Date,Supplier,Amount
FAT-001,2026-05-01,ENEL,1500.00
FAT-002,2026-05-02,COPASA,800.00
FAT-003,2026-05-03,TELECOM,350.00
EOF

# 2. Run test script
bash TEST_UPLOAD_API.sh

# Expected output: Success with file ID
```

### Work Locally (Development)
```bash
# Navigate to project
cd "C:\Users\Rosane Souza\OneDrive - ORION REFEIÇÕES EMPRESARIAIS LTDA\Drive - Rosane\Diretoria Administrativa\Financeiro\Resultado Financeiro\2026\BD_2026\AlufranFinConsole"

# Run locally on localhost:5000
dotnet run --project AlufranFinConsole.Api

# Test locally
curl http://localhost:5000/api/auth/login -X POST -H "Content-Type: application/json" -d '{"email":"admin@alufran.local","password":"AlufranAdmin@2026"}'
```

---

## 📚 Documentation Files

| File | Purpose | Read When |
|------|---------|-----------|
| UPLOAD_API.md | API reference | Need endpoint details |
| PHASE3_COMPLETION_LOG.md | Phase 3 summary | Understanding what was built |
| PHASE4_PLAN.md | Phase 4 roadmap | Planning next phase |
| DEPLOYMENT_STATUS.md | Build status | Troubleshooting deployment |
| TEST_UPLOAD_API.sh | Automated tests | Testing the API |

---

## 🧪 Test Scenarios

### Scenario 1: Happy Path (Successful Upload)
```bash
bash TEST_UPLOAD_API.sh
# Expected: ✅ All tests pass, file gets ID
```

### Scenario 2: Duplicate File Detection
```bash
# Run same file twice
bash TEST_UPLOAD_API.sh
bash TEST_UPLOAD_API.sh
# Expected 2nd run: ❌ "File already uploaded"
```

### Scenario 3: Invalid File Type
```bash
curl -X POST https://alufranfinconsole.onrender.com/api/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@test.csv" \
  -F "fileType=INVALID" \
  -F "competence=2026-05"
# Expected: 400 Bad Request - "Invalid fileType"
```

### Scenario 4: Invalid Competence Format
```bash
curl -X POST https://alufranfinconsole.onrender.com/api/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@test.csv" \
  -F "fileType=FAT" \
  -F "competence=2026-5"  # Missing leading 0
# Expected: 400 Bad Request - "competence must be in YYYY-MM format"
```

### Scenario 5: File Too Large
```bash
dd if=/dev/zero of=large.bin bs=1M count=60  # 60 MB file
curl -X POST https://alufranfinconsole.onrender.com/api/upload \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@large.bin" \
  -F "fileType=FAT" \
  -F "competence=2026-05"
# Expected: 400 Bad Request - "File exceeds maximum size"
```

---

## 🛠️ Development Workflow

### After Code Change
```bash
# 1. Make changes to .cs files
# 2. Run locally
dotnet run --project AlufranFinConsole.Api

# 3. Test locally
curl http://localhost:5000/...

# 4. If good, commit and push
git add .
git commit -m "Your message"
git push origin master

# 5. Render automatically redeploys
# Check status at: https://dashboard.render.com
```

### Compilation Check
```bash
# Check if code compiles
dotnet build

# Expected: 0 errors, 0 warnings
```

### Database Check (SQLite)
```bash
# Install EF Core tools if needed
dotnet tool install -g dotnet-ef

# Check migrations
dotnet ef migrations list

# Apply pending migrations
dotnet ef database update
```

---

## 🔐 Authentication

### Get Admin Token
```bash
curl -X POST https://alufranfinconsole.onrender.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@alufran.local",
    "password": "AlufranAdmin@2026"
  }' | jq -r '.token'

# Save to variable
TOKEN=$(curl -s ... | jq -r '.token')

# Use in requests
curl -H "Authorization: Bearer $TOKEN" ...
```

### Decode JWT Token (Optional)
```bash
# Install jq if needed
# Then use: https://jwt.io or manually:

TOKEN="eyJhbGc..."
# Copy to https://jwt.io to decode header.payload.signature
```

---

## 📊 File Types Supported

| Type | Meaning | Example |
|------|---------|---------|
| PAG | Pagamentos | supplier_payments_2026-05.csv |
| REC | Recebimentos | customer_receipts_2026-05.csv |
| FAT | Faturas | invoices_2026-05.csv |
| EMITIDAS | Notas Emitidas | issued_notes_2026-05.csv |
| COMP | Compras | purchases_2026-05.csv |
| TRANSF | Transferências | transfers_2026-05.csv |
| FOPAG | Formas de Pagamento | payment_methods_2026-05.csv |

---

## 🚨 Common Issues

### Issue: API Returns 503 Service Unavailable
**Cause:** Still deploying on Render free tier  
**Fix:** Wait 2-5 minutes and try again

### Issue: Authentication Failed
**Cause:** Wrong email/password or token expired  
**Fix:** Get fresh token: `curl ... /api/auth/login`

### Issue: File Upload Returns 400 Bad Request
**Cause:** Invalid fileType, competence format, or file too large  
**Fix:** Check error message and validate inputs

### Issue: Localhost API Connection Refused
**Cause:** Server not running  
**Fix:** Run `dotnet run --project AlufranFinConsole.Api`

### Issue: Database Locked
**Cause:** Concurrent access or previous process still running  
**Fix:** Restart service in Render dashboard or kill local process

---

## 📈 Monitoring

### Check Build Status
```bash
# Via Render dashboard
open https://dashboard.render.com

# Via Git (last commit)
git log --oneline -1
# Should show: Fix: Add missing EntityFrameworkCore...
```

### Check API Logs (Once Live)
```bash
# Render provides live logs in dashboard
# Look for:
# ✅ "File uploaded successfully"
# ✅ "User {id} uploading file"
# ❌ "Error uploading file"
```

### Check Database Records
```bash
# Via SQLite (local dev)
sqlite3 alufran_fin_close.db
> SELECT COUNT(*) FROM import_files;

# Via Render app (production)
# Use admin dashboard (Phase 5+) or logs
```

---

## 🚀 Phase Checklist

### Phase 3: Upload & Versioning ✅
- [x] Code implemented and tested locally
- [x] Pushed to GitHub
- [x] Render deployment triggered
- [ ] API responds to requests (waiting for deployment)
- [ ] All endpoints tested
- [ ] File storage working
- [ ] Database records created

### Phase 4: Staging & Saneamento (Ready to Start)
- [ ] StagingData entity created
- [ ] DataValidationService implemented
- [ ] DataCleansingService implemented
- [ ] StagingController endpoints created
- [ ] QA Report generation working
- [ ] Integration tests passing

---

## 💡 Tips

**Tip 1: Save Token to File**
```bash
curl ... | jq -r '.token' > token.txt
TOKEN=$(cat token.txt)
curl -H "Authorization: Bearer $TOKEN" ...
```

**Tip 2: Create Test Data Easily**
```bash
# Generate random invoice numbers
seq 1 100 | while read i; do
  echo "FAT-$i,2026-05-01,SUPPLIER_$i,$((RANDOM))00"
done > test_invoices.csv
```

**Tip 3: Pretty Print JSON Responses**
```bash
curl ... | jq '.'                    # Full tree
curl ... | jq '.files[0]'             # First file
curl ... | jq '.[] | select(.status=="PENDING")' # Filter
```

**Tip 4: Log All Requests**
```bash
curl -v -X POST ...  # Shows full request/response headers
```

---

## 🔗 Important Links

- **API Base:** https://alufranfinconsole.onrender.com
- **Render Dashboard:** https://dashboard.render.com
- **GitHub Repo:** https://github.com/rosanesouzzza/AlufranFinConsole
- **Documentation:** See .md files in project root

---

## 🎓 Learning Resources

- [ASP.NET Core API Tutorial](https://learn.microsoft.com/en-us/aspnet/core)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [JWT Authentication](https://jwt.io/introduction)
- [Render Deployment](https://render.com/docs)

---

*Last Updated: 2026-05-08 | Status: PHASE 3 DEPLOYED | Next: PHASE 4 READY*
