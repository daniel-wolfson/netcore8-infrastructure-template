# ================================================
# Aurora PostgreSQL Learning Script
# ================================================

param(
    [switch]$Start,
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Status,
    [switch]$Test,
    [switch]$Clean
)

$postgresHost = "localhost"
$postgresPort = 5432
$postgresDb = "auroradb"
$postgresUser = "admin"
$postgresPass = "localpassword"

function Write-Header {
    param([string]$text)
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host " $text" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-DockerRunning {
    try {
        docker info | Out-Null
        return $true
    } catch {
        Write-Host "[ERROR] Docker is not running!" -ForegroundColor Red
        Write-Host "Please start Docker Desktop first." -ForegroundColor Yellow
        return $false
    }
}

function Test-PostgresHealthy {
    try {
        $result = docker exec aurora-postgres-local pg_isready -U $postgresUser -d $postgresDb 2>&1
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Start-AuroraService {
    Write-Header "Starting Aurora PostgreSQL"
    
    if (-not (Test-DockerRunning)) {
        return
    }

    Write-Host "[INFO] Starting Aurora PostgreSQL container..." -ForegroundColor Green
    
    # Navigate to parent directory where docker-compose.yaml is located
    Push-Location (Join-Path $PSScriptRoot "..")
    
    docker-compose up -d aurora-postgres

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[INFO] Waiting for PostgreSQL to be ready..." -ForegroundColor Yellow
        Start-Sleep -Seconds 8

        $attempts = 0
        $maxAttempts = 30
        
        Write-Host "[INFO] Checking PostgreSQL health..." -NoNewline
        while (-not (Test-PostgresHealthy) -and $attempts -lt $maxAttempts) {
            Write-Host "." -NoNewline
            Start-Sleep -Seconds 1
            $attempts++
        }
        Write-Host ""

        if (Test-PostgresHealthy) {
            Write-Host "[SUCCESS] Aurora PostgreSQL is running!" -ForegroundColor Green
            Show-DatabaseInfo
            Show-AuroraQuickStart
        } else {
            Write-Host "[WARNING] PostgreSQL health check failed" -ForegroundColor Yellow
            Write-Host "View logs: docker logs aurora-postgres-local -f" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[ERROR] Failed to start PostgreSQL" -ForegroundColor Red
    }
    
    Pop-Location
}

function Stop-AuroraService {
    Write-Header "Stopping Aurora PostgreSQL"
    
    Push-Location (Join-Path $PSScriptRoot "..")
    
    docker-compose stop aurora-postgres
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[SUCCESS] Aurora PostgreSQL stopped" -ForegroundColor Green
    }
    
    Pop-Location
}

function Restart-AuroraService {
    Write-Header "Restarting Aurora PostgreSQL"
    
    Stop-AuroraService
    Start-Sleep -Seconds 2
    Start-AuroraService
}

function Show-Status {
    Write-Header "Aurora PostgreSQL Status"
    
    Push-Location (Join-Path $PSScriptRoot "..")
    
    Write-Host "Docker Container:" -ForegroundColor Cyan
    docker-compose ps aurora-postgres
    
    Pop-Location
    
    Write-Host ""
    Write-Host "PostgreSQL Status:" -ForegroundColor Cyan
    if (Test-PostgresHealthy) {
        Write-Host "  [STATUS] PostgreSQL is HEALTHY ?" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "  Connection Details:" -ForegroundColor Yellow
        Write-Host "    Host: $postgresHost" -ForegroundColor Gray
        Write-Host "    Port: $postgresPort" -ForegroundColor Gray
        Write-Host "    Database: $postgresDb" -ForegroundColor Gray
        Write-Host "    Username: $postgresUser" -ForegroundColor Gray
        Write-Host "    Password: $postgresPass" -ForegroundColor Gray
        
        # Get database info
        Write-Host ""
        Write-Host "  Database Info:" -ForegroundColor Yellow
        $dbInfo = docker exec aurora-postgres-local psql -U $postgresUser -d $postgresDb -t -c "SELECT version();" 2>$null
        if ($dbInfo) {
            Write-Host "    $($dbInfo.Trim())" -ForegroundColor Gray
        }

        # Get table count
        $tableCount = docker exec aurora-postgres-local psql -U $postgresUser -d $postgresDb -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'app';" 2>$null
        if ($tableCount) {
            Write-Host "    Tables in 'app' schema: $($tableCount.Trim())" -ForegroundColor Gray
        }
        
        # Show table row counts
        Write-Host ""
        Write-Host "  Table Row Counts:" -ForegroundColor Yellow
        Show-TableCounts
    } else {
        Write-Host "  [STATUS] PostgreSQL is NOT RUNNING ?" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Start Aurora with: .\aurora-learning.ps1 -Start" -ForegroundColor Yellow
    }
}

function Show-DatabaseInfo {
    Write-Host ""
    Write-Host "?? Database Information:" -ForegroundColor Cyan
    Write-Host "??????????????????????????????????????????????" -ForegroundColor Gray
    
    # Show tables
    Write-Host ""
    Write-Host "Tables in 'app' schema:" -ForegroundColor Yellow
    $tables = docker exec aurora-postgres-local psql -U $postgresUser -d $postgresDb -t -c "\dt app.*" 2>$null
    if ($tables) {
        Write-Host $tables -ForegroundColor Gray
    }
    
    # Show row counts
    Write-Host ""
    Write-Host "Sample Data:" -ForegroundColor Yellow
    Show-TableCounts
}

function Show-TableCounts {
    $tables = @("customers", "products", "orders", "order_items")
    
    foreach ($table in $tables) {
        $count = docker exec aurora-postgres-local psql -U $postgresUser -d $postgresDb -t -c "SELECT COUNT(*) FROM app.$table;" 2>$null
        if ($count) {
            Write-Host "    app.$table`: $($count.Trim()) rows" -ForegroundColor Gray
        }
    }
}

function Test-AuroraOperations {
    Write-Header "Running Aurora PostgreSQL Tests"
    
    if (-not (Test-PostgresHealthy)) {
        Write-Host "[ERROR] Aurora PostgreSQL is not running!" -ForegroundColor Red
        Write-Host "Start Aurora with: .\aurora-learning.ps1 -Start" -ForegroundColor Yellow
        return
    }

    Write-Host "[INFO] Running AuroraDB integration tests..." -ForegroundColor Cyan
    Write-Host ""
    
    # Navigate to solution root (3 levels up from Aurora folder)
    Push-Location (Join-Path $PSScriptRoot "..\..\..") 
    
    dotnet test --filter "FullyQualifiedName~AuroraDBTests" --verbosity minimal
    
    Pop-Location
    
    Write-Host ""
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[SUCCESS] AuroraDB tests passed! ?" -ForegroundColor Green
    } else {
        Write-Host "[FAILURE] Some AuroraDB tests failed ?" -ForegroundColor Red
    }
}

function Clean-AuroraEnvironment {
    Write-Header "Cleaning Aurora Environment"
    
    Push-Location (Join-Path $PSScriptRoot "..")
    
    Write-Host "[INFO] Stopping PostgreSQL container..." -ForegroundColor Yellow
    docker-compose stop aurora-postgres
    
    Write-Host "[INFO] Removing PostgreSQL container..." -ForegroundColor Yellow
    docker-compose rm -f aurora-postgres
    
    Write-Host "[INFO] Removing PostgreSQL volumes..." -ForegroundColor Yellow
    docker volume rm localstack_aurora-postgres-data -f 2>$null
    
    Pop-Location
    
    Write-Host "[SUCCESS] Aurora environment cleaned!" -ForegroundColor Green
    Write-Host "[INFO] Database will be recreated on next start" -ForegroundColor Yellow
}

function Show-AuroraQuickStart {
    Write-Host ""
    Write-Host "?? Aurora Quick Start Commands:" -ForegroundColor Cyan
    Write-Host "??????????????????????????????????????????????" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Check status:" -ForegroundColor White
    Write-Host "  .\aurora-learning.ps1 -Status" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Run Aurora tests:" -ForegroundColor White
    Write-Host "  .\aurora-learning.ps1 -Test" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Connect to database:" -ForegroundColor White
    Write-Host "  docker exec -it aurora-postgres-local psql -U $postgresUser -d $postgresDb" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Run a query:" -ForegroundColor White
    Write-Host "  docker exec -it aurora-postgres-local psql -U $postgresUser -d $postgresDb \\" -ForegroundColor Gray
    Write-Host "    -c `"SELECT * FROM app.customers;`"" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# List tables:" -ForegroundColor White
    Write-Host "  docker exec -it aurora-postgres-local psql -U $postgresUser -d $postgresDb \\" -ForegroundColor Gray
    Write-Host "    -c `"\dt app.*`"" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# View logs:" -ForegroundColor White
    Write-Host "  docker logs aurora-postgres-local -f" -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? Connection Details:" -ForegroundColor Cyan
    Write-Host "  Host: $postgresHost" -ForegroundColor Gray
    Write-Host "  Port: $postgresPort" -ForegroundColor Gray
    Write-Host "  Database: $postgresDb" -ForegroundColor Gray
    Write-Host "  Username: $postgresUser" -ForegroundColor Gray
    Write-Host "  Password: $postgresPass" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Connection String:" -ForegroundColor Gray
    Write-Host "  Host=$postgresHost;Port=$postgresPort;Database=$postgresDb;Username=$postgresUser;Password=$postgresPass" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "?? Documentation:" -ForegroundColor Cyan
    Write-Host "  Complete Guide: AURORA_README.md" -ForegroundColor Gray
    Write-Host "  Quick Reference: AURORA_QUICK_REFERENCE.md" -ForegroundColor Gray
    Write-Host ""
}

function Show-Help {
    Write-Header "Aurora PostgreSQL Learning Script"
    
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  .\aurora-learning.ps1 [Options]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Cyan
    Write-Host "  -Start        Start Aurora PostgreSQL container" -ForegroundColor White
    Write-Host "  -Stop         Stop PostgreSQL container" -ForegroundColor White
    Write-Host "  -Restart      Restart PostgreSQL container" -ForegroundColor White
    Write-Host "  -Status       Show Aurora service status" -ForegroundColor White
    Write-Host "  -Test         Run AuroraDB integration tests" -ForegroundColor White
    Write-Host "  -Clean        Clean environment (remove volumes)" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host "  .\aurora-learning.ps1 -Start" -ForegroundColor Gray
    Write-Host "  .\aurora-learning.ps1 -Status" -ForegroundColor Gray
    Write-Host "  .\aurora-learning.ps1 -Test" -ForegroundColor Gray
    Write-Host "  .\aurora-learning.ps1 -Clean" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Documentation:" -ForegroundColor Cyan
    Write-Host "  AURORA_README.md - Complete guide" -ForegroundColor Gray
    Write-Host "  AURORA_QUICK_REFERENCE.md - SQL reference" -ForegroundColor Gray
    Write-Host ""
}

# Main execution
Push-Location $PSScriptRoot

try {
    if ($Start) {
        Start-AuroraService
    }
    elseif ($Stop) {
        Stop-AuroraService
    }
    elseif ($Restart) {
        Restart-AuroraService
    }
    elseif ($Status) {
        Show-Status
    }
    elseif ($Test) {
        Test-AuroraOperations
    }
    elseif ($Clean) {
        Clean-AuroraEnvironment
    }
    else {
        Show-Help
    }
}
finally {
    Pop-Location
}
