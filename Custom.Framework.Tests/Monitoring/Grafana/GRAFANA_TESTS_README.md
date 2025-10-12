# Grafana Integration Tests

Comprehensive integration tests for the Grafana client implementation in Custom.Framework.

## Overview

The `GrafanaTests.cs` file contains **27 integration tests** that verify all aspects of the Grafana integration:

- ✅ **Service Registration** - DI container setup
- ✅ **Health Checks** - Grafana availability verification
- ✅ **Data Source Management** - Create, read, update, delete data sources
- ✅ **Dashboard Management** - Full CRUD operations on dashboards
- ✅ **Annotation Management** - Event marking and retrieval
- ✅ **Real-World Scenarios** - Deployment tracking, monitoring dashboards
- ✅ **Error Handling** - Graceful handling of failures

## Prerequisites

### 1. Grafana Instance

The tests require a running Grafana instance. You can use Docker:

```bash
# Start Grafana with default credentials
docker run -d \
  --name grafana-test \
  -p 3001:3000 \
  -e "GF_SECURITY_ADMIN_PASSWORD=admin" \
  grafana/grafana:latest
```

**Default Credentials:**
- URL: http://localhost:3001
- Username: `admin`
- Password: `admin`

### 2. Environment Variables (Optional)

Override default test configuration with environment variables:

```bash
# Windows (PowerShell)
$env:GRAFANA_URL="http://localhost:3001"
$env:GRAFANA_USERNAME="admin"
$env:GRAFANA_PASSWORD="admin"

# Linux/macOS
export GRAFANA_URL="http://localhost:3001"
export GRAFANA_USERNAME="admin"
export GRAFANA_PASSWORD="admin"
```

## Running the Tests

### Run All Grafana Tests

```bash
# From solution root
dotnet test --filter "FullyQualifiedName~GrafanaTests"

# With detailed output
dotnet test --filter "FullyQualifiedName~GrafanaTests" --logger "console;verbosity=detailed"
```

### Run Specific Test

```bash
# Test dashboard creation
dotnet test --filter "FullyQualifiedName~GrafanaTests.Dashboard_Create_Should_Work"

# Test annotation service
dotnet test --filter "FullyQualifiedName~GrafanaTests.AnnotationService_MarkDeployment_Should_Work"

# Test data source management
dotnet test --filter "FullyQualifiedName~GrafanaTests.DataSource_Create_Should_Work"
```

### Run in Visual Studio

1. Open **Test Explorer** (Test → Test Explorer)
2. Expand **Custom.Framework.Tests → GrafanaTests**
3. Right-click and select **Run**

## Test Categories

### 1. Service Registration Tests (2 tests)

Tests DI container registration:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.GrafanaClient_Should_Be_Registered"
```

✅ Verifies `IGrafanaClient` is registered  
✅ Verifies `IGrafanaAnnotationService` is registered

### 2. Health Check Tests (1 test)

Tests Grafana connectivity:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.HealthCheck_Should_Return_Status"
```

✅ Verifies `/api/health` endpoint responds

### 3. Data Source Tests (5 tests)

Tests data source management:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.DataSource"
```

✅ Create Prometheus data source  
✅ Get data source by UID  
✅ Get data source by name  
✅ List all data sources  
✅ Delete data source

### 4. Dashboard Tests (6 tests)

Tests dashboard operations:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.Dashboard"
```

✅ Create dashboard  
✅ Get dashboard by UID  
✅ Update dashboard  
✅ Delete dashboard  
✅ Create dashboard with panels  
✅ Create dashboard with Prometheus queries

### 5. Annotation Tests (6 tests)

Tests annotation/event marking:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.Annotation"
```

✅ Create annotation  
✅ Get annotations by time range  
✅ Delete annotation  
✅ Mark deployment via service  
✅ Mark error via service  
✅ Mark custom event via service

### 6. Real-World Scenario Tests (2 tests)

Tests complete workflows:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.RealWorld"
```

✅ Deployment tracking workflow  
✅ Create monitoring dashboard with multiple panels

### 7. Error Handling Tests (2 tests)

Tests resilience:

```bash
dotnet test --filter "FullyQualifiedName~GrafanaTests.Should_Handle"
```

✅ Handle non-existent resources gracefully  
✅ Handle invalid Grafana URL

## Test Behavior

### When Grafana is Available

All tests run normally and interact with the real Grafana API:

```
✓ Grafana is available and healthy
✓ Data source created successfully
  Name: test_prometheus_abc123
  UID: ds-abc123
  ID: 1
```

### When Grafana is NOT Available

Tests gracefully skip with informative messages:

```
⚠ Grafana is not available. Some tests will be skipped.
  Make sure Grafana is running at: http://localhost:3001
  Run: docker run -d -p 3001:3000 -e "GF_SECURITY_ADMIN_PASSWORD=admin" grafana/grafana:latest

⚠ Test skipped - Grafana not available
```

