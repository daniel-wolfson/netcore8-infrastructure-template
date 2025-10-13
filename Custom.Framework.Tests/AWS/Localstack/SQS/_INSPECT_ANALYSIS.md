# Docker Inspect Analysis & LocalStack Configuration Update

## Analysis Date
2025-01-20

## Source
`lockstack.inspect.json` - Docker inspect output from running LocalStack container

## Key Findings from Docker Inspect

### 1. Container State Issues
```json
"State": {
    "Status": "exited",
    "ExitCode": 137,  // SIGKILL - Container was killed
    "Running": false,
    "Health": {
        "Status": "unhealthy",
        "FailingStreak": 0
    }
}
```

**Analysis:**
- Exit Code 137 = 128 + 9 (SIGKILL signal)
- Container was forcibly killed, likely due to resource constraints or Docker daemon shutdown
- Health checks were passing (ExitCode 0 in logs) but overall status was "unhealthy"

### 2. Health Check Configuration
```json
"Healthcheck": {
    "Test": ["CMD", "curl", "-f", "http://localhost:4566/_localstack/health"],
    "Interval": 10000000000,     // 10 seconds
    "Timeout": 5000000000,       // 5 seconds
    "StartPeriod": 30000000000,  // 30 seconds
    "Retries": 5
}
```

**Health Check Output:**
```json
{
    "services": {
        "sqs": "running",
        "dynamodb": "running",
        "s3": "running",
        "rds": "disabled"
    },
    "edition": "community",
    "version": "4.9.3.dev26"
}
```

### 3. Network Configuration
```json
"NetworkMode": "aws_localstack-network",
"Networks": {
    "aws_localstack-network": {
        "Aliases": ["localstack-main", "localstack"],
        "DNSNames": ["localstack-main", "localstack", "64ad39669ea4"]
    }
}
```

### 4. Port Bindings
```json
"PortBindings": {
    "4566/tcp": [{"HostIp": "127.0.0.1", "HostPort": "4566"}],
    "4510/tcp": [{"HostIp": "127.0.0.1", "HostPort": "4510"}],
    "4559/tcp": [{"HostIp": "127.0.0.1", "HostPort": "4559"}]
}
```

**Note:** Ports are bound to `127.0.0.1` (localhost only)

### 5. Volume Mounts
```json
"Mounts": [
    {
        "Source": "D:\\Projects\\DotNetCore\\NetCore8.Infrastructure\\Custom.Framework.Tests\\AWS\\volume",
        "Destination": "/var/lib/localstack",
        "Mode": "rw"
    },
    {
        "Source": "/var/run/docker.sock",
        "Destination": "/var/run/docker.sock",
        "Mode": "rw"
    }
]
```

**Docker Socket Mount:** Required for Lambda support (Docker-in-Docker)

### 6. Restart Policy
```json
"RestartPolicy": {
    "Name": "no",
    "MaximumRetryCount": 0
}
```

Container will **not** restart automatically on failure.

## Changes Applied to AmazonSqsTests.cs

### Before
```csharp
private static readonly IContainer _localStackContainer = new LocalStackBuilder()
    .WithImage("localstack/localstack:latest")
    .WithName(Environment.GetEnvironmentVariable("LOCALSTACK_DOCKER_NAME") ?? "localstack-test-main")
    .WithNetwork("bridge")  // ? Wrong network
    .WithPortBinding(4566, 4566)
    .WithPortBinding(4510, 4510)
    .WithPortBinding(4559, 4559)
    // ... environment variables ...
    .WithBindMount(Environment.GetEnvironmentVariable("LOCALSTACK_VOLUME_DIR") 
        ?? $"{TestAwsPath}/volume", "/var/lib/localstack")
    //.WithBindMount("/var/run/docker.sock", "/var/run/docker.sock")  // ? Commented out
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilHttpRequestIsSucceeded(r => r
            .ForPath("/_localstack/health")
            .ForPort(4566)))
    .Build();
```

### After
```csharp
private static readonly IContainer _localStackContainer = new LocalStackBuilder()
    .WithImage("localstack/localstack:latest")
    .WithName(Environment.GetEnvironmentVariable("LOCALSTACK_DOCKER_NAME") ?? "localstack-test-main")
    .WithNetwork("aws_localstack-network")  // ? Correct network name
    .WithPortBinding(4566, 4566) // Maps host 4566 -> container 4566
    .WithPortBinding(4510, 4510) // Maps host 4510 -> container 4510
    .WithPortBinding(4559, 4559) // Maps host 4559 -> container 4559
    .WithEnvironment("LOCALSTACK_AUTH_TOKEN", Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN") ?? string.Empty)  // ? Null-safe
    .WithEnvironment("SERVICES", "sqs,rds,dynamodb,s3")
    .WithEnvironment("DEBUG", Environment.GetEnvironmentVariable("DEBUG") ?? "1")
    .WithEnvironment("PERSISTENCE", Environment.GetEnvironmentVariable("PERSISTENCE") ?? "0")
    .WithEnvironment("LAMBDA_EXECUTOR", Environment.GetEnvironmentVariable("LAMBDA_EXECUTOR") ?? "local")
    .WithEnvironment("DOCKER_HOST", "unix:///var/run/docker.sock")
    .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
    .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
    .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
    .WithEnvironment("EAGER_SERVICE_LOADING", "1")
    .WithEnvironment("SQS_ENDPOINT_STRATEGY", "path")
    .WithEnvironment("RDS_PG_CUSTOM_VERSIONS", "16.1")
    .WithBindMount(Environment.GetEnvironmentVariable("LOCALSTACK_VOLUME_DIR") 
        ?? $"{TestAwsPath}/volume", "/var/lib/localstack")
    .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock")  // ? Re-enabled for Lambda support
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilHttpRequestIsSucceeded(r => r
            .ForPath("/_localstack/health")
            .ForPort(4566)
            .ForStatusCode(System.Net.HttpStatusCode.OK)))  // ? Explicit status code check
    .Build();
```

