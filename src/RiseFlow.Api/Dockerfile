## Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the API csproj from the repo root into the image and restore it explicitly.
# This assumes Railway's build context is the REPO ROOT (recommended).
COPY ["src/RiseFlow.Api/RiseFlow.Api.csproj", "src/RiseFlow.Api/"]
RUN dotnet restore "src/RiseFlow.Api/RiseFlow.Api.csproj"

# Copy the full source tree and publish the API project
COPY . .
WORKDIR /src/src/RiseFlow.Api
RUN dotnet publish "RiseFlow.Api.csproj" -c Release -o /app/publish

## Run stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Railway sets PORT at runtime; Program.cs configures UseUrls from it
EXPOSE 5000
ENTRYPOINT ["dotnet", "RiseFlow.Api.dll"]
