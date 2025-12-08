# Integration Testing Architecture

## Overview

This document describes the integration testing infrastructure for the InDepthDispenza Azure Functions application. 
The tests are designed as full black-box integration tests where all components run in Docker containers with proper 
network isolation and communication. The idea behind this is that potentiall the code can be reused against
actual infrastructure. The test containers themselves and extraction of the initialization facilitated replacement.

## Architecture

### Container Setup

The integration tests orchestrate three Docker containers:

1. **Azure Functions Container** - The application under test
2. **WireMock Container** - Mocks the YouTube API
3. **Azurite Container** - Emulates Azure Storage Queue

All containers communicate via a dedicated Docker network: `indepthdispenza-test-network`

### Network Architecture

```
┌─────────────────────────────────────────────────────┐
│   Docker Network: indepthdispenza-test-network      │
│                                                     │
│   ┌──────────────┐    ┌──────────────┐              │
│   │  WireMock    │    │  Azurite     │              │
│   │ (wiremock)   │    │ (azurite)    │              │
│   │  Port 80     │    │  Port 10000  │              │
│   └──────┬───────┘    └──────┬───────┘              │
│          │                    │                     │
│          │  Internal Network  │                     │
│          │                    │                     │
│   ┌──────┴────────────────────┴────┐                │
│   │    Functions Container         │                │
│   │  Uses: http://wiremock:80      │                │
│   │  Uses: azurite:10000           │                │
│   └────────────────┬───────────────┘                │
│                    │ Port 80                        │
└────────────────────┼────────────────────────────────┘
                     │ Mapped ports
         ┌───────────┴──────────────┐
         │       Host (test runner)  │
         │  Uses: 127.0.0.1:{ports}  │
         │  - Verify HTTP responses  │
         │  - Check queue messages   │
         └───────────────────────────┘
```

## Key Components

### 1. DockerEnvironmentSetupStrategy

Responsible for setting up the Docker infrastructure:

- Creates a Docker network for inter-container communication
- Starts Azurite container with network alias `azurite`
- Starts WireMock container with network alias `wiremock`
- Provides two sets of connection strings:
  - **Container URLs**: Using internal network aliases (for Functions container)
  - **Host URLs**: Using localhost with mapped ports (for test runner)

**Network Aliases:**
- WireMock: `wiremock` (accessible at `http://wiremock:80` from containers)
- Azurite: `azurite` (accessible at `azurite:10000` from containers)

### 2. WireMockConfiguration

Helper class to configure mock YouTube API responses using WireMock.Net's admin client:

- Uses `wireMockContainer.CreateWireMockAdminClient()` for type-safe API interaction
- Configures success responses with mock video data
- Configures error responses (e.g., 429 rate limits)
- Provides `Reset()` to clear all mappings between tests

### 3. IntegrationTestBase

Base class providing:

- Container orchestration (setup/teardown)
- Docker image building from Dockerfile
- Functions container startup with environment configuration
- Shared test fixtures (QueueClient, HttpClient, WireMock config)
- Test isolation (queue clearing, mock resetting between tests)

### 4. EnvironmentSetupOutput

Configuration model containing:

```csharp
public record EnvironmentSetupOutput(
    string AzureWebJobsStorage,           // Internal URL for Functions
    string AzureWebJobsStorageForHost,    // Public URL for test runner
    string VideoQueueName,
    string YouTubeApiBaseUrl,             // Internal URL: http://wiremock:80/youtube/v3
    string YouTubeApiKey,
    WireMockContainer WireMockContainer,
    INetwork Network
);
```

## Connection String Strategy

### Problem
Containers need to communicate via internal Docker DNS, while the test runner on the host needs to use localhost with mapped ports.

### Solution
Two separate connection strings:

#### For Functions Container (Internal Network)
```
AzureWebJobsStorage: DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...;BlobEndpoint=http://azurite:10000/...
YouTubeApiBaseUrl: http://wiremock:80/youtube/v3
```

#### For Test Runner (Host Machine)
```
AzureWebJobsStorageForHost: DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...;BlobEndpoint=http://127.0.0.1:{mappedPort}/...
WireMock Admin API: Uses WireMockContainer.CreateWireMockAdminClient()
```

