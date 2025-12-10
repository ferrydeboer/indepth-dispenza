# InDepthDispenza

[![.NET CI](https://github.com/ferrydeboer/indepth-dispenza/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ferrydeboer/indepth-dispenza/actions/workflows/dotnet.yml)

Azure Functions app for scanning the Joe Dispenza Testimonials playlists and running AI-based analysis for data extraction and browsing.

## Features

- **Playlist Scanning**: Fetch video metadata from YouTube playlists
- **Transcript Analysis** _(coming soon)_: Extract healing journey data using LLM analysis
- **Data Visualization** _(coming soon)_: Browse and visualize testimonial patterns

See [docs/features/video-analysis/transcript-analysis.md](docs/features/video-analysis/transcript-analysis.md) for the full feature roadmap.

---

## Local Development Setup

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for local infrastructure)
- [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local) (optional, for local function debugging)

### 1. Start Local Infrastructure

Run the following from the **repository root**:

```bash
docker-compose up -d
```

This starts:
- **Cosmos DB Emulator** on `https://localhost:8081`
- **Azurite** (Azure Storage Emulator) on ports `10000-10002`

Verify services are running:
```bash
docker-compose ps
```

Access Cosmos DB Data Explorer: `https://localhost:8081/_explorer/index.html`

> **Note**: The Cosmos DB emulator uses a self-signed certificate. Your browser will show a security warning - this is expected for local development.

### 2. Configure Backend

Copy the example settings file:
```bash
cd backend/InDepthDispenza.Functions
cp local.settings.json.example local.settings.json
```

Edit `local.settings.json` and add your API keys:
- **YouTube API Key**: Get from [Google Cloud Console](https://console.cloud.google.com/)
- **Azure OpenAI** _(optional, for future transcript analysis)_: Your Azure OpenAI endpoint and key

### 3. Build and Test

From the `backend/` directory:

```bash
dotnet restore
dotnet build
dotnet test
```

### 4. Run Azure Functions Locally

From `backend/InDepthDispenza.Functions/`:

```bash
func start
```

Or run from your IDE (Visual Studio, Rider, VS Code).

The function will be available at: `http://localhost:7071`

Test the ScanPlaylist endpoint:
```bash
curl "http://localhost:7071/api/ScanPlaylist?playlistId=PLootDQRi8UqSU_-3hXvbR5bHARxBNPVBx&limit=5"
```

### 5. Stop Local Infrastructure

```bash
docker-compose down
```

To also remove volumes (data persistence):
```bash
docker-compose down -v
```

---

## Project Structure

```
indepth-dispenza/
├── backend/                    # .NET Azure Functions
│   ├── InDepthDispenza.Functions/
│   ├── IndepthDispenza.Tests/
│   └── InDepthDispenza.IntegrationTests/
├── frontend/                   # Future: Web UI
├── docs/                       # Feature documentation
├── infrastructure/             # Future: Bicep templates for Azure deployment
├── docker-compose.yml          # Local development infrastructure
└── .github/workflows/          # CI/CD pipelines
```

---

## Deployment

Infrastructure and deployment automation is planned for Story 0 of the Transcript Analysis feature.

For now, deploy manually:
1. Deploy `backend/InDepthDispenza.Functions/` to Azure Functions
2. Configure Application Settings with production connection strings

---

## Contributing

1. Create a feature branch from `main`
2. Make your changes with tests
3. Ensure all tests pass: `dotnet test`
4. Submit a pull request

See [docs/features/](docs/features/) for planned features and implementation details.
