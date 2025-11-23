# Makefile for Consul Docker Management
# Usage: make <target>

.PHONY: help consul-start consul-stop all-start all-stop status logs clean

# Default target
help:
	@echo "Consul Docker Management"
	@echo "========================"
	@echo "Available targets:"
	@echo "  consul-start    - Start Consul server only"
	@echo "  consul-stop     - Stop Consul server"
	@echo "  all-start       - Start all services (Consul + PostgreSQL + Redis + API)"
	@echo "  all-stop        - Stop all services"
	@echo "  status          - Show service status"
	@echo "  logs            - Show logs (use SERVICE=name to specify)"
	@echo "  clean           - Remove all containers and volumes"
	@echo "  test-consul     - Test Consul connectivity"
	@echo "  ui              - Open Consul UI in browser"

# Start Consul server only
consul-start:
	@echo "Starting Consul server..."
	@docker-compose -f docker-compose.consul.yml up -d
	@sleep 5
	@echo "? Consul started!"
	@echo "?? UI: http://localhost:8500/ui"

# Stop Consul server
consul-stop:
	@echo "Stopping Consul server..."
	@docker-compose -f docker-compose.consul.yml down
	@echo "? Consul stopped"

# Start all services
all-start:
	@echo "Starting all services..."
	@docker-compose up -d
	@sleep 10
	@echo "? All services started!"
	@echo "?? Services:"
	@echo "   - Consul UI: http://localhost:8500/ui"
	@echo "   - Your API: http://localhost:5000"
	@echo "   - PostgreSQL: localhost:5432"
	@echo "   - Redis: localhost:6379"

# Stop all services
all-stop:
	@echo "Stopping all services..."
	@docker-compose down
	@echo "? All services stopped"

# Show service status
status:
	@echo "Container Status:"
	@docker-compose ps
	@echo ""
	@echo "Consul Members:"
	@docker exec consul-server consul members 2>/dev/null || echo "Consul not running"

# Show logs
logs:
	@docker-compose logs -f $(or $(SERVICE),consul)

# Clean up everything
clean:
	@echo "??  This will remove all containers and volumes!"
	@read -p "Are you sure? (y/N): " confirm && [ "$$confirm" = "y" ] || exit 1
	@docker-compose down -v
	@echo "? Cleanup complete"

# Test Consul connectivity
test-consul:
	@echo "Testing Consul connectivity..."
	@curl -s http://localhost:8500/v1/status/leader && echo "? Consul API is accessible" || echo "? Consul API is not accessible"
	@curl -s http://localhost:8500/v1/catalog/services | jq . && echo "? Can list services" || echo "? Cannot list services"

# Open Consul UI (works on macOS/Linux with xdg-open or open command)
ui:
	@command -v open > /dev/null && open http://localhost:8500/ui || \
	 command -v xdg-open > /dev/null && xdg-open http://localhost:8500/ui || \
	 echo "Please open http://localhost:8500/ui in your browser"

# Development shortcuts
dev: consul-start
	@echo "Development environment ready!"

prod: all-start
	@echo "Production-like environment ready!"
