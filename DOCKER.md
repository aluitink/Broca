# Broca ActivityPub - Docker Deployment

This directory contains the Docker Compose configuration for running the complete Broca ActivityPub stack.

## Architecture

The application consists of three containerized services:

- **broca-api** - ASP.NET Core Web API hosting the ActivityPub server implementation
- **broca-web** - Blazor WebAssembly frontend application
- **nginx** - Reverse proxy for routing and load balancing

```
┌─────────────────────────────────────────┐
│              nginx (Port 80)             │
│         Reverse Proxy & Routing          │
└────────┬──────────────────────┬─────────┘
         │                      │
    ┌────▼─────┐          ┌────▼─────┐
    │broca-api │          │broca-web │
    │ (8080)   │          │  (8080)  │
    └──────────┘          └──────────┘
```

### Routing

- `/ap/*` → broca-api (ActivityPub endpoints)
- `/.well-known/*` → broca-api (WebFinger, etc.)
- `/*` → broca-web (Frontend application)

## Quick Start

### Development

```bash
# Start all services
docker-compose up

# Or build and start in detached mode
docker-compose up --build -d

# View logs
docker-compose logs -f

# Stop services
docker-compose down
```

The application will be available at:
- **Main Application**: http://localhost
- **API (direct)**: http://localhost:5050
- **Web (direct)**: http://localhost:5051

### Production

```bash
# Use only the base compose file (without override)
docker-compose -f docker-compose.yml up -d
```

## Configuration

### Environment Variables

Configure the API via environment variables in [docker-compose.yml](docker-compose.yml):

| Variable | Description | Default |
|----------|-------------|---------|
| `ActivityPub__BaseUrl` | Public URL for ActivityPub endpoints | `http://localhost/ap` |
| `ActivityPub__PrimaryDomain` | Primary domain for actors | `localhost` |
| `ActivityPub__ServerName` | Server display name | `Broca ActivityPub Server` |
| `ActivityPub__SystemActorUsername` | System actor username | `sys` |
| `ActivityPub__RoutePrefix` | API route prefix | `ap` |
| `Persistence__DataPath` | Data storage path | `/app/data` |

### Volumes

- `broca-data` - Persistent storage for ActivityPub data
- `./data` (development) - Local data directory mounted for easy access

### Ports

**Production (docker-compose.yml only):**
- Port 80 - Nginx reverse proxy (only exposed port)

**Development (with docker-compose.override.yml):**
- Port 80 - Nginx reverse proxy
- Port 5050 - Direct API access (debugging)
- Port 5051 - Direct Web access (debugging)

## Health Checks

All services include health checks:

- **broca-api**: WebFinger endpoint check
- **broca-web**: HTTP root endpoint check
- **nginx**: Custom `/health` endpoint

```bash
# Check service health
docker-compose ps
```

## Logs

```bash
# View all logs
docker-compose logs

# Follow logs for a specific service
docker-compose logs -f broca-api
docker-compose logs -f broca-web
docker-compose logs -f nginx

# View last 100 lines
docker-compose logs --tail=100
```

## Data Management

### Backup Data

```bash
# Create backup of persistent volume
docker run --rm -v broca-data:/data -v $(pwd):/backup alpine tar czf /backup/broca-data-backup.tar.gz -C /data .
```

### Restore Data

```bash
# Restore from backup
docker run --rm -v broca-data:/data -v $(pwd):/backup alpine tar xzf /backup/broca-data-backup.tar.gz -C /data
```

### Clear All Data

```bash
# Stop services and remove volume
docker-compose down -v
```

## Nginx Configuration

The nginx configuration provides:

- **Reverse proxying** for API and Web services
- **Rate limiting** (10 req/s for API, 30 req/s for Web)
- **CORS headers** for ActivityPub endpoints
- **Gzip compression** for all text-based content
- **Static asset caching** (1 year for immutable assets)
- **Security headers** (X-Frame-Options, CSP, etc.)

Edit [nginx/nginx.conf](nginx/nginx.conf) to customize routing or add SSL.

### Adding SSL/TLS

1. Mount certificates in `docker-compose.yml`:
   ```yaml
   nginx:
     volumes:
       - ./certs:/etc/nginx/certs:ro
   ```

2. Update `nginx/nginx.conf` to include SSL configuration:
   ```nginx
   server {
       listen 443 ssl http2;
       ssl_certificate /etc/nginx/certs/cert.pem;
       ssl_certificate_key /etc/nginx/certs/key.pem;
       # ... rest of config
   }
   ```

## Troubleshooting

### Services won't start

```bash
# Check service status
docker-compose ps

# View detailed logs
docker-compose logs

# Rebuild containers
docker-compose build --no-cache
docker-compose up
```

### API returns 502 Bad Gateway

- Check if broca-api is healthy: `docker-compose ps`
- View API logs: `docker-compose logs broca-api`
- Verify network connectivity: `docker network inspect broca_broca-network`

### Web app shows blank page

- Check browser console for errors
- Verify nginx is routing correctly: `docker-compose logs nginx`
- Check if broca-web is healthy: `docker-compose ps`

### Data not persisting

- Verify volume exists: `docker volume ls | grep broca`
- Check volume mount: `docker-compose config`
- Inspect volume: `docker volume inspect broca_broca-data`

## Development

For local development without Docker, see:
- [src/Broca.API/README.md](src/Broca.API/README.md) - API development
- [src/Broca.Web/README.md](src/Broca.Web/README.md) - Web development

## Production Deployment

For production deployments:

1. Set appropriate `ActivityPub__BaseUrl` with your domain
2. Configure SSL/TLS certificates
3. Set up proper DNS records
4. Configure firewall rules
5. Use docker-compose without the override file
6. Consider using Docker Swarm or Kubernetes for orchestration
7. Set up monitoring and alerting
8. Configure automated backups

## Support

For issues and questions:
- GitHub Issues: https://github.com/brocadev/broca/issues
- Documentation: See individual project READMEs
