#!/bin/bash
set -e

echo "Building AlufranFinConsole API..."

# Restore dependencies
dotnet restore

# Publish the API project
dotnet publish -c Release -o ./out AlufranFinConsole.Api/AlufranFinConsole.Api.csproj

echo "Build completed successfully!"
