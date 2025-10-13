#docker compose --env-file env -f docker-compose.yaml up  --force-recreate --no-deps --wait localstack
#docker compose --env-file env -f docker-compose.yaml up  --force-recreate --no-deps --wait sqs-init

# In PowerShell or CMD
docker-compose -f Custom.Framework.Tests\AWS\docker-compose.yaml up --profile init

# Check if containers are on same network
docker network inspect aws_localstack-network

# Check logs
docker logs sqs-init
docker logs localstack-main