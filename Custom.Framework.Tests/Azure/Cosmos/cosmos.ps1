# Cosmos DB Emulator - PowerShell Management Script
# Provides commands to manage Cosmos DB Emulator

param(
    [Parameter(Position=0)]
    [ValidateSet('start', 'stop', 'restart', 'init', 'clean', 'logs', 'status', 'help')]
    [string]$Command = 'help'
)

$ComposeFile = "docker-compose.cosmos.yml"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Change to script directory
Push-Location $ScriptDir

function Write-Header {
    param([string]$Title)
    Write-Host ""
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host " $Title" -ForegroundColor Cyan
    Write-Host "================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-DockerRunning {
    try {
        docker info | Out-Null
        return $true
    }
    catch {
        Write-Host "? ERROR: Docker is not running!" -ForegroundColor Red
        Write-Host "   Please start Docker Desktop and try again." -ForegroundColor Yellow
        return $false
    }
}

function Start-CosmosEmulator {
    Write-Header "Azure Cosmos DB Emulator - Start"
    
    if (-not (Test-DockerRunning)) { 
        Pop-Location
        exit 1 
    }
    
    Write-Host "?? Starting Cosmos DB Emulator..." -ForegroundColor Green
    Write-Host ""
    
    docker-compose -f $ComposeFile up -d cosmos-emulator
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "? ERROR: Failed to start Cosmos DB Emulator" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Write-Host ""
    Write-Host "? Cosmos DB Emulator is starting..." -ForegroundColor Green
    Write-Host ""
    Write-Host "? Please wait 1-2 minutes for the emulator to fully initialize" -ForegroundColor Yellow
    Write-Host "   The emulator is resource-intensive and takes time to start" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "?? Connection Details:" -ForegroundColor Cyan
    Write-Host "   Endpoint: https://localhost:8081"
    Write-Host "   Key: C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
    Write-Host ""
    Write-Host "?? Data Explorer: https://localhost:8081/_explorer/index.html" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "?? Useful commands:" -ForegroundColor Yellow
    Write-Host "   Initialize DB:  .\cosmos.ps1 init"
    Write-Host "   View logs:      .\cosmos.ps1 logs"
    Write-Host "   Check status:   .\cosmos.ps1 status"
    Write-Host "   Stop:           .\cosmos.ps1 stop"
    Write-Host ""
}

function Stop-CosmosEmulator {
    Write-Header "Azure Cosmos DB Emulator - Stop"
    
    Write-Host "?? Stopping Cosmos DB Emulator..." -ForegroundColor Yellow
    Write-Host ""
    
    docker-compose -f $ComposeFile down
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "? ERROR: Failed to stop Cosmos DB Emulator" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Write-Host ""
    Write-Host "? Cosmos DB Emulator stopped" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? To preserve data, volumes are retained" -ForegroundColor Yellow
    Write-Host "?? To remove all data: .\cosmos.ps1 clean" -ForegroundColor Yellow
    Write-Host ""
}

function Restart-CosmosEmulator {
    Write-Header "Azure Cosmos DB Emulator - Restart"
    
    Write-Host "?? Restarting Cosmos DB Emulator..." -ForegroundColor Yellow
    Write-Host ""
    
    docker-compose -f $ComposeFile restart cosmos-emulator
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "? ERROR: Failed to restart Cosmos DB Emulator" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Write-Host ""
    Write-Host "? Cosmos DB Emulator restarted" -ForegroundColor Green
    Write-Host "? Wait 1-2 minutes for full initialization" -ForegroundColor Yellow
    Write-Host ""
}

function Initialize-CosmosDB {
    Write-Header "Azure Cosmos DB Emulator - Initialize"
    
    # Check if emulator is running
    $running = docker ps --format "{{.Names}}" | Select-String -Pattern "cosmos-emulator"
    
    if (-not $running) {
        Write-Host "? ERROR: Cosmos DB Emulator is not running!" -ForegroundColor Red
        Write-Host "   Please run: .\cosmos.ps1 start" -ForegroundColor Yellow
        Write-Host ""
        Pop-Location
        exit 1
    }
    
    Write-Host "?? Running initialization..." -ForegroundColor Green
    Write-Host ""
    
    docker-compose -f $ComposeFile --profile init up cosmos-init
    
    Write-Host ""
    Write-Host "? Initialization complete" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? Database: HospitalityOrders" -ForegroundColor Cyan
    Write-Host "?? Container: Orders" -ForegroundColor Cyan
    Write-Host "?? Partition Key: /hotelCode" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "?? View in Data Explorer: https://localhost:8081/_explorer/index.html" -ForegroundColor Cyan
    Write-Host ""
}

