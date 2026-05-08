# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["AlufranFinConsole.Api/AlufranFinConsole.Api.csproj", "AlufranFinConsole.Api/"]
COPY ["AlufranFinConsole.Domain/AlufranFinConsole.Domain.csproj", "AlufranFinConsole.Domain/"]
COPY ["AlufranFinConsole.Application/AlufranFinConsole.Application.csproj", "AlufranFinConsole.Application/"]
COPY ["AlufranFinConsole.Infrastructure/AlufranFinConsole.Infrastructure.csproj", "AlufranFinConsole.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "AlufranFinConsole.Api/AlufranFinConsole.Api.csproj"

# Copy all source code
COPY . .

# Build the project
RUN dotnet publish "AlufranFinConsole.Api/AlufranFinConsole.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Create directory for SQLite database
RUN mkdir -p /var/data

# Expose port
EXPOSE 10000

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000

# Start the application
ENTRYPOINT ["dotnet", "AlufranFinConsole.Api.dll"]
