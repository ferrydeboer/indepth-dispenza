# Story 0: Infrastructure Setup

**As a** developer
**I want** local development infrastructure and Azure deployment automation
**So that** I can develop locally and deploy to production reliably

## Technical Details

### Repository Structure
```
indepth-dispenza/                    # Repository root
├── backend/                         # Azure Functions (.NET)
│   ├── InDepthDispenza.Functions/
│   ├── InDepthDispenza.Tests/
│   └── InDepthDispenza.IntegrationTests/
├── frontend/                        # Future: Web UI (Phase 3)
├── docs/                           # Documentation
├── infrastructure/                 # Bicep templates (all Azure resources)
│   ├── main.bicep
│   ├── modules/
│   └── parameters/
├── docker-compose.yml              # Local development environment
└── .github/workflows/              # CI/CD pipelines
```

### Local Development (docker-compose)
Create `docker-compose.yml` at **repository root** with:
- **Cosmos DB Emulator** (Azure CosmosDB Linux Emulator)
- **Azurite** (Azure Storage emulator - already used in tests)
- Services should be accessible from both backend and future frontend directories

### Infrastructure as Code
Create Azure Bicep templates in **`infrastructure/`** directory at repository root:
- `main.bicep` - Main orchestration file
- `modules/`
  - `cosmos-db.bicep` - Cosmos DB account with serverless mode
    - Collections: `transcript-cache`, `video-analysis`
    - Partition keys, indexes
  - `storage.bicep` - Azure Storage account for queue storage
  - `function-app.bicep` - Azure Functions backend (consumption plan)
  - `app-insights.bicep` - Application Insights for monitoring
  - `static-web-app.bicep` - (Future: Phase 3) Static Web App for frontend
- `parameters/`
  - `dev.parameters.json` - Development environment parameters
  - `prod.parameters.json` - Production environment parameters

### CI/CD Pipeline
Extend `.github/workflows/dotnet.yml` or create separate `deploy.yml`:
1. Build and test backend (existing)
2. **Deploy infrastructure** (new) - `az deployment sub create --location westeurope --template-file infrastructure/main.bicep`
3. **Deploy functions** (new) - Azure Functions deployment from `backend/`
4. Future: Frontend deployment to Static Web App (Phase 3)

## Acceptance Criteria

### Local Development
- [x] `docker-compose.yml` file at **repository root**
- [x] Cosmos DB emulator accessible at `https://localhost:8081`
- [x] Azurite runs with queues on port 10001, accessible from backend/frontend
- [x] `backend/InDepthDispenza.Functions/local.settings.json.example` with connection strings
- [x] Root-level `README.md` with instructions for: `docker-compose up -d`
- [ ] Integration tests in `backend/` can connect to docker-compose services
- [ ] Environment variables documented for both backend and future frontend

### Infrastructure as Code
- [x] Bicep templates in `infrastructure/` directory at repository root
- [x] All templates validate: `az bicep build --file infrastructure/main.bicep`
- [x] Parameter files for dev/prod in `infrastructure/parameters/`
- [ ] Cosmos DB serverless mode configured
- [ ] Storage account with queue enabled
- [ ] Function App with consumption plan (Linux)
- [ ] Application Insights connected to Function App
- [ ] Outputs include connection strings for CI/CD
- [ ] Infrastructure prepared for future Static Web App (placeholder in modules)

### CI/CD Pipeline
- [ ] Workflow runs on push to `main` branch
- [ ] Infrastructure deployment step: `az deployment sub create --template-file infrastructure/main.bicep`
- [ ] Function App deployment using Azure Functions action (deploys from `backend/`)
- [ ] Secrets stored in GitHub secrets:
  - `AZURE_CREDENTIALS` (service principal)
  - `AZURE_SUBSCRIPTION_ID`
  - `AZURE_OPENAI_KEY` (for Story 2)
- [ ] Deployment only on successful tests
- [ ] Deployment working directory properly set for backend vs infrastructure
- [ ] Manual approval for production deployment (optional)

## Cost Considerations
- **Cosmos DB Serverless**: $0.25/million requests (~$5-10/month)
- **Azure Functions Consumption**: First 1M executions free, then $0.20/million
- **Storage Account**: ~$0.01/month for queue storage
- **Application Insights**: 5GB free/month
- **Total estimated**: <$10/month for development workload

## Dependencies
- Existing GitHub workflow: `.github/workflows/dotnet.yml`
- Existing Testcontainers setup in `InDepthDispenza.IntegrationTests`

## Enables
- [Story 1: Transcript Retrieval & Storage](story-1-transcript-retrieval.md)
- [Story 2: LLM Analysis Pipeline](story-2-llm-analysis.md)
- All future development work

## Technical Notes

### Why Cosmos DB Emulator vs Azurite?
Cosmos DB emulator provides exact parity with Azure Cosmos DB, unlike Azurite which only emulates blob/queue/table storage. This ensures:
- Query testing with real indexes
- Partition key behavior validation
- Document schema validation

### Bicep vs Terraform
Using Bicep because:
- Native Azure support (no state management needed)
- Better Azure resource support
- Simpler syntax for Azure-only deployments

### Alternative: Skip docker-compose for local dev?
**No** - Integration tests already use Testcontainers, but developers need a persistent environment for:
- Manual testing with Azure Functions Core Tools
- Debugging with real data
- UI development (Phase 3)
