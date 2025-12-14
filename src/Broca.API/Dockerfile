# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Broca.ActivityPub.sln", "./"]
COPY ["src/Broca.API/Broca.API.csproj", "src/Broca.API/"]
COPY ["src/Broca.ActivityPub.Server/Broca.ActivityPub.Server.csproj", "src/Broca.ActivityPub.Server/"]
COPY ["src/Broca.ActivityPub.Client/Broca.ActivityPub.Client.csproj", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Core/Broca.ActivityPub.Core.csproj", "src/Broca.ActivityPub.Core/"]
COPY ["src/Broca.ActivityPub.Persistence/Broca.ActivityPub.Persistence.csproj", "src/Broca.ActivityPub.Persistence/"]

# Restore dependencies
RUN dotnet restore "src/Broca.API/Broca.API.csproj"

# Copy source code
COPY ["src/Broca.API/", "src/Broca.API/"]
COPY ["src/Broca.ActivityPub.Server/", "src/Broca.ActivityPub.Server/"]
COPY ["src/Broca.ActivityPub.Client/", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Core/", "src/Broca.ActivityPub.Core/"]
COPY ["src/Broca.ActivityPub.Persistence/", "src/Broca.ActivityPub.Persistence/"]

# Build and publish
WORKDIR "/src/src/Broca.API"
RUN dotnet publish "Broca.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Broca.API.dll"]
