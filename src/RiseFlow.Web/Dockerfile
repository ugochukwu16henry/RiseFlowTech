## Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the Blazor Web project file and restore it explicitly.
COPY RiseFlow.Web.csproj ./
RUN dotnet restore "RiseFlow.Web.csproj"

# Copy the rest of the project and publish
COPY . .
RUN dotnet publish "RiseFlow.Web.csproj" -c Release -o /app/publish

## Run stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway injects PORT; Program.cs already binds to it via UseUrls.
EXPOSE 5000

ENTRYPOINT ["dotnet", "RiseFlow.Web.dll"]

