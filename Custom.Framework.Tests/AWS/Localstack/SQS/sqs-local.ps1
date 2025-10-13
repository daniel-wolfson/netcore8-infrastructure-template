# ================================================
# Amazon SQS Learning Script
# ================================================

param(
    [switch]$Start,
    [switch]$Stop,
    [switch]$Restart,
    [switch]$Status,
    [switch]$Test,
    [switch]$Clean
)

$endpoint = "http://localhost:4566"
$queueBaseUrl = "http://localhost:4566/000000000000"

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

function Test-LocalStackHealthy {
    try {
        $response = Invoke-RestMethod -Uri "$endpoint/_localstack/health" -TimeoutSec 2
        return $true
    } catch {
        return $false
    }
}

function Start-SqsService {
    Write-Header "Starting Amazon SQS (LocalStack)"
    
    if (-not (Test-DockerRunning)) {
        return
    }

    Write-Host "[INFO] Starting LocalStack for SQS..." -ForegroundColor Green
    
    # Navigate to parent directory where docker-compose.yaml is located
    Push-Location (Join-Path $PSScriptRoot "..")
    
    docker-compose up -d localstack

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[INFO] Waiting for LocalStack to be ready..." -ForegroundColor Yellow
        Start-Sleep -Seconds 10

        $attempts = 0
        $maxAttempts = 30
        
        Write-Host "[INFO] Checking LocalStack health..." -NoNewline
        while (-not (Test-LocalStackHealthy) -and $attempts -lt $maxAttempts) {
            Write-Host "." -NoNewline
            Start-Sleep -Seconds 1
            $attempts++
        }
        Write-Host ""

        if (Test-LocalStackHealthy) {
            Write-Host "[SUCCESS] LocalStack is running!" -ForegroundColor Green
            Show-SqsQuickStart
        } else {
            Write-Host "[WARNING] LocalStack health check failed" -ForegroundColor Yellow
            Write-Host "View logs: docker logs localstack-main -f" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[ERROR] Failed to start LocalStack" -ForegroundColor Red
    }
    
    Pop-Location
}

function Stop-SqsService {
    Write-Header "Stopping Amazon SQS (LocalStack)"
    
    Push-Location (Join-Path $PSScriptRoot "..")
    
    docker-compose stop localstack
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[SUCCESS] LocalStack stopped" -ForegroundColor Green
    }
    
    Pop-Location
}

function Restart-SqsService {
    Write-Header "Restarting Amazon SQS (LocalStack)"
    
    Stop-SqsService
    Start-Sleep -Seconds 2
    Start-SqsService
}

function Show-Status {
    Write-Header "Amazon SQS Status"
    
    Push-Location (Join-Path $PSScriptRoot "..")
    
    Write-Host "Docker Container:" -ForegroundColor Cyan
    docker-compose ps localstack
    
    Pop-Location
    
    Write-Host ""
    Write-Host "LocalStack Status:" -ForegroundColor Cyan
    if (Test-LocalStackHealthy) {
        Write-Host "  [STATUS] LocalStack is HEALTHY ?" -ForegroundColor Green
        
        Write-Host ""
        Write-Host "  SQS Queues:" -ForegroundColor Yellow
        List-Queues
    } else {
        Write-Host "  [STATUS] LocalStack is NOT RUNNING ?" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Start SQS with: .\sqs-learning.ps1 -Start" -ForegroundColor Yellow
    }
}

