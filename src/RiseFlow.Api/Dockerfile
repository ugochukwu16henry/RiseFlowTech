## Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# IMPORTANT: Railway is using src/RiseFlow.Api as the build context for this Dockerfile.
# So the csproj is directly in the context root, not under src/RiseFlow.Api again.

# Copy the API csproj and restore it explicitly.
COPY RiseFlow.Api.csproj ./
RUN dotnet restore "RiseFlow.Api.csproj"

# Copy the rest of the project and publish
COPY . .
RUN dotnet publish "RiseFlow.Api.csproj" -c Release -o /app/publish

## Run stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Railway sets PORT at runtime; Program.cs configures UseUrls from it
EXPOSE 5000
ENTRYPOINT ["dotnet", "RiseFlow.Api.dll"]
