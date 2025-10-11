#!/bin/bash

# Aurora DB Migration Quick Start Script
# This script helps you quickly set up and migrate your Aurora database

echo "=================================="
echo "Aurora DB Migration Quick Start"
echo "=================================="
echo ""

# Check if dotnet ef is installed
echo "Checking for EF Core tools..."
if ! dotnet tool list -g | grep -q "dotnet-ef"; then
    echo "EF Core tools not found. Installing..."
    dotnet tool install --global dotnet-ef
    echo "EF Core tools installed successfully"
else
    echo "EF Core tools already installed"
fi

echo ""

# Navigate to Custom.Framework project
PROJECT_PATH="./Custom.Framework"
cd "$PROJECT_PATH" || exit
echo "Working directory: $(pwd)"

echo ""
echo "Select an option:"
echo "1. Create Initial Migration"
echo "2. Apply Migrations to Database"
echo "3. Generate SQL Script"
echo "4. List Migrations"
echo "5. Remove Last Migration"
echo "6. Check Migration Status"
echo "7. Exit"
echo ""

read -p "Enter your choice (1-7): " choice

case $choice in
    1)
        echo ""
        echo "Creating initial migration..."
        dotnet ef migrations add InitialCreate --context AuroraDbContext --output-dir Aws/AuroraDB/Migrations
        echo ""
        echo "Migration created successfully!"
        echo "Files created in: Aws/AuroraDB/Migrations/"
        echo ""
        echo "Next steps:"
        echo "1. Review the generated migration files"
        echo "2. Run this script again and select option 2 to apply the migration"
        ;;
    2)
        echo ""
        echo "Applying migrations to database..."
        dotnet ef database update --context AuroraDbContext
        echo ""
        echo "Migrations applied successfully!"
        ;;
    3)
        echo ""
        read -p "Enter output file name (default: migration.sql): " outputFile
        outputFile=${outputFile:-migration.sql}
        echo "Generating SQL script..."
        dotnet ef migrations script --context AuroraDbContext --idempotent --output "$outputFile"
        echo ""
        echo "SQL script generated: $outputFile"
        ;;
    4)
        echo ""
        echo "Listing all migrations..."
        dotnet ef migrations list --context AuroraDbContext
        ;;
    5)
        echo ""
        echo "WARNING: This will remove the last migration"
        read -p "Are you sure? (yes/no): " confirm
        if [ "$confirm" = "yes" ]; then
            echo "Removing last migration..."
            dotnet ef migrations remove --context AuroraDbContext
            echo "Migration removed successfully"
        else
            echo "Operation cancelled"
        fi
        ;;
    6)
        echo ""
        echo "Checking migration status..."
        echo ""
        echo "Applied migrations:"
        dotnet ef migrations list --context AuroraDbContext
        echo ""
        echo "Checking for pending changes..."
        dotnet ef migrations has-pending-model-changes --context AuroraDbContext
        ;;
    7)
        echo "Goodbye!"
        exit 0
        ;;
    *)
        echo "Invalid choice. Please run the script again."
        exit 1
        ;;
esac

echo ""
echo "Press Enter to exit..."
read -r