function List-Queues {
    try {
        $queues = aws --endpoint-url=$endpoint sqs list-queues --output json 2>$null | ConvertFrom-Json
        
        if ($queues.QueueUrls) {
            foreach ($queueUrl in $queues.QueueUrls) {
                $queueName = $queueUrl -replace ".*/(.*)", '$1'
                Write-Host "    ? $queueName" -ForegroundColor Green
                
                # Get message count
                $attrs = aws --endpoint-url=$endpoint sqs get-queue-attributes `
                    --queue-url $queueUrl `
                    --attribute-names ApproximateNumberOfMessages `
                    --output json 2>$null | ConvertFrom-Json
                
                if ($attrs) {
                    $msgCount = $attrs.Attributes.ApproximateNumberOfMessages
                    Write-Host "      Messages: $msgCount" -ForegroundColor Gray
                }
            }
        } else {
            Write-Host "    No queues found" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "    Error listing queues: $_" -ForegroundColor Red
    }
}

function Test-SqsOperations {
    Write-Header "Running SQS Tests"
    
    if (-not (Test-LocalStackHealthy)) {
        Write-Host "[ERROR] LocalStack is not running!" -ForegroundColor Red
        Write-Host "Start SQS with: .\sqs-learning.ps1 -Start" -ForegroundColor Yellow
        return
    }

    Write-Host "[INFO] Running SQS integration tests..." -ForegroundColor Cyan
    Write-Host ""
    
    # Navigate to solution root (3 levels up from SQS folder)
    Push-Location (Join-Path $PSScriptRoot "..\..\..") 
    
    dotnet test --filter "FullyQualifiedName~AmazonSqsTests" --verbosity minimal
    
    Pop-Location
    
    Write-Host ""
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[SUCCESS] SQS tests passed! ?" -ForegroundColor Green
    } else {
        Write-Host "[FAILURE] Some SQS tests failed ?" -ForegroundColor Red
    }
}

function Clean-SqsEnvironment {
    Write-Header "Cleaning SQS Environment"
    
    Push-Location (Join-Path $PSScriptRoot "..")
    
    Write-Host "[INFO] Stopping LocalStack container..." -ForegroundColor Yellow
    docker-compose stop localstack
    
    Write-Host "[INFO] Removing LocalStack container..." -ForegroundColor Yellow
    docker-compose rm -f localstack
    
    Write-Host "[INFO] Removing LocalStack volumes..." -ForegroundColor Yellow
    if (Test-Path ".\volume") {
        Remove-Item ".\volume\*" -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "[SUCCESS] Volume cleaned" -ForegroundColor Green
    }
    
    Pop-Location
    
    Write-Host "[SUCCESS] SQS environment cleaned!" -ForegroundColor Green
}

function Show-SqsQuickStart {
    Write-Host ""
    Write-Host "?? SQS Quick Start Commands:" -ForegroundColor Cyan
    Write-Host "??????????????????????????????????????????????" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Check status:" -ForegroundColor White
    Write-Host "  .\sqs-learning.ps1 -Status" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Run SQS tests:" -ForegroundColor White
    Write-Host "  .\sqs-learning.ps1 -Test" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Send a message:" -ForegroundColor White
    Write-Host "  aws --endpoint-url=$endpoint sqs send-message \\" -ForegroundColor Gray
    Write-Host "    --queue-url $queueBaseUrl/test-orders-queue \\" -ForegroundColor Gray
    Write-Host "    --message-body 'Hello from SQS!'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Receive messages:" -ForegroundColor White
    Write-Host "  aws --endpoint-url=$endpoint sqs receive-message \\" -ForegroundColor Gray
    Write-Host "    --queue-url $queueBaseUrl/test-orders-queue \\" -ForegroundColor Gray
    Write-Host "    --max-number-of-messages 10" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# List queues:" -ForegroundColor White
    Write-Host "  aws --endpoint-url=$endpoint sqs list-queues" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# View logs:" -ForegroundColor White
    Write-Host "  docker logs localstack-main -f" -ForegroundColor Gray
    Write-Host ""
    Write-Host "# Interactive CLI:" -ForegroundColor White
    Write-Host "  .\sqs-cli.bat" -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? Endpoint:" -ForegroundColor Cyan
    Write-Host "  LocalStack: $endpoint" -ForegroundColor Gray
    Write-Host "  Health: $endpoint/_localstack/health" -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? Documentation:" -ForegroundColor Cyan
    Write-Host "  Complete Guide: SQS_README.md" -ForegroundColor Gray
    Write-Host "  Quick Reference: SQS_QUICK_REFERENCE.md" -ForegroundColor Gray
    Write-Host ""
}

function Show-Help {
    Write-Header "Amazon SQS Learning Script"
    
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  .\sqs-learning.ps1 [Options]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Cyan
    Write-Host "  -Start        Start LocalStack for SQS" -ForegroundColor White
    Write-Host "  -Stop         Stop LocalStack" -ForegroundColor White
    Write-Host "  -Restart      Restart LocalStack" -ForegroundColor White
    Write-Host "  -Status       Show SQS service status" -ForegroundColor White
    Write-Host "  -Test         Run SQS integration tests" -ForegroundColor White
    Write-Host "  -Clean        Clean environment (remove volumes)" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host "  .\sqs-learning.ps1 -Start" -ForegroundColor Gray
    Write-Host "  .\sqs-learning.ps1 -Status" -ForegroundColor Gray
    Write-Host "  .\sqs-learning.ps1 -Test" -ForegroundColor Gray
    Write-Host "  .\sqs-learning.ps1 -Clean" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Documentation:" -ForegroundColor Cyan
    Write-Host "  SQS_README.md - Complete guide" -ForegroundColor Gray
    Write-Host "  SQS_QUICK_REFERENCE.md - Command reference" -ForegroundColor Gray
    Write-Host ""
}

# Main execution
Push-Location $PSScriptRoot

try {
    if ($Start) {
        Start-SqsService
    }
    elseif ($Stop) {
        Stop-SqsService
    }
    elseif ($Restart) {
        Restart-SqsService
    }
    elseif ($Status) {
        Show-Status
    }
    elseif ($Test) {
        Test-SqsOperations
    }
    elseif ($Clean) {
        Clean-SqsEnvironment
    }
    else {
        Show-Help
    }
}
finally {
    Pop-Location
}