**Service registration tests still run** (they don't need Grafana).

## Example Test Output

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27, Duration: 5 s

GrafanaTests Output:
  ✓ Grafana is available and healthy
  ✓ IGrafanaClient successfully registered in DI
  ✓ IGrafanaAnnotationService successfully registered in DI
  ✓ Data source created successfully
    Name: test_prometheus_9a8b7c6d5e4f3a2b1
    UID: ds-abc123
    ID: 1
  ✓ Dashboard created successfully
    Title: Test Dashboard 1a2b3c4d5e6f7g8h
    UID: db-xyz789
    URL: /d/db-xyz789/test-dashboard-1a2b3c4d5e6f7g8h
  ✓ Deployment annotation created via service
    Version: 1.2.3
```

## Continuous Integration

### GitHub Actions Example

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    services:
      grafana:
        image: grafana/grafana:latest
        ports:
          - 3001:3000
        env:
          GF_SECURITY_ADMIN_PASSWORD: admin
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Wait for Grafana
        run: |
          timeout 60 bash -c 'until curl -f http://localhost:3001/api/health; do sleep 2; done'
      
      - name: Run Grafana Tests
        run: dotnet test --filter "FullyQualifiedName~GrafanaTests"
        env:
          GRAFANA_URL: http://localhost:3001
          GRAFANA_USERNAME: admin
          GRAFANA_PASSWORD: admin
```

### Azure DevOps Example

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

services:
  grafana:
    image: grafana/grafana:latest
    ports:
      - 3001:3000
    env:
      GF_SECURITY_ADMIN_PASSWORD: admin

steps:
  - task: UseDotNet@2
    inputs:
      version: '8.0.x'
  
  - script: |
      until curl -f http://localhost:3001/api/health; do sleep 2; done
    displayName: 'Wait for Grafana'
  
  - script: dotnet test --filter "FullyQualifiedName~GrafanaTests"
    displayName: 'Run Grafana Integration Tests'
    env:
      GRAFANA_URL: http://localhost:3001
      GRAFANA_USERNAME: admin
      GRAFANA_PASSWORD: admin
```

## Docker Compose for Local Testing

Create `docker-compose.test.yml`:

```yaml
version: '3.8'

services:
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3001:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_USERS_ALLOW_SIGN_UP=false
    volumes:
      - grafana-test-data:/var/lib/grafana
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:3001/api/health"]
      interval: 5s
      timeout: 3s
      retries: 10

volumes:
  grafana-test-data:
```

**Usage:**

```bash
# Start Grafana
docker-compose -f docker-compose.test.yml up -d

# Wait for healthy
docker-compose -f docker-compose.test.yml ps

# Run tests
dotnet test --filter "FullyQualifiedName~GrafanaTests"

# Stop and cleanup
docker-compose -f docker-compose.test.yml down -v
```

## Troubleshooting

### Issue: All tests skipped

**Cause:** Grafana is not running

**Solution:**
```bash
# Check if Grafana is running
curl http://localhost:3001/api/health

# Start Grafana
docker run -d -p 3001:3000 -e "GF_SECURITY_ADMIN_PASSWORD=admin" grafana/grafana:latest
```

### Issue: Authentication failures

**Cause:** Incorrect credentials

**Solution:**
```bash
# Test credentials manually
curl -u admin:admin http://localhost:3001/api/datasources

# Or use environment variables
export GRAFANA_USERNAME=your_username
export GRAFANA_PASSWORD=your_password
```

### Issue: Tests fail with "connection refused"

**Cause:** Grafana not fully started or wrong URL

**Solution:**
```bash
# Wait for Grafana to be ready
timeout 60 bash -c 'until curl -f http://localhost:3001/api/health; do sleep 2; done'

# Check Grafana logs
docker logs grafana-test
```

### Issue: Port 3000 already in use

**Solution:**
```bash
# Use different port
docker run -d -p 3001:3000 -e "GF_SECURITY_ADMIN_PASSWORD=admin" grafana/grafana:latest

# Update environment variable
export GRAFANA_URL=http://localhost:3001
```

## Test Cleanup

The tests automatically clean up created resources:

- ✅ **Dashboards** - Deleted after test completes
- ✅ **Data Sources** - Deleted after test completes
- ✅ **Annotations** - Deleted after test completes (where applicable)

**Manual cleanup** (if tests crash):

```bash
# Stop and remove Grafana container
docker stop grafana-test
docker rm grafana-test

# Remove volume (clears all data)
docker volume rm grafana-test-data
```

## Best Practices

1. **Run with fresh Grafana** - Use ephemeral Docker containers
2. **Don't use production Grafana** - Tests create/delete resources
3. **Check test output** - Tests provide helpful diagnostic messages
4. **Use environment variables** - Override defaults for CI/CD
5. **Verify cleanup** - Check Grafana UI after tests if debugging

## Related Documentation

- [Grafana Integration README](../Custom.Framework/Monitoring/Grafana/README.md)
- [Grafana Quick Start Guide](../Custom.Framework/Monitoring/Grafana/QUICK_START.md)
- [Grafana HTTP API Docs](https://grafana.com/docs/grafana/latest/http_api/)

## Summary

✅ **27 comprehensive integration tests**  
✅ **Covers all Grafana client functionality**  
✅ **Graceful handling of unavailable Grafana**  
✅ **CI/CD ready**  
✅ **Automatic resource cleanup**

Run the tests to ensure your Grafana integration is working correctly! 🚀
