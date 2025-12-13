# Contributing to Broca

Thank you for your interest in contributing to Broca! This guide will help you get started with development and understand our workflow.

## Development Workflow

We follow a **Git Flow** branching strategy:

```
feature/* → develop → main → tagged releases
```

### Branch Structure

- **`main`** - Production-ready code. Only receives merges from `develop`.
- **`develop`** - Integration branch for ongoing development. Always in a working state.
- **`feature/*`** - Feature branches for new work (e.g., `feature/add-oauth-support`).
- **`fix/*`** - Bug fix branches (e.g., `fix/signature-validation`).

### Contributing Process

1. **Fork and Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/Broca.git
   cd Broca
   ```

2. **Create a Feature Branch from Develop**
   ```bash
   git checkout develop
   git pull origin develop
   git checkout -b feature/your-feature-name
   ```

3. **Make Your Changes**
   - Write code following our conventions (see below)
   - Add tests for new functionality
   - Update documentation as needed

4. **Test Locally**
   ```bash
   # Restore and build
   dotnet restore Broca.ActivityPub.sln
   dotnet build Broca.ActivityPub.sln --configuration Release
   
   # Run all tests
   dotnet test Broca.ActivityPub.sln --configuration Release
   
   # Run integration tests specifically
   dotnet test tests/Broca.ActivityPub.IntegrationTests/Broca.ActivityPub.IntegrationTests.csproj
   ```

5. **Commit Your Changes**
   ```bash
   git add .
   git commit -m "feat: add OAuth support for client authentication"
   ```
   
   Use conventional commit messages:
   - `feat:` - New feature
   - `fix:` - Bug fix
   - `docs:` - Documentation changes
   - `test:` - Test additions/changes
   - `refactor:` - Code refactoring
   - `chore:` - Maintenance tasks

6. **Push and Create PR**
   ```bash
   git push origin feature/your-feature-name
   ```
   
   Create a Pull Request targeting the `develop` branch on GitHub.

7. **Code Review**
   - CI will automatically run tests on your PR
   - Address any feedback from reviewers
   - Once approved, your PR will be merged to `develop`

## Getting Started Locally

### Prerequisites

- .NET 9.0 SDK or later
- Git
- An IDE (Visual Studio, VS Code, or Rider recommended)

### Setup

```bash
# Clone the repository
git clone https://github.com/aluitink/Broca.git
cd Broca

# Checkout develop branch
git checkout develop

# Restore dependencies
dotnet restore Broca.ActivityPub.sln

# Build the solution
dotnet build Broca.ActivityPub.sln

# Run tests to verify setup
dotnet test Broca.ActivityPub.sln
```

### Running Samples

Try out the sample applications to understand how Broca works:

```bash
# Web API sample
cd samples/Broca.Sample.WebApi
dotnet run

# Blazor sample
cd samples/Broca.Sample.BlazorApp
dotnet run
```

See [samples/README.md](./samples/README.md) for more details.

### Running with Docker

```bash
cd samples
docker-compose up
```

See [docs/DOCKER.md](./docs/DOCKER.md) for detailed Docker setup.

## Project Structure

- **`src/`** - Main library projects
  - `Broca.ActivityPub.Core` - Core interfaces and models
  - `Broca.ActivityPub.Client` - Client library
  - `Broca.ActivityPub.Server` - Server components
  - `Broca.ActivityPub.Persistence` - Storage implementations
  - `Broca.ActivityPub.WebClient` - Blazor components
  
- **`tests/`** - Test projects
  - `Broca.ActivityPub.IntegrationTests` - Integration tests
  
- **`samples/`** - Example applications
- **`docs/`** - Comprehensive documentation

## Code Guidelines

### Code Style

- Follow standard C# coding conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and single-purpose

### Testing

- Write unit tests for new functionality
- Add integration tests for complex scenarios
- Ensure all tests pass before submitting PR
- Aim for good test coverage, especially for core functionality

### Documentation

- Update relevant documentation in `docs/` when adding features
- Add XML comments to public APIs
- Include code examples for new features
- Update README.md if adding major functionality

## Continuous Integration

Our CI pipeline runs automatically on:
- Pushes to `main` and `develop`
- Pull requests targeting `main` or `develop`

The CI workflow:
1. Restores dependencies
2. Builds the solution in Release mode
3. Runs all tests
4. Publishes test results

Your PR must pass CI checks before it can be merged.

## Release Process

Releases are created from the `main` branch:

1. Merge `develop` → `main` via PR
2. Tag the release on `main`:
   ```bash
   git checkout main
   git pull origin main
   git tag v0.2.0
   git push origin v0.2.0
   ```
3. The release workflow automatically:
   - Builds and tests
   - Packs NuGet packages
   - Publishes to NuGet.org and GitHub Packages
   - Creates a GitHub release

## Getting Help

- **Documentation**: Check the [docs](./docs) folder first
- **Issues**: Search existing [GitHub Issues](https://github.com/aluitink/Broca/issues)
- **Discussions**: Start a [GitHub Discussion](https://github.com/aluitink/Broca/discussions)

## Code of Conduct

- Be respectful and constructive
- Welcome newcomers and help them get started
- Focus on what's best for the community
- Show empathy towards other contributors

## Questions?

If you have questions about contributing, feel free to:
- Open an issue with the `question` label
- Start a discussion in GitHub Discussions
- Review existing documentation in the `docs/` folder

Thank you for contributing to Broca! 🎉
