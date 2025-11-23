# Docker Network Cleanup Script
# Run this if you encounter network conflicts

Write-Host "?? Checking for existing Docker networks..." -ForegroundColor Yellow

# List all custom networks
$networks = docker network ls --filter "name=custom-microservices" --format "{{.Name}}"

if ($networks) {
    Write-Host "??  Found existing networks:" -ForegroundColor Yellow
    docker network ls --filter "name=custom-microservices"
    
    $confirm = Read-Host "`nDo you want to remove these networks? (y/N)"
    
    if ($confirm -eq 'y' -or $confirm -eq 'Y') {
        Write-Host "`n?? Stopping containers using these networks..." -ForegroundColor Yellow
        
        # Stop all containers first
        docker-compose -f Custom.Framework.Tests\Consul\docker-compose.yml down 2>$null
        docker-compose -f docker-compose.consul.yml down 2>$null
        
        # Remove networks
        foreach ($network in $networks) {
            Write-Host "Removing network: $network" -ForegroundColor Cyan
            docker network rm $network 2>$null
        }
        
        Write-Host "? Networks removed successfully!" -ForegroundColor Green
    }
} else {
    Write-Host "? No conflicting networks found!" -ForegroundColor Green
}

Write-Host "`n?? Current Docker networks:" -ForegroundColor Cyan
docker network ls

Write-Host "`n?? You can now start your services:" -ForegroundColor Yellow
Write-Host "   docker-compose -f Custom.Framework.Tests\Consul\docker-compose.yml up -d" -ForegroundColor White
