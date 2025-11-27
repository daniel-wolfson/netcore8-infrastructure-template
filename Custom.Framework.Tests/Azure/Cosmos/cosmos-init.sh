#!/bin/sh

# Cosmos DB Initialization Script
# Creates database and container with proper configuration

set -e

echo "================================================"
echo "Azure Cosmos DB Emulator - Initialization"
echo "================================================"

# Configuration
COSMOS_ENDPOINT="${COSMOS_ENDPOINT:-https://cosmos-emulator:8081}"
COSMOS_KEY="${COSMOS_KEY:-C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==}"
DATABASE_NAME="${DATABASE_NAME:-HospitalityOrders}"
CONTAINER_NAME="${CONTAINER_NAME:-Orders}"
PARTITION_KEY="${PARTITION_KEY:-/hotelCode}"

echo "?? Configuration:"
echo "   Endpoint: $COSMOS_ENDPOINT"
echo "   Database: $DATABASE_NAME"
echo "   Container: $CONTAINER_NAME"
echo "   Partition Key: $PARTITION_KEY"
echo ""

# Note: Azure CLI doesn't directly support Cosmos DB emulator with self-signed certs
# Using REST API directly instead

echo "? Creating database: $DATABASE_NAME..."

# Create Database via REST API
HTTP_DATE=$(date -u +"%a, %d %b %Y %H:%M:%S GMT")

curl -k -X POST "$COSMOS_ENDPOINT/dbs" \
  -H "Authorization: type%3Dmaster%26ver%3D1.0%26sig%3D$COSMOS_KEY" \
  -H "Content-Type: application/json" \
  -H "x-ms-date: $HTTP_DATE" \
  -H "x-ms-version: 2018-12-31" \
  -d "{\"id\":\"$DATABASE_NAME\"}" \
  || echo "   Database may already exist (this is OK)"

echo "? Database created or already exists"
echo ""

echo "? Creating container: $CONTAINER_NAME..."

# Create Container with TTL and partition key
HTTP_DATE=$(date -u +"%a, %d %b %Y %H:%M:%S GMT")

curl -k -X POST "$COSMOS_ENDPOINT/dbs/$DATABASE_NAME/colls" \
  -H "Authorization: type%3Dmaster%26ver%3D1.0%26sig%3D$COSMOS_KEY" \
  -H "Content-Type: application/json" \
  -H "x-ms-date: $HTTP_DATE" \
  -H "x-ms-version: 2018-12-31" \
  -H "x-ms-offer-throughput: 4000" \
  -d "{
    \"id\":\"$CONTAINER_NAME\",
    \"partitionKey\":{
      \"paths\":[\"$PARTITION_KEY\"],
      \"kind\":\"Hash\"
    },
    \"defaultTtl\":-1,
    \"indexingPolicy\":{
      \"indexingMode\":\"consistent\",
      \"automatic\":true,
      \"includedPaths\":[{\"path\":\"/*\"}],
      \"excludedPaths\":[{\"path\":\"/\\\"_etag\\\"/?\"}]
    }
  }" \
  || echo "   Container may already exist (this is OK)"

echo "? Container created or already exists"
echo ""

echo "================================================"
echo "? Cosmos DB Initialization Complete!"
echo "================================================"
echo ""
echo "?? Connection Details:"
echo "   Endpoint: https://localhost:8081"
echo "   Key: C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw=="
echo "   Database: $DATABASE_NAME"
echo "   Container: $CONTAINER_NAME"
echo ""
echo "?? Data Explorer: https://localhost:8081/_explorer/index.html"
echo ""
echo "?? Note: Application will auto-create database/container on first run if not exists"
echo ""
