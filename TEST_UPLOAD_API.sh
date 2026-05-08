#!/bin/bash

# Test script for Alufran Upload API (Phase 3)
# Run this once the Render deployment is ready

API_BASE="https://alufranfinconsole.onrender.com"
ADMIN_EMAIL="admin@alufran.local"
ADMIN_PASSWORD="AlufranAdmin@2026"

echo "🔐 Step 1: Authenticate and get JWT token..."
TOKEN=$(curl -s -X POST "$API_BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" | jq -r '.token')

if [ -z "$TOKEN" ] || [ "$TOKEN" == "null" ]; then
  echo "❌ Authentication failed. Exiting."
  exit 1
fi

echo "✅ Token obtained: ${TOKEN:0:20}..."
echo ""

# Create a test CSV file
TEST_FILE="test_invoice_$(date +%s).csv"
cat > "$TEST_FILE" << 'EOF'
Invoice,Date,Supplier,Amount
FAT-001,2026-05-01,ENEL,1500.00
FAT-002,2026-05-02,COPASA,800.00
FAT-003,2026-05-03,TELECOM,350.00
EOF

echo "📄 Step 2: Upload test file..."
echo "File: $TEST_FILE (Type: FAT, Competence: 2026-05)"
echo ""

RESPONSE=$(curl -s -X POST "$API_BASE/api/upload" \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@$TEST_FILE" \
  -F "fileType=FAT" \
  -F "competence=2026-05")

echo "📋 Response:"
echo "$RESPONSE" | jq '.'
echo ""

# Extract file ID from response
FILE_ID=$(echo "$RESPONSE" | jq -r '.id // empty')

if [ ! -z "$FILE_ID" ]; then
  echo "✅ Upload successful! File ID: $FILE_ID"
  echo ""

  echo "📊 Step 3: Get file details..."
  curl -s -X GET "$API_BASE/api/upload/$FILE_ID" \
    -H "Authorization: Bearer $TOKEN" | jq '.'
  echo ""

  echo "📈 Step 4: List all uploaded files..."
  curl -s -X GET "$API_BASE/api/upload?fileType=FAT&competence=2026-05&limit=10" \
    -H "Authorization: Bearer $TOKEN" | jq '.'
  echo ""

  echo "📊 Step 5: Get upload statistics..."
  curl -s -X GET "$API_BASE/api/upload/stats?competence=2026-05" \
    -H "Authorization: Bearer $TOKEN" | jq '.'
else
  echo "❌ Upload failed. No file ID returned."
fi

# Cleanup
rm -f "$TEST_FILE"
echo ""
echo "✅ Test complete."