## Test Execution Flow

### OneTimeSetUp (Per Test Fixture)
1. Create Docker network
2. Start Azurite container
3. Start WireMock container
4. Build Functions Docker image from Dockerfile
5. Start Functions container
6. Create QueueClient (using host connection string)
7. Create HttpClient for Functions endpoint
8. Wait for Functions host to be ready

### SetUp (Per Test)
1. Clear all messages from the queue
2. Reset all WireMock mappings

### Test Execution
1. Configure WireMock with expected YouTube API responses
2. Make HTTP call to Functions endpoint
3. Verify HTTP response
4. Verify messages in Azurite queue

### OneTimeTearDown
1. Stop Functions container
2. Stop WireMock container
3. Stop Azurite container
4. Delete Docker network

## Example Test

```csharp
[Test]
public async Task VideosQueuedSuccessfully_WhenPlaylistScannedWithLimit()
{
    // Arrange - Configure WireMock to return 100 videos
    const int totalVideos = 100;
    const int limit = 3;
    await WireMockConfig.SetupPlaylistResponse(TestPlaylistId, totalVideos);

    // Act - Call the Functions endpoint
    var response = await InvokeScanPlaylistAsync(TestPlaylistId, limit);

    // Assert - Verify HTTP response
    response.IsSuccess.Should().BeTrue();
    response.StatusCode.Should().Be(200);

    // Assert - Verify queue messages
    var queueProperties = await QueueClient.GetPropertiesAsync();
    queueProperties.Value.ApproximateMessagesCount.Should().Be(limit);
}
```

## Benefits of This Approach

### True Black-Box Testing
- Tests the complete system end-to-end
- No mocking of internal components
- Tests the actual Docker image that would be deployed

### Proper Network Isolation
- Mimics real-world container deployments
- Uses Docker's internal DNS for service discovery
- Same pattern as docker-compose

### Environment Switching
- Strategy pattern allows switching between Docker and real infrastructure
- Easy to add other environment setups (e.g., LocalEnvironmentSetupStrategy)

### Maintainability
- Containers are automatically managed
- Automatic cleanup on test completion
- Dockerfile follows standard conventions (can build manually)

### Testcontainers Integration
- Uses official Testcontainers libraries
- Leverages WireMock.Net's built-in admin client
- Network management handled by Testcontainers

## Running the Tests

### Prerequisites
- Docker installed and running
- .NET 9 SDK
- Azure Functions Core Tools (for local development)

### Build
```bash
dotnet build InDepthDispenza.IntegrationTests/InDepthDispenza.IntegrationTests.csproj
```

### Run Tests
```bash
dotnet test InDepthDispenza.IntegrationTests/InDepthDispenza.IntegrationTests.csproj
```

### Manual Docker Build (Optional)
```bash
cd InDepthDispenza.Functions
docker build -t indepthdispenza-functions:test .
```

## Dockerfile

The Functions app uses a multi-stage Dockerfile:

1. **Build Stage**: Compiles the .NET project
2. **Publish Stage**: Creates release artifacts
3. **Final Stage**: Installs Azure Functions Core Tools and runs `func start`

The Dockerfile is designed to be built from its own directory, following Docker conventions.

## Troubleshooting

### Container Not Starting
- Check Docker daemon is running
- Check port conflicts (80, 10000, 1080)
- Review container logs in test output

### Network Issues
- Ensure network is being created (`docker network ls`)
- Verify containers are on the same network (`docker network inspect indepthdispenza-test-network`)
- Check network aliases are configured correctly

### Connection Errors from Test Runner
- Ensure using `AzureWebJobsStorageForHost` not `AzureWebJobsStorage`
- Verify ports are mapped correctly
- Check localhost connectivity

### Functions Not Responding
- Increase wait timeout in `WaitForFunctionHostAsync()`
- Check Functions logs in container output
- Verify environment variables are set correctly
- Ensure Azure Functions Core Tools is installed in the image

## Future Enhancements

### Potential Improvements
- Add support for testing with real Azure infrastructure
- Implement performance/load testing scenarios
- Add test data builders for complex scenarios
- Create helper methods for common assertion patterns
- Add support for parallel test execution with unique networks per fixture
