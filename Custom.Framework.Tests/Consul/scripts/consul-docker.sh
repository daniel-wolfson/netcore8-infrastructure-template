#!/bin/bash

# Consul Docker Setup Script
# This script helps you start and manage Consul with Docker

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Functions
print_success() {
    echo -e "${GREEN}? $1${NC}"
}

print_error() {
    echo -e "${RED}? $1${NC}"
}

print_info() {
    echo -e "${YELLOW}??  $1${NC}"
}

# Check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        print_error "Docker is not running. Please start Docker first."
        exit 1
    fi
    print_success "Docker is running"
}

# Start Consul
start_consul() {
    print_info "Starting Consul server..."
    docker-compose -f docker-compose.consul.yml up -d
    
    echo "Waiting for Consul to be healthy..."
    sleep 5
    
    if docker ps | grep -q consul-server; then
        print_success "Consul server started successfully"
        print_info "Consul UI: http://localhost:8500/ui"
        print_info "Consul API: http://localhost:8500"
    else
        print_error "Failed to start Consul"
        exit 1
    fi
}

# Start full infrastructure
start_all() {
    print_info "Starting complete infrastructure (Consul + Services)..."
    docker-compose up -d
    
    echo "Waiting for services to be healthy..."
    sleep 10
    
    print_success "Infrastructure started successfully"
    print_info "Services:"
    print_info "  - Consul UI: http://localhost:8500/ui"
    print_info "  - Your API: http://localhost:5000"
    print_info "  - PostgreSQL: localhost:5432"
    print_info "  - Redis: localhost:6379"
}

# Stop Consul
stop_consul() {
    print_info "Stopping Consul server..."
    docker-compose -f docker-compose.consul.yml down
    print_success "Consul stopped"
}

# Stop all services
stop_all() {
    print_info "Stopping all services..."
    docker-compose down
    print_success "All services stopped"
}

# Clean up (remove volumes)
cleanup() {
    print_info "Cleaning up volumes..."
    docker-compose down -v
    print_success "Cleanup complete"
}

# Show status
status() {
    print_info "Container Status:"
    docker-compose ps
    
    echo ""
    print_info "Consul Members:"
    docker exec consul-server consul members 2>/dev/null || echo "Consul not running"
    
    echo ""
    print_info "Registered Services:"
    curl -s http://localhost:8500/v1/catalog/services 2>/dev/null | jq . || echo "Consul API not accessible"
}

# Show logs
logs() {
    SERVICE=${1:-consul}
    print_info "Showing logs for: $SERVICE"
    docker-compose logs -f "$SERVICE"
}

# Main menu
show_menu() {
    echo ""
    echo "==================================="
    echo "   Consul Docker Management"
    echo "==================================="
    echo "1. Start Consul only"
    echo "2. Start complete infrastructure"
    echo "3. Stop Consul"
    echo "4. Stop all services"
    echo "5. Show status"
    echo "6. Show logs"
    echo "7. Cleanup (remove volumes)"
    echo "8. Exit"
    echo "==================================="
}

# Main script
main() {
    check_docker
    
    if [ $# -eq 0 ]; then
        # Interactive mode
        while true; do
            show_menu
            read -p "Enter choice [1-8]: " choice
            
            case $choice in
                1) start_consul ;;
                2) start_all ;;
                3) stop_consul ;;
                4) stop_all ;;
                5) status ;;
                6) 
                    read -p "Service name (default: consul): " service
                    logs "${service:-consul}"
                    ;;
                7) cleanup ;;
                8) 
                    print_info "Goodbye!"
                    exit 0
                    ;;
                *) print_error "Invalid option" ;;
            esac
            
            echo ""
            read -p "Press Enter to continue..."
        done
    else
        # Command line mode
        case $1 in
            start-consul) start_consul ;;
            start-all) start_all ;;
            stop-consul) stop_consul ;;
            stop-all) stop_all ;;
            status) status ;;
            logs) logs "$2" ;;
            cleanup) cleanup ;;
            *)
                echo "Usage: $0 {start-consul|start-all|stop-consul|stop-all|status|logs [service]|cleanup}"
                exit 1
                ;;
        esac
    fi
}

# Run main script
main "$@"
