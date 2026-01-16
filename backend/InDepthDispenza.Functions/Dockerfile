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

# Final stage - use the .NET 9 SDK image and manually install Azure Functions Core Tools
FROM mcr.microsoft.com/dotnet/sdk:9.0
WORKDIR /home/site/wwwroot

# Install Azure Functions Core Tools
RUN apt-get update && \
    apt-get install -y wget && \
    wget -q https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y azure-functions-core-tools-4 && \
    rm -rf /var/lib/apt/lists/*

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    ASPNETCORE_URLS=http://+:80

COPY --from=publish /app/publish .

EXPOSE 80

CMD ["func", "start", "--port", "80"]
