# Key Generation for Tests

## Overview

The test suite now generates fresh RSA key pairs dynamically for each test run, rather than using hardcoded test keys. This provides:

- **Better Security Testing**: Each test run uses unique keys, similar to production scenarios
- **Realistic Testing**: Keys are generated the same way they would be in production
- **No Hardcoded Secrets**: Even test keys are generated on-the-fly

## Implementation Details

### Key Components

1. **`CryptographyService`** - Ported from Rayven, handles RSA key generation and PEM export
2. **`KeyGenerator`** - Simple helper for generating key pairs in tests
3. **`CreateAuthenticatedTestClient`** - Updated to auto-generate keys when not provided

### Usage in Tests

#### Automatic Key Generation (Recommended)

The simplest approach - let the framework generate keys automatically:

```csharp
// Keys are generated automatically
var (client, privateKeyPem) = TestDataSeeder.CreateAuthenticatedTestClient(
    () => _factory.CreateClient(),
    "https://localhost:7001/users/alice"
);
```

The tuple destructuring gives you:
- `client` - The authenticated client ready to use
- `privateKeyPem` - The generated private key (useful if you need it for verification)

If you don't need the private key, use tuple discard:

```csharp
var (client, _) = TestDataSeeder.CreateAuthenticatedTestClient(
    () => _factory.CreateClient(),
    "https://localhost:7001/users/alice"
);
```

#### Manual Key Generation

If you need more control, generate keys explicitly:

```csharp
// Generate a full key pair
var (privateKey, publicKey) = KeyGenerator.GenerateKeyPair();

// Or just generate a private key
var privateKey = KeyGenerator.GeneratePrivateKey();

// Use the generated key
var (client, _) = TestDataSeeder.CreateAuthenticatedTestClient(
    () => _factory.CreateClient(),
    "https://localhost:7001/users/alice",
    privateKeyPem: privateKey
);
```

#### Custom Key Sizes

For specialized testing scenarios:

```csharp
// Generate 4096-bit keys for testing stronger encryption
var (privateKey, publicKey) = KeyGenerator.GenerateKeyPair(keySize: 4096);
```

## Migration from Old Code

### Before (Hardcoded Keys)

```csharp
var client = TestDataSeeder.CreateAuthenticatedTestClient(
    () => _factory.CreateClient(),
    "https://localhost:7001/users/alice",
    TestConstants.TestPrivateKey,
    "https://localhost:7001/users/alice#main-key"
);
```

### After (Auto-Generated Keys)

```csharp
var (client, _) = TestDataSeeder.CreateAuthenticatedTestClient(
    () => _factory.CreateClient(),
    "https://localhost:7001/users/alice"
);
```

Note: The return type changed from `IActivityPubClient` to `(IActivityPubClient client, string privateKeyPem)`.

## Key Generation Details

Keys are generated using:
- **Algorithm**: RSA
- **Key Size**: 2048 bits (default, configurable)
- **Format**: PEM (PKCS#8 for private keys)
- **Provider**: `RSACryptoServiceProvider` with non-persistent CSP

Each key pair is unique and not stored anywhere after the test completes.

## Multi-Server Federation Testing

The test suite includes comprehensive multi-server federation tests that verify Broca instances can communicate with each other.

### Overview

Multi-server tests simulate real-world scenarios where two separate servers (localhost-a and localhost-b) communicate using the ActivityPub protocol. This validates that:
- Broca instances can federate with each other
- Activities are correctly delivered across servers
- Each server maintains independent state
- Cross-server interactions (likes, shares) work properly

### Test Infrastructure

**ConfigurableBrocaServerFactory.cs**
A configurable WebApplicationFactory that allows creating multiple server instances with different base URLs for federation testing.

Key Features:
- Accepts custom base URLs (e.g., "https://localhost-a:7001", "https://localhost-b:7002")
- Each instance has its own in-memory repositories (simulating separate databases)
- Supports friendly instance naming for debugging

### Test Scenarios

#### Test 1: MultiServer_ActorOnServerA_LikesNoteOnServerB
Verifies that an actor on Server A (Alice) can like a note created by an actor on Server B (Bob).

**Flow:**
1. Bob creates a note on Server B
2. Alice (from Server A) likes Bob's note
3. The Like activity is delivered to Bob's inbox on Server B
4. Server B correctly stores and indexes the like

#### Test 2: MultiServer_ActorOnServerB_SharesNoteOnServerA
Verifies bidirectional federation - an actor on Server B (Bob) sharing content from Server A (Alice).

**Flow:**
1. Alice creates a note on Server A
2. Bob (from Server B) shares Alice's note
3. The Announce activity is delivered to Alice's inbox on Server A
4. Server A correctly stores and indexes the share

#### Test 3: MultiServer_BidirectionalInteraction_BothServersReceiveActivities
Tests complex scenarios where actors on both servers interact with content on both servers simultaneously.

**Flow:**
1. Alice creates a note on Server A, Bob creates a note on Server B
2. Alice likes Bob's note (cross-server interaction)
3. Bob shares Alice's note (cross-server interaction)
4. Both servers correctly receive and process the activities

#### Test 4: MultiServer_MultipleActorsOnDifferentServers_AllInteractionsTracked
Verifies that multiple actors from different servers can all interact with the same content.

**Flow:**
1. Alice creates a note on Server A
2. Bob (from Server B) likes the note
3. Charlie (from Server A) likes the note
4. Both likes are correctly tracked with proper attribution

#### Test 5: MultiServer_VerifyInstanceIsolation_RepositoriesAreIndependent
Confirms that the two server instances maintain separate state.

**Flow:**
1. Create actors on both servers
2. Verify that data stored on Server A doesn't appear on Server B
3. Verify that data stored on Server B doesn't appear on Server A

### Technical Details

**In-Memory Test Server Routing**
Since WebApplicationFactory creates in-memory test servers (not real HTTP servers), cross-server requests require special handling:

- When posting to Server A, use Server A's HttpClient
- When posting to Server B, use Server B's HttpClient
- The ActivityPubClient is configured with the appropriate HttpClient factory

Example:
```csharp
// Alice (on Server A) posting to Server B
var (aliceClient, _) = TestDataSeeder.CreateAuthenticatedTestClient(
    () => _serverB.CreateClient(), // Use Server B's client to route to Server B
    aliceActorId); // Alice's identity from Server A
```

**Actor Identity vs. Server Routing**
The tests demonstrate an important distinction:
- **Actor Identity**: Where the actor claims to be from (e.g., `https://localhost-a:7001/users/alice`)
- **HTTP Routing**: Which test server's HttpClient is used (determines where the request actually goes)

This allows us to simulate an actor from Server A making a request to Server B.

### Running Multi-Server Tests

```bash
# Run all multi-server federation tests
dotnet test --filter "FullyQualifiedName~MultiServerFederationTests"

# Run a specific test
dotnet test --filter "FullyQualifiedName~MultiServer_ActorOnServerA_LikesNoteOnServerB"
```

### Test Results

All tests pass successfully, confirming that:
- ✅ Broca instances can communicate with each other
- ✅ Activities are correctly delivered across servers
- ✅ Like and Share activities work in multi-server scenarios
- ✅ Each server maintains independent state
- ✅ Multiple actors from different servers can interact with the same content
