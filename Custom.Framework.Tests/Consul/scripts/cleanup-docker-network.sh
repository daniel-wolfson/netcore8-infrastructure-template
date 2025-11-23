#!/bin/bash

# Docker Network Cleanup Script
# Run this if you encounter network conflicts

set -e

echo "?? Checking for existing Docker networks..."

# List all custom networks
NETWORKS=$(docker network ls --filter "name=custom-microservices" --format "{{.Name}}" 2>/dev/null || true)

if [ -n "$NETWORKS" ]; then
    echo "??  Found existing networks:"
    docker network ls --filter "name=custom-microservices"
    
    read -p $'\nDo you want to remove these networks? (y/N): ' confirm
    
    if [[ "$confirm" =~ ^[Yy]$ ]]; then
        echo ""
        echo "?? Stopping containers using these networks..."
        
        # Stop all containers first
        docker-compose -f Custom.Framework.Tests/Consul/docker-compose.yml down 2>/dev/null || true
        docker-compose -f docker-compose.consul.yml down 2>/dev/null || true
        
        # Remove networks
        for network in $NETWORKS; do
            echo "Removing network: $network"
            docker network rm "$network" 2>/dev/null || true
        done
        
        echo "? Networks removed successfully!"
    fi
else
    echo "? No conflicting networks found!"
fi

echo ""
echo "?? Current Docker networks:"
docker network ls

echo ""
echo "?? You can now start your services:"
echo "   docker-compose -f Custom.Framework.Tests/Consul/docker-compose.yml up -d"
