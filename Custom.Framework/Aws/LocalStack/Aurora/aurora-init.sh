#!/bin/bash

echo "================================================"
echo "???  Initializing Aurora PostgreSQL Database"
echo "================================================"

# Wait for PostgreSQL to be ready
echo "? Waiting for PostgreSQL to be ready..."
until docker exec aurora-postgres-local pg_isready -U admin -d auroradb -q; do
  echo "  Waiting for database..."
  sleep 2
done

echo "  ? PostgreSQL is ready"

echo ""
echo "?? Database Information:"
echo "??????????????????????????????????????????????"

# Get PostgreSQL version
echo ""
echo "PostgreSQL Version:"
docker exec aurora-postgres-local psql -U admin -d auroradb -t -c "SELECT version();"

echo ""
echo "Database: auroradb"
echo "Schema: app"

# List tables
echo ""
echo "Tables in 'app' schema:"
docker exec aurora-postgres-local psql -U admin -d auroradb -c "\dt app.*"

# Show table counts
echo ""
echo "Table Row Counts:"
echo "??????????????????????????????????????????????"

for table in customers products orders order_items; do
    count=$(docker exec aurora-postgres-local psql -U admin -d auroradb -t -c "SELECT COUNT(*) FROM app.$table;")
    echo "  app.$table: $count rows"
done

# Show sample data
echo ""
echo "Sample Customers:"
docker exec aurora-postgres-local psql -U admin -d auroradb -c "SELECT id, email, first_name, last_name, balance FROM app.customers LIMIT 3;"

echo ""
echo "Sample Products:"
docker exec aurora-postgres-local psql -U admin -d auroradb -c "SELECT id, name, sku, price, quantity FROM app.products LIMIT 5;"

echo ""
echo "================================================"
echo "? Aurora PostgreSQL Setup Complete!"
echo "================================================"
echo ""
echo "?? Quick Start Commands:"
echo "??????????????????????????????????????????????"
echo ""
echo "# Connect to database:"
echo "  docker exec -it aurora-postgres-local psql -U admin -d auroradb"
echo ""
echo "# Run query:"
echo "  docker exec -it aurora-postgres-local psql -U admin -d auroradb \\"
echo "    -c \"SELECT * FROM app.customers;\""
echo ""
echo "# Connection string for .NET:"
echo "  Host=localhost;Port=5432;Database=auroradb;Username=admin;Password=localpassword"
echo ""
echo "# List tables:"
echo "  docker exec -it aurora-postgres-local psql -U admin -d auroradb -c \"\\dt app.*\""
echo ""
echo "?? Run .NET Tests:"
echo "  dotnet test --filter 'FullyQualifiedName~AuroraDBTests'"
echo ""
echo "================================================"
