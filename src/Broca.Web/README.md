# Broca Web Client

A modern, decentralized ActivityPub web client built with Blazor WebAssembly.

## Features

- 🔐 **API Key Authentication** - Development-friendly authentication using API keys (OAuth support coming soon)
- 📬 **Inbox Management** - View and manage incoming activities
- 📤 **Outbox Tracking** - Monitor activities you've sent
- 👥 **Collection Management** - Manage followers and following
- 👤 **Profile Management** - View and manage your actor profile
- 🔄 **CORS Fallback** - Automatic proxy fallback for CORS-restricted requests
- 🔏 **HTTP Signatures** - All authenticated requests are cryptographically signed
- 🎨 **Fluent UI** - Modern, accessible interface using Microsoft Fluent UI

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- A running Broca ActivityPub Server instance

### Running the Web Client

1. **Configure the API Base URL** (optional)
   
   Edit `appsettings.json` to point to your server:
   ```json
   {
     "ApiBaseUrl": "https://your-server.com"
   }
   ```

2. **Run the application**
   ```bash
   dotnet run --project src/Broca.Web
   ```

3. **Access the web client**
   
   Open your browser to `https://localhost:5001` (or the configured port)

### First-Time Login

1. Navigate to the **Login** page
2. Enter your **Actor ID** (e.g., `https://your-server.com/users/yourusername`)
3. Enter your **API Key** (obtained from your server administrator)
4. Click **Login**

The client will:
- Authenticate with the server using your API key
- Fetch your actor profile including the private key
- Sign all future requests with your private key
- Store your credentials in local storage for persistence

### Using the Client

#### Inbox
View activities sent to you from other actors across the Fediverse.

- Navigate to **Inbox** in the navigation menu
- Activities are loaded dynamically as you scroll
- Click on activities to view details

#### Outbox
Monitor activities you have sent.

- Navigate to **Outbox** in the navigation menu
- See all activities you've posted
- Track delivery status (future feature)

#### Collections
Manage your social connections.

- Navigate to **Collections** in the navigation menu
- Switch between **Followers** and **Following** tabs
- View actor profiles for each connection

#### Profile
View and manage your account.

- Navigate to **Profile** in the navigation menu
- See your complete actor information
- Logout when needed

## Architecture

### Authentication Flow

```
1. User enters Actor ID + API Key
2. Client sends GET request to Actor ID with Authorization: Bearer {apiKey}
3. Server responds with actor profile + privateKeyPem (if API key is valid)
4. Client stores private key and uses it to sign all subsequent requests
5. Credentials are persisted in browser local storage
```

### Request Signing

All authenticated requests include HTTP Signatures:
- Requests to your inbox/outbox are signed with your private key
- Signature includes request method, path, host, date, and digest (for POST)
- Remote servers can verify requests using your public key from your actor profile

### CORS Fallback

When direct requests fail due to CORS:
1. Client attempts direct request to remote server
2. If CORS error occurs, falls back to server-side proxy
3. Proxy makes the request with proper signing
4. Response is relayed back to client

## Development

### Project Structure

```
src/Broca.Web/
├── Pages/              # Razor page components
│   ├── Home.razor      # Landing/dashboard page
│   ├── Login.razor     # Authentication page
│   ├── Inbox.razor     # Inbox view
│   ├── Outbox.razor    # Outbox view
│   ├── Collections.razor # Followers/following
│   └── Profile.razor   # User profile
├── Layout/             # Layout components
│   ├── MainLayout.razor
│   └── NavMenu.razor
├── Services/           # Application services
│   └── AuthenticationStateService.cs
└── Program.cs          # Application entry point
```

### Adding New Features

The client uses Broca.ActivityPub.Components for reusable UI patterns:

```razor
@using Broca.ActivityPub.Components

<ActivityFeed 
    CollectionUrl="@inboxUrl" 
    PageSize="20"
    ShowObjects="true" />
```

Available components:
- `ActivityFeed` - Display a stream of activities
- `ActorProfile` - Show an actor's profile
- `CollectionLoader` - Generic collection pagination
- `ObjectDisplay` - Render ActivityPub objects
- `ActivityPubClientProvider` - Authentication context

## Security Considerations

### Current (Development) Authentication

- API keys are stored in browser local storage
- Private keys are fetched once and cached
- No server-side sessions
- Suitable for development and testing

### Future (Production) Authentication

- OAuth 2.0 / OpenID Connect
- Token refresh mechanism
- Server-side session management
- Hardware security key support

### Best Practices

1. **Never share your API key** - Treat it like a password
2. **Use HTTPS** - Always access the web client over HTTPS
3. **Logout when done** - Clears local storage and private key
4. **Unique API keys per device** - Request separate keys for different devices

## Troubleshooting

### Login Fails

- Verify your Actor ID is correct and accessible
- Ensure your API key matches the server's `AdminApiToken` configuration
- Check server logs for authentication errors
- Confirm the server is running and accessible

### Activities Not Loading

- Check browser console for CORS errors
- Verify the proxy endpoint is accessible at `/api/proxy`
- Ensure you're authenticated (check navigation menu shows your username)
- Try refreshing the page

### Private Key Not Retrieved

The server must:
1. Have `AdminApiToken` configured in `appsettings.json`
2. Return `privateKeyPem` in actor response when valid API key is provided
3. Have the actor's private key stored in its persistence layer

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for development guidelines.

## License

This project is part of the Broca ActivityPub suite. See the root LICENSE file for details.
