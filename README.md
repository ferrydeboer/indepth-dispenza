# InDepthDispenza

[![.NET CI](https://github.com/ferrydeboer/indepth-dispenza/actions/workflows/dotnet.yml/badge.svg)](https://github.com/ferrydeboer/indepth-dispenza/actions/workflows/dotnet.yml)

Azure Functions app for scanning the Joe Dispenza Testimonials playlists (e.g., ScanPlaylist endpoint)
and run AI based analysis for data extraction and browsing.

## Setup
1. Clone the repo.
2. `dotnet restore`
3. `dotnet build`
4. `dotnet test` (runs in `backend/IndepthDispenza.Tests/`)

## Deploy
Deploy `backend/InDepthDispenza.Functions/` to Azure Functions.
