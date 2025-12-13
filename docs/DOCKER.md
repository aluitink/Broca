# Docker Setup for Broca Sample Web API

This guide explains how to run the Broca Sample Web API using Docker.

## Prerequisites

- Docker
- Docker Compose

## Quick Start

1. **Build and start the container:**
   ```bash
   docker-compose up -d
   ```

2. **View logs:**
   ```bash
   docker logs broca-sample-api
   ```

3. **Stop the container:**
   ```bash
   docker-compose down
   ```

## Features

The Docker container includes:

- **In-memory persistence** - No external database required
- **System user (sys)** - Automatically initialized on startup
- **ActivityPub server** - Full ActivityPub protocol support
- **WebFinger** - Actor discovery via webfinger protocol

## Testing the Endpoints

### Get the sys user actor
```bash
curl http://localhost:5050/users/sys | jq .
```

### WebFinger lookup
```bash
curl "http://localhost:5050/.well-known/webfinger?resource=acct:sys@localhost" | jq .
```

### Sample weather forecast (demo endpoint)
```bash
curl http://localhost:5050/weatherforecast | jq .
```

## Configuration

The docker-compose.yml file configures the following environment variables:

- `ASPNETCORE_ENVIRONMENT`: Development
- `ActivityPub__BaseUrl`: http://localhost:5050
- `ActivityPub__PrimaryDomain`: localhost
- `ActivityPub__ServerName`: Broca Sample Server
- `ActivityPub__SystemActorUsername`: sys

You can modify these in `docker-compose.yml` to suit your needs.

## Ports

- **5050**: HTTP port exposed on the host (maps to container port 8080)

## Volumes

- `broca-data`: Persistent storage for application data (currently in-memory, ready for future persistence)

## Healthcheck

The container includes a healthcheck that queries the webfinger endpoint:
```bash
curl -f "http://localhost:8080/.well-known/webfinger?resource=acct:sys@localhost"
```

Check health status:
```bash
docker ps
```

## Troubleshooting

### Port already in use
If port 5050 is already allocated, modify the port mapping in `docker-compose.yml`:
```yaml
ports:
  - "YOUR_PORT:8080"
```

And update the `ActivityPub__BaseUrl` environment variable accordingly.

### View real-time logs
```bash
docker-compose logs -f broca-api
```

### Rebuild after code changes
```bash
docker-compose build --no-cache
docker-compose up -d
```
