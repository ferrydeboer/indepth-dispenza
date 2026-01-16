FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["InDepthDispenza.Functions.csproj", "./"]
RUN dotnet restore "InDepthDispenza.Functions.csproj"

# Copy everything else and build
COPY . .
RUN dotnet build "InDepthDispenza.Functions.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "InDepthDispenza.Functions.csproj" -c Release -o /app/publish

# Final stage - use the proper Azure Functions base image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated9.0
WORKDIR /home/site/wwwroot

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=publish /app/publish .