function Clear-CosmosData {
    Write-Header "Azure Cosmos DB Emulator - Clean/Reset"
    
    Write-Host "??  WARNING: This will DELETE all Cosmos DB data!" -ForegroundColor Red
    Write-Host ""
    
    $confirm = Read-Host "Are you sure you want to continue? (yes/no)"
    
    if ($confirm -ne 'yes') {
        Write-Host ""
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        Write-Host ""
        Pop-Location
        exit 0
    }
    
    Write-Host ""
    Write-Host "?? Stopping Cosmos DB Emulator..." -ForegroundColor Yellow
    docker-compose -f $ComposeFile down
    
    Write-Host ""
    Write-Host "???  Removing volumes and data..." -ForegroundColor Yellow
    docker volume rm cosmos-data 2>$null
    
    Write-Host ""
    Write-Host "? Cosmos DB Emulator cleaned" -ForegroundColor Green
    Write-Host ""
    Write-Host "?? Run '.\cosmos.ps1 start' to start fresh" -ForegroundColor Yellow
    Write-Host ""
}

function Show-Logs {
    Write-Header "Azure Cosmos DB Emulator - Logs"
    
    # Check if emulator is running
    $running = docker ps --format "{{.Names}}" | Select-String -Pattern "cosmos-emulator"
    
    if (-not $running) {
        Write-Host "? ERROR: Cosmos DB Emulator is not running!" -ForegroundColor Red
        Write-Host "   Please run: .\cosmos.ps1 start" -ForegroundColor Yellow
        Write-Host ""
        Pop-Location
        exit 1
    }
    
    Write-Host "?? Showing live logs (Ctrl+C to exit)..." -ForegroundColor Cyan
    Write-Host ""
    
    docker logs -f cosmos-emulator
}

function Show-Status {
    Write-Header "Azure Cosmos DB Emulator - Status"
    
    # Check if emulator container exists
    $container = docker ps -a --format "{{.Names}}" | Select-String -Pattern "cosmos-emulator"
    
    if (-not $container) {
        Write-Host "Status: Not Created" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "?? Run '.\cosmos.ps1 start' to create and start the emulator" -ForegroundColor Yellow
        Write-Host ""
        Pop-Location
        exit 0
    }
    
    # Get container status
    $status = docker inspect cosmos-emulator --format '{{.State.Status}}'
    $health = docker inspect cosmos-emulator --format '{{.State.Health.Status}}' 2>$null
    $uptime = docker inspect cosmos-emulator --format '{{.State.StartedAt}}'
    
    Write-Host "Container: cosmos-emulator" -ForegroundColor Cyan
    Write-Host "Status: $status" -ForegroundColor $(if ($status -eq 'running') { 'Green' } else { 'Red' })
    
    if ($health) {
        Write-Host "Health: $health" -ForegroundColor $(if ($health -eq 'healthy') { 'Green' } elseif ($health -eq 'starting') { 'Yellow' } else { 'Red' })
    }
    
    if ($status -eq 'running') {
        Write-Host "Started: $uptime" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "?? Connection:" -ForegroundColor Cyan
        Write-Host "   Endpoint: https://localhost:8081"
        Write-Host "   Data Explorer: https://localhost:8081/_explorer/index.html"
        Write-Host ""
        Write-Host "?? Commands:" -ForegroundColor Yellow
        Write-Host "   View logs: .\cosmos.ps1 logs"
        Write-Host "   Initialize: .\cosmos.ps1 init"
        Write-Host "   Stop: .\cosmos.ps1 stop"
    }
    else {
        Write-Host ""
        Write-Host "?? Run '.\cosmos.ps1 start' to start the emulator" -ForegroundColor Yellow
    }
    
    Write-Host ""
}

function Show-Help {
    Write-Header "Azure Cosmos DB Emulator - Help"
    
    Write-Host "Usage: .\cosmos.ps1 <command>" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Available Commands:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  start       Start Cosmos DB Emulator"
    Write-Host "  stop        Stop Cosmos DB Emulator"
    Write-Host "  restart     Restart Cosmos DB Emulator"
    Write-Host "  init        Initialize database and container"
    Write-Host "  clean       Stop and remove all data"
    Write-Host "  logs        View live logs"
    Write-Host "  status      Check emulator status"
    Write-Host "  help        Show this help message"
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  .\cosmos.ps1 start      # Start emulator"
    Write-Host "  .\cosmos.ps1 init       # Initialize database"
    Write-Host "  .\cosmos.ps1 status     # Check status"
    Write-Host "  .\cosmos.ps1 logs       # View logs"
    Write-Host "  .\cosmos.ps1 stop       # Stop emulator"
    Write-Host ""
    Write-Host "?? Documentation:" -ForegroundColor Cyan
    Write-Host "   See DOCKER-COSMOS-README.md for detailed information"
    Write-Host ""
}

# Execute command
switch ($Command) {
    'start'   { Start-CosmosEmulator }
    'stop'    { Stop-CosmosEmulator }
    'restart' { Restart-CosmosEmulator }
    'init'    { Initialize-CosmosDB }
    'clean'   { Clear-CosmosData }
    'logs'    { Show-Logs }
    'status'  { Show-Status }
    'help'    { Show-Help }
    default   { Show-Help }
}

Pop-Location
