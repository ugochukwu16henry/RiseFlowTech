# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Run stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Railway sets PORT at runtime; Program.cs configures UseUrls from it
EXPOSE 5000
ENTRYPOINT ["dotnet", "RiseFlow.Api.dll"]
