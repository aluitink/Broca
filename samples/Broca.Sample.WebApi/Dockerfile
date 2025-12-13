# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["samples/Broca.Sample.WebApi/Broca.Sample.WebApi.csproj", "samples/Broca.Sample.WebApi/"]
COPY ["src/Broca.ActivityPub.Server/Broca.ActivityPub.Server.csproj", "src/Broca.ActivityPub.Server/"]
COPY ["src/Broca.ActivityPub.Client/Broca.ActivityPub.Client.csproj", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Core/Broca.ActivityPub.Core.csproj", "src/Broca.ActivityPub.Core/"]
COPY ["src/Broca.ActivityPub.Persistence/Broca.ActivityPub.Persistence.csproj", "src/Broca.ActivityPub.Persistence/"]

# Restore dependencies
RUN dotnet restore "samples/Broca.Sample.WebApi/Broca.Sample.WebApi.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/samples/Broca.Sample.WebApi"
RUN dotnet build "Broca.Sample.WebApi.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "Broca.Sample.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Broca.Sample.WebApi.dll"]
