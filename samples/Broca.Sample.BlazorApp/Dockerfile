# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Broca.ActivityPub.sln", "./"]
COPY ["samples/Broca.Sample.BlazorApp/Broca.Sample.BlazorApp.csproj", "samples/Broca.Sample.BlazorApp/"]
COPY ["src/Broca.ActivityPub.Core/Broca.ActivityPub.Core.csproj", "src/Broca.ActivityPub.Core/"]
COPY ["src/Broca.ActivityPub.Client.WebCrypto/Broca.ActivityPub.Client.WebCrypto.csproj", "src/Broca.ActivityPub.Client.WebCrypto/"]
COPY ["src/Broca.ActivityPub.Client/Broca.ActivityPub.Client.csproj", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Components/Broca.ActivityPub.Components.csproj", "src/Broca.ActivityPub.Components/"]

# Restore dependencies
RUN dotnet restore "samples/Broca.Sample.BlazorApp/Broca.Sample.BlazorApp.csproj"

# Copy source code
COPY ["samples/Broca.Sample.BlazorApp/", "samples/Broca.Sample.BlazorApp/"]
COPY ["src/Broca.ActivityPub.Core/", "src/Broca.ActivityPub.Core/"]
COPY ["src/Broca.ActivityPub.Client.WebCrypto/", "src/Broca.ActivityPub.Client.WebCrypto/"]
COPY ["src/Broca.ActivityPub.Client/", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Components/", "src/Broca.ActivityPub.Components/"]

# Build and publish
WORKDIR "/src/samples/Broca.Sample.BlazorApp"
RUN dotnet publish "Broca.Sample.BlazorApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - use nginx to serve static files
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Copy published WASM files
COPY --from=build /app/publish/wwwroot .

# Copy custom nginx config
COPY samples/Broca.Sample.BlazorApp/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

