# Story 0: Infrastructure Setup

**As a** developer
**I want** local development infrastructure and Azure deployment automation
**So that** I can develop locally and deploy to production reliably

## Technical Details

### Local Development (docker-compose)
Create `backend/docker-compose.yml` with:
- **Cosmos DB Emulator** (or Azure CosmosDB Linux Emulator)
- **Azurite** (Azure Storage emulator - already used in tests)
- **Azure Functions runtime** for local debugging

### Infrastructure as Code
Create Azure Bicep templates in `backend/infrastructure/`:
- `main.bicep` - Main orchestration file
- `cosmos-db.bicep` - Cosmos DB account with serverless mode
  - Collections: `transcript-cache`, `video-analysis`
  - Partition keys, indexes
- `storage.bicep` - Azure Storage account for queue storage
- `function-app.bicep` - Azure Functions (consumption plan)
- `app-insights.bicep` - Application Insights for monitoring

### CI/CD Pipeline
Extend `.github/workflows/dotnet.yml` to:
1. Build and test (existing)
2. **Deploy infrastructure** (new) - Bicep → Azure
3. **Deploy functions** (new) - Azure Functions deployment

## Acceptance Criteria

### Local Development
- [ ] `docker-compose.yml` file in `backend/` directory
- [ ] Cosmos DB emulator accessible at `https://localhost:8081`
- [ ] Azurite runs with queues on port 10001
- [ ] `local.settings.json.example` template with connection strings
- [ ] README documentation for running: `docker-compose up -d`
- [ ] Integration tests can run against docker-compose environment

### Infrastructure as Code
- [ ] Bicep templates validate: `az bicep build`
- [ ] Parameters file for dev/prod environments
- [ ] Cosmos DB serverless mode configured
- [ ] Storage account with queue enabled
- [ ] Function App with consumption plan (Linux)
- [ ] Application Insights connected to Function App
- [ ] Outputs include connection strings for CI/CD

### CI/CD Pipeline
- [ ] Workflow runs on push to `main` branch
- [ ] Infrastructure deployment step using Azure CLI
- [ ] Function App deployment using Azure Functions action
- [ ] Secrets stored in GitHub secrets:
  - `AZURE_CREDENTIALS` (service principal)
  - `AZURE_SUBSCRIPTION_ID`
  - `AZURE_OPENAI_KEY` (for Story 2)
- [ ] Deployment only on successful tests
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
