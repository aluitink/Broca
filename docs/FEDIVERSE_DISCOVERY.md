# Fediverse Discovery Endpoints

This document describes the fediverse discovery endpoints implemented in Broca ActivityPub Server.

## Overview

Broca now supports standard fediverse discovery endpoints that are commonly used by ActivityPub implementations to discover instance metadata and capabilities. These endpoints are essential for federation with other instances in the threadiverse/fediverse.

## Implemented Endpoints

### 1. WebFinger (`.well-known/webfinger`)

Already implemented. Resolves account identifiers to ActivityPub actor URLs.

**Endpoint:** `GET /.well-known/webfinger?resource=acct:username@domain`

**Response:** JRD (JSON Resource Descriptor) with links to the actor's ActivityPub profile.

### 2. NodeInfo Discovery (`.well-known/nodeinfo`)

Provides links to available NodeInfo schemas supported by the instance.

**Endpoint:** `GET /.well-known/nodeinfo`

**Response:**
```json
{
  "links": [
    {
      "rel": "http://nodeinfo.diaspora.software/ns/schema/2.0",
      "href": "https://your-instance.com/nodeinfo/2.0"
    },
    {
      "rel": "http://nodeinfo.diaspora.software/ns/schema/2.1",
      "href": "https://your-instance.com/nodeinfo/2.1"
    }
  ]
}
```

### 3. NodeInfo 2.0 (`/nodeinfo/2.0`)

Provides detailed instance metadata following the NodeInfo 2.0 schema.

**Endpoint:** `GET /nodeinfo/2.0`

**Response:**
```json
{
  "version": "2.0",
  "software": {
    "name": "broca",
    "version": "1.0.0"
  },
  "protocols": ["activitypub"],
  "services": {
    "outbound": [],
    "inbound": []
  },
  "usage": {
    "users": {
      "total": 1,
      "activeMonth": 1,
      "activeHalfyear": 1
    },
    "localPosts": 0
  },
  "openRegistrations": false,
  "metadata": {
    "nodeName": "Your Instance Name",
    "nodeDescription": "Your instance description"
  }
}
```

### 4. NodeInfo 2.1 (`/nodeinfo/2.1`)

Similar to NodeInfo 2.0 but includes repository information.

**Endpoint:** `GET /nodeinfo/2.1`

**Response:** Same as 2.0 but with additional `repository` field in the `software` object.

### 5. x-nodeinfo2 (`.well-known/x-nodeinfo2`)

An alternative NodeInfo format used by some fediverse implementations.

**Endpoint:** `GET /.well-known/x-nodeinfo2`

**Response:**
```json
{
  "version": "1.0",
  "server": {
    "baseUrl": "https://your-instance.com",
    "name": "Your Instance Name",
    "software": "broca",
    "version": "1.0.0"
  },
  "protocols": ["activitypub"],
  "services": {
    "outbound": [],
    "inbound": []
  },
  "openRegistrations": false,
  "usage": {
    "users": {
      "total": 1,
      "activeMonth": 1,
      "activeHalfyear": 1
    },
    "localPosts": 0,
    "localComments": 0
  },
  "metadata": {
    "nodeName": "Your Instance Name",
    "nodeDescription": "Your instance description"
  }
}
```

### 6. host-meta (`.well-known/host-meta`)

Provides WebFinger discovery template in XRD format.

**Endpoint:** `GET /.well-known/host-meta`

**Response:** XRD/XML format
```xml
<?xml version="1.0" encoding="UTF-8"?>
<XRD xmlns="http://docs.oasis-open.org/ns/xri/xrd-1.0">
  <Link rel="lrdd" template="https://your-instance.com/.well-known/webfinger?resource={uri}"/>
</XRD>
```

### 7. host-meta.json (`.well-known/host-meta.json`)

Provides WebFinger discovery template in JRD format.

**Endpoint:** `GET /.well-known/host-meta.json`

**Response:**
```json
{
  "links": [
    {
      "rel": "lrdd",
      "template": "https://your-instance.com/.well-known/webfinger?resource={uri}"
    }
  ]
}
```

## Configuration

You can configure instance metadata in your `appsettings.json`:

```json
{
  "ActivityPub": {
    "BaseUrl": "https://your-instance.com",
    "PrimaryDomain": "your-instance.com",
    "ServerName": "Your Server Name",
    "ServerDescription": "A description of your instance"
  }
}
```

### Configuration Options

- **BaseUrl**: The base URL of your instance (required)
- **PrimaryDomain**: The primary domain for your instance (required)
- **ServerName**: Display name for your server and instance name in NodeInfo metadata
- **ServerDescription**: Description of your instance shown in NodeInfo metadata

## Implementation Details

### Services

- **NodeInfoService**: Handles generation of NodeInfo documents and instance statistics
- **WebFingerService**: Existing service for WebFinger protocol

### Controllers

- **NodeInfoController**: Exposes NodeInfo endpoints
- **HostMetaController**: Exposes host-meta endpoints
- **XNodeInfo2Controller**: Exposes x-nodeinfo2 endpoint
- **WebFingerController**: Existing controller for WebFinger

### Statistics

Currently, the NodeInfo endpoints return placeholder statistics:
- Total users: 1
- Active users (month): 1
- Active users (half-year): 1
- Local posts: 0

**TODO**: The implementation includes a TODO to add counting methods to `IActorRepository` and `IActivityRepository` to provide accurate statistics.

## Why These Endpoints Matter

1. **Discovery**: Other fediverse instances use these endpoints to discover information about your instance
2. **Federation**: Proper implementation ensures better interoperability with other ActivityPub software
3. **Network Graphs**: Many fediverse tools and monitoring services rely on NodeInfo for network statistics
4. **Client Support**: Some clients use these endpoints to determine instance capabilities

## Testing

You can test these endpoints using curl:

```bash
# NodeInfo discovery
curl https://your-instance.com/.well-known/nodeinfo

# NodeInfo 2.0
curl https://your-instance.com/nodeinfo/2.0

# host-meta
curl https://your-instance.com/.well-known/host-meta

# x-nodeinfo2
curl https://your-instance.com/.well-known/x-nodeinfo2
```

## References

- [NodeInfo Specification](https://nodeinfo.diaspora.software/)
- [WebFinger RFC 7033](https://tools.ietf.org/html/rfc7033)
- [ActivityPub Specification](https://www.w3.org/TR/activitypub/)
