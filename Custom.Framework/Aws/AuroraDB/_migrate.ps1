# Aurora DB Quick Start Script
# This script helps you quickly set up and migrate your Aurora database

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Aurora DB Migration Quick Start" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet ef is installed
Write-Host "Checking for EF Core tools..." -ForegroundColor Yellow
$efInstalled = dotnet tool list -g | Select-String "dotnet-ef"

if (-not $efInstalled) {
    Write-Host "EF Core tools not found. Installing..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-ef
    Write-Host "EF Core tools installed successfully" -ForegroundColor Green
} else {
    Write-Host "EF Core tools already installed" -ForegroundColor Green
}

Write-Host ""

# Navigate to Custom.Framework project
$projectPath = "D:\Projects\DotNetCore\NetCore8.Infrastructure\Custom.Framework"
Set-Location $projectPath
Write-Host "Working directory: $projectPath" -ForegroundColor Yellow

Write-Host ""
Write-Host "Select an option:" -ForegroundColor Cyan
Write-Host "1. Create Initial Migration" -ForegroundColor White
Write-Host "2. Apply Migrations to Database" -ForegroundColor White
Write-Host "3. Generate SQL Script" -ForegroundColor White
Write-Host "4. List Migrations" -ForegroundColor White
Write-Host "5. Remove Last Migration" -ForegroundColor White
Write-Host "6. Check Migration Status" -ForegroundColor White
Write-Host "7. Exit" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Enter your choice (1-7)"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "Creating initial migration..." -ForegroundColor Yellow
        dotnet ef migrations add InitialCreate --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations
        Write-Host ""
        Write-Host "Migration created successfully!" -ForegroundColor Green
        Write-Host "Files created in: Aws/AuroraDB/Migrations/" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Cyan
        Write-Host "1. Review the generated migration files" -ForegroundColor White
        Write-Host "2. Run this script again and select option 2 to apply the migration" -ForegroundColor White
    }
    "2" {
        Write-Host ""
        Write-Host "Applying migrations to database..." -ForegroundColor Yellow
        dotnet ef database update --context AuroraDbContext
        Write-Host ""
        Write-Host "Migrations applied successfully!" -ForegroundColor Green
    }
    "3" {
        Write-Host ""
        $outputFile = Read-Host "Enter output file name (default: migration.sql)"
        if ([string]::IsNullOrWhiteSpace($outputFile)) {
            $outputFile = "migration.sql"
        }
        Write-Host "Generating SQL script..." -ForegroundColor Yellow
        dotnet ef migrations script --context AuroraDbContext --idempotent --output $outputFile
        Write-Host ""
        Write-Host "SQL script generated: $outputFile" -ForegroundColor Green
    }
    "4" {
        Write-Host ""
        Write-Host "Listing all migrations..." -ForegroundColor Yellow
        dotnet ef migrations list --context AuroraDbContext
    }
    "5" {
        Write-Host ""
        Write-Host "WARNING: This will remove the last migration" -ForegroundColor Red
        $confirm = Read-Host "Are you sure? (yes/no)"
        if ($confirm -eq "yes") {
            Write-Host "Removing last migration..." -ForegroundColor Yellow
            dotnet ef migrations remove --context AuroraDbContext
            Write-Host "Migration removed successfully" -ForegroundColor Green
        } else {
            Write-Host "Operation cancelled" -ForegroundColor Yellow
        }
    }
    "6" {
        Write-Host ""
        Write-Host "Checking migration status..." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Applied migrations:" -ForegroundColor Cyan
        dotnet ef migrations list --context AuroraDbContext
        Write-Host ""
        Write-Host "Checking for pending changes..." -ForegroundColor Cyan
        dotnet ef migrations has-pending-model-changes --context AuroraDbContext
    }
    "7" {
        Write-Host "Goodbye!" -ForegroundColor Cyan
        exit
    }
    default {
        Write-Host "Invalid choice. Please run the script again." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
