# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["Broca.ActivityPub.sln", "./"]
COPY ["src/Broca.Web/Broca.Web.csproj", "src/Broca.Web/"]
COPY ["src/Broca.ActivityPub.Components/Broca.ActivityPub.Components.csproj", "src/Broca.ActivityPub.Components/"]
COPY ["src/Broca.ActivityPub.Client/Broca.ActivityPub.Client.csproj", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Core/Broca.ActivityPub.Core.csproj", "src/Broca.ActivityPub.Core/"]

# Restore dependencies
RUN dotnet restore "src/Broca.Web/Broca.Web.csproj"

# Copy source code
COPY ["src/Broca.Web/", "src/Broca.Web/"]
COPY ["src/Broca.ActivityPub.Components/", "src/Broca.ActivityPub.Components/"]
COPY ["src/Broca.ActivityPub.Client/", "src/Broca.ActivityPub.Client/"]
COPY ["src/Broca.ActivityPub.Core/", "src/Broca.ActivityPub.Core/"]

# Build and publish
WORKDIR "/src/src/Broca.Web"
RUN dotnet publish "Broca.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - serve static files with nginx
FROM nginx:alpine AS final

# Copy static files to nginx html directory
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html

# Copy nginx config for SPA routing
COPY src/Broca.Web/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
