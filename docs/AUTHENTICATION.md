# ActivityPub Client Authentication

This document explains how to configure authentication for the ActivityPub client components.

## Overview

The `ActivityPubClientProvider` component supports three modes of operation:

1. **Anonymous Mode** - Browse public ActivityPub content without authentication
2. **API Key Authentication** - Securely authenticate using an API key
3. **Private Key Authentication** - Advanced authentication with direct private key management

## Component Library Usage

### Anonymous Mode (Default)

Simply wrap your components with the provider:

```razor
<ActivityPubClientProvider>
    <ActorBrowser />
    <ActivityFeed />
</ActivityPubClientProvider>
```

### API Key Authentication

Pass credentials via parameters:

```razor
<ActivityPubClientProvider 
    ActorId="https://mastodon.social/@username"
    ApiKey="your-api-key">
    
    <ActorProfile ActorId="@context.CurrentActorId" />
    <ActivityFeed />
</ActivityPubClientProvider>
```

### Private Key Authentication

For advanced scenarios where you manage your own keys:

```razor
<ActivityPubClientProvider 
    ActorId="https://mastodon.social/@username"
    PrivateKeyPem="-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----"
    PublicKeyId="https://mastodon.social/@username#main-key">
    
    <ActorProfile ActorId="@context.CurrentActorId" />
</ActivityPubClientProvider>
```

### Programmatic Configuration

Get a reference to the provider and configure it dynamically:

```razor
<ActivityPubClientProvider @ref="provider">
    @if (provider.IsAuthenticated)
    {
        <p>Signed in as @provider.CurrentActorId</p>
    }
    else
    {
        <p>Anonymous mode</p>
    }
</ActivityPubClientProvider>

@code {
    private ActivityPubClientProvider? provider;

    private async Task SignIn(string actorId, string apiKey)
    {
        if (provider != null)
        {
            await provider.SetApiKeyAsync(actorId, apiKey);
        }
    }

    private void SignOut()
    {
        provider?.ClearAuthentication();
    }
}
```

## Properties & Methods

### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `ActorId` | `string?` | The ActivityPub actor ID for authenticated requests |
| `ApiKey` | `string?` | API key for fetching credentials from the server |
| `PrivateKeyPem` | `string?` | PEM-encoded private key for direct authentication |
| `PublicKeyId` | `string?` | Public key ID (required with `PrivateKeyPem`) |
| `OnClientReady` | `EventCallback<IActivityPubClient>` | Callback when client is initialized |
| `ChildContent` | `RenderFragment?` | Child components to render |

### Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `Client` | `IActivityPubClient` | The underlying ActivityPub client instance |
| `IsReady` | `bool` | Whether the client has finished initializing |
| `IsAuthenticated` | `bool` | Whether the client is in authenticated mode |
| `CurrentActorId` | `string?` | The current authenticated actor ID |

### Public Methods

| Method | Parameters | Description |
|--------|------------|-------------|
| `SetApiKeyAsync` | `actorId, apiKey` | Configure with API key authentication |
| `SetPrivateKeyAsync` | `actorId, privateKeyPem, publicKeyId` | Configure with private key authentication |
| `ClearAuthentication` | - | Switch back to anonymous mode |

## Web Application Example

The `Broca.Web` project includes a Settings page demonstrating how to build a UI for credential configuration:

**File:** [src/Broca.Web/Pages/Settings.razor](../Broca.Web/Pages/Settings.razor)

This page shows:
- Tabbed interface for API key vs. private key authentication
- Form validation and error handling
- Switching between anonymous and authenticated modes
- Using Fluent UI components for a polished interface

### Key Features

- **Tab-based authentication**: Users can choose between API key and private key methods
- **Real-time validation**: Input validation before attempting authentication
- **Error handling**: Clear error messages for failed authentication attempts
- **Sign out**: Easy way to return to anonymous mode
- **Sensitive data handling**: Clears password/key fields after use

## Authentication Flow

### API Key Flow

1. User provides Actor ID and API Key
2. Provider updates `ActivityPubClientOptions` with these credentials
3. Provider calls `Client.InitializeAsync()`
4. Client fetches actor profile from server using API key as Bearer token
5. Server returns actor data including private key (if authorized)
6. Client is now authenticated and can sign requests

### Private Key Flow

1. User provides Actor ID, Private Key (PEM), and Public Key ID
2. Provider updates `ActivityPubClientOptions` directly
3. Client is immediately authenticated (no server fetch needed)
4. Client can sign requests using the provided private key

### Anonymous Flow

1. No credentials provided
2. Client operates in read-only mode
3. Can fetch public ActivityPub content
4. Cannot perform authenticated actions

## Security Considerations

### API Keys

- Store API keys securely (never in source code)
- Use environment variables or secure configuration
- API keys are sent over HTTPS to fetch credentials
- Keys should have appropriate expiration and scope

### Private Keys

- Never expose private keys in client-side code
- Consider using browser local storage with encryption
- Private keys remain client-side (not sent to server)
- Ideal for progressive web apps with offline support

### Anonymous Mode

- Perfect for read-only browsing
- No authentication overhead
- Suitable for public content aggregation
- Some features may be unavailable

## Component Consumers

If you're building your own Blazor app using `Broca.ActivityPub.Components`:

1. **Add the provider at the root** of your layout or app
2. **Choose authentication mode** based on your requirements
3. **Build your own UI** for credential entry (or use anonymous mode)
4. **Use cascading parameters** to access the provider in child components

The components are designed to be flexible - you control the authentication UX while the components handle the plumbing.