## Key Changes Summary

### 1. ? Network Configuration
- **Changed from:** `"bridge"`
- **Changed to:** `"aws_localstack-network"`
- **Reason:** Matches actual Docker runtime network configuration

### 2. ? Docker Socket Mount
- **Status:** Re-enabled
- **Path:** `/var/run/docker.sock:/var/run/docker.sock`
- **Reason:** Required for Lambda support (Docker-in-Docker scenarios)

### 3. ? Enhanced Wait Strategy
- **Added:** `.ForStatusCode(System.Net.HttpStatusCode.OK)`
- **Reason:** More robust health check validation

### 4. ? Null Safety
- **Changed:** `LOCALSTACK_AUTH_TOKEN` handling
- **From:** `Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN")`
- **To:** `Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN") ?? string.Empty`
- **Reason:** Prevents null reference exceptions

### 5. ? Project File Fix
- **File:** `Custom.Framework.Tests.csproj`
- **Changed:** `lockstack.inspect.json` from `<Compile>` to `<None>`
- **Reason:** JSON files should not be compiled as C# code

## Health Check Behavior

### Docker Native Health Check
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:4566/_localstack/health"]
  interval: 10s
  timeout: 5s
  retries: 5
  start_period: 30s
```

### Testcontainers Wait Strategy
```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilHttpRequestIsSucceeded(r => r
        .ForPath("/_localstack/health")
        .ForPort(4566)
        .ForStatusCode(System.Net.HttpStatusCode.OK)))
```

**Both mechanisms** ensure LocalStack is ready before tests execute.

## Troubleshooting Insights

### Exit Code 137 Analysis
**Possible Causes:**
1. **Memory Limit Exceeded:** Container exceeded memory limits
2. **Docker Daemon Shutdown:** System shutdown or Docker Desktop restart
3. **Manual Kill:** `docker stop` or `docker kill` command
4. **Resource Constraints:** Host machine resource pressure

**Solutions:**
1. Increase Docker memory limits
2. Ensure proper container cleanup between test runs
3. Use `docker-compose down -v` to clean volumes
4. Monitor Docker Desktop resource usage

### Health Check Passing But Container Unhealthy
**Explanation:**
- Health checks (curl commands) were succeeding (ExitCode: 0)
- Container was marked "unhealthy" because it was in "exited" state
- This is normal behavior when container is stopped

## Docker Network Details

### Network Type
- **Name:** `aws_localstack-network`
- **Driver:** bridge
- **Network ID:** `e6fef426fc0fe919f8fc576ba91e7bdddedca501a718ea8bda065e8ffa0bd794`

### DNS Resolution
Containers in the network can reach LocalStack via:
- `localstack-main` (container name)
- `localstack` (network alias)
- `64ad39669ea4` (container hostname)

## Services Status from Health Check

| Service | Status |
|---------|--------|
| **sqs** | ? running |
| **dynamodb** | ? running |
| **s3** | ? running |
| **kinesis** | ? running |
| **dynamodbstreams** | ? running |
| **rds** | ? disabled |
| **lambda** | ? disabled |

## Environment Variables in Running Container

```bash
DOCKER_HOST=unix:///var/run/docker.sock
AWS_ACCESS_KEY_ID=test
EAGER_SERVICE_LOADING=1
LAMBDA_EXECUTOR=local
LOCALSTACK_AUTH_TOKEN=test
AWS_SECRET_ACCESS_KEY=test
DEBUG=1
RDS_PG_CUSTOM_VERSIONS=16.1
SERVICES=sqs,rds,dynamodb,s3
SQS_ENDPOINT_STRATEGY=path
AWS_DEFAULT_REGION=us-east-1
PERSISTENCE=0
LOCALSTACK_BUILD_VERSION=4.9.3.dev26
```

## Recommendations

### 1. For Production Tests
- ? Use explicit health checks with status code validation
- ? Enable Docker socket mount for Lambda testing
- ? Use named networks for service discovery
- ? Set reasonable timeouts and wait strategies

### 2. For CI/CD Pipelines
- Consider using ephemeral containers
- Implement proper cleanup between test runs
- Monitor container resource usage
- Use fixed port bindings with fallback ranges

### 3. For Local Development
- Keep PERSISTENCE=0 for faster startup
- Use named volumes for persistent state if needed
- Monitor container logs for errors
- Ensure Docker Desktop has sufficient resources

## Verification Commands

```bash
# Check if LocalStack is healthy
curl http://localhost:4566/_localstack/health

# View container logs
docker logs localstack-test-main

# Inspect container
docker inspect localstack-test-main

# Check network
docker network inspect aws_localstack-network

# Monitor resources
docker stats localstack-test-main
```

## References

- [LocalStack Documentation](https://docs.localstack.cloud/)
- [Testcontainers Documentation](https://dotnet.testcontainers.org/)
- [Docker Health Checks](https://docs.docker.com/engine/reference/builder/#healthcheck)
- [Docker Exit Codes](https://docs.docker.com/engine/reference/run/#exit-status)

---

**Status:** ? Configuration Updated and Build Successful  
**Date:** 2025-01-20  
**Version:** LocalStack 4.9.3.dev26
