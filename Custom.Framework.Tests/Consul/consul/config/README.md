# Consul Configuration Directory

This directory contains Consul server configuration files.

## Files

- **`server.json`** - Main Consul server configuration
- **`services/`** - Directory for service definitions (optional)

## Configuration Explained

### server.json

```json
{
  "datacenter": "dc1",              // Name of the datacenter
  "data_dir": "/consul/data",       // Where Consul stores data
  "log_level": "INFO",              // Logging verbosity
  "server": true,                   // This is a server node
  "ui_config": {
    "enabled": true                 // Enable Web UI
  },
  "addresses": {
    "http": "0.0.0.0"               // Listen on all interfaces
  },
  "bootstrap_expect": 1,            // Single server mode
  "enable_script_checks": true      // Allow health check scripts
}
```

## Volume Mount

In docker-compose.yml:
```yaml
volumes:
  - ./consul/config:/consul/config  # Mount config directory
```

**Note:** The volume is mounted **without** `:ro` (read-only) flag to avoid `chown` permission errors.

## Service Definitions (Optional)

You can add service definitions in JSON format to register services automatically on Consul startup:

### Example: API Service

Create `services/my-api.json`:
```json
{
  "service": {
    "name": "my-api",
    "port": 80,
    "tags": ["api", "v1"],
    "check": {
      "http": "http://my-api:80/health",
      "interval": "10s"
    }
  }
}
```

## Troubleshooting

### Error: "chown: Read-only file system"
**Cause:** Volume mounted with `:ro` flag  
**Fix:** Remove `:ro` from volume mount or don't mount config directory

### Error: Config directory not found
**Cause:** Directory doesn't exist  
**Fix:** This directory is created automatically when you clone the repo

### Consul won't start
**Cause:** Invalid JSON in config files  
**Fix:** Validate JSON syntax using `jq` or online validator

## Best Practices

1. **Keep configs in version control** - Easy to track changes
2. **Use environment-specific configs** - Different settings for dev/prod
3. **Validate before deploying** - Test configs locally first
4. **Don't store secrets** - Use environment variables or vault

## References

- [Consul Configuration](https://developer.hashicorp.com/consul/docs/agent/config/config-files)
- [Service Definitions](https://developer.hashicorp.com/consul/docs/services)
