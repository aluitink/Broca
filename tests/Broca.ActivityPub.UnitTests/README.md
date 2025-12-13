# Broca.ActivityPub.UnitTests

Unit tests for Broca ActivityPub library components.

## Purpose

This project contains **unit tests** that test individual service classes and components in isolation, without the HTTP layer or WebApplicationFactory overhead.

## Distinction from Integration Tests

- **Unit Tests (this project)**: Test service methods directly through interfaces. Fast, focused tests.
- **Integration Tests**: Test full HTTP request/response cycles using in-memory test servers. Validate end-to-end behavior.

## Test Organization

Tests are organized by the service or component they test:

- `BlobStorageServiceTests` - Tests for `IBlobStorageService` implementations

## Running Tests

```bash
# Run all unit tests
dotnet test tests/Broca.ActivityPub.UnitTests/Broca.ActivityPub.UnitTests.csproj

# Run specific test class
dotnet test --filter "FullyQualifiedName~BlobStorageServiceTests"
```

## Dependencies

Unit tests have minimal dependencies:
- xUnit testing framework
- Moq for mocking (when needed)
- Direct references to Core and Persistence projects
- No ASP.NET Core hosting dependencies
