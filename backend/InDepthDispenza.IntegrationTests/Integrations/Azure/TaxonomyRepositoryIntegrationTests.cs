using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using InDepthDispenza.Functions.Integrations.Azure.Cosmos;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.IntegrationTests.Infrastructure;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InDepthDispenza.IntegrationTests.Integrations.Azure;

[TestFixture]
public class TaxonomyRepositoryIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task NewerMajorSeedVersion_IsInserted_AsNewTaxonomyVersion()
    {
        // Arrange: create Cosmos client connected to emulator
        var connectionString = EnvironmentSetup.CosmosDbContainer
            .GetConnectionString()
            .Replace("https://", "http://");

        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            LimitToEndpoint = true,
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        using var cosmos = new CosmosClient(connectionString, clientOptions);

        // Use a dedicated, randomized container name per test run to avoid interference
        var containerName = $"taxonomy-versions-{Fixture.Create<Guid>():N}";
        var options = Options.Create(new CosmosDbOptions
        {
            AccountEndpoint = EnvironmentSetup.CosmosDbEndpoint,
            AccountKey = EnvironmentSetup.CosmosDbKey,
            DatabaseName = EnvironmentSetup.CosmosDbDatabaseName,
            TaxonomyVersionsContainer = containerName
        });

        // Ensure the container exists and is empty for a clean start
        var db = cosmos.GetDatabase(options.Value.DatabaseName!);
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = options.Value.TaxonomyVersionsContainer!,
            PartitionKeyPath = "/id"
        });
        var container = db.GetContainer(options.Value.TaxonomyVersionsContainer!);
        // Delete all items if any
        using (var iterator = container.GetItemQueryIterator<dynamic>("SELECT c.id FROM c"))
        {
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync();
                foreach (var item in page)
                {
                    string id = item.id;
                    await container.DeleteItemAsync<dynamic>(id, new PartitionKey(id));
                }
            }
        }

        // Prepare the seed file by copying the ACTUAL repo seed into the location the repository reads from
        // This ensures the test breaks if the real seed format/content changes incompatibly.
        // Repository expects: AppContext.BaseDirectory/VideoAnalysis/Taxonomy/taxonomy-seed.json
        var seedDir = Path.Combine(AppContext.BaseDirectory, "VideoAnalysis", "Taxonomy");
        Directory.CreateDirectory(seedDir);
        var seedPath = Path.Combine(seedDir, "taxonomy-seed.json");

        // Locate the actual seed file in the Functions project
        var repoSeedPath = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..",
            "InDepthDispenza.Functions",
            "VideoAnalysis", "Taxonomy", "taxonomy-seed.json"));

        var seedJson = await File.ReadAllTextAsync(repoSeedPath);
        await File.WriteAllTextAsync(seedPath, seedJson);

        // Read expected version from the actual seed file so assertions adapt to file changes
        string? seedVersionString = null;
        using (var docJson = JsonDocument.Parse(seedJson))
        {
            var root = docJson.RootElement;
            if (root.TryGetProperty("id", out var versionEl) && versionEl.ValueKind == JsonValueKind.String)
            {
                seedVersionString = versionEl.GetString();
            }
        }
        seedVersionString.Should().NotBeNull("seed file must contain a top-level 'id' string");
        var expectedSeedVersion = TaxonomyVersion.Parse(seedVersionString!);

        // Insert an older version (v1.0) to verify that seeding upgrades to v2.0
        var repo = new CosmosTaxonomyRepository(
            NullLogger<CosmosTaxonomyRepository>.Instance,
            cosmos,
            options);

        var oldDoc = new TaxonomyDocument
        {
            Version = new TaxonomyVersion(1, 0),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Changes = [Fixture.Create<string>()],
            Taxonomy =
            {
                [Fixture.Create<string>().Replace(" ", "_")] = new AchievementTypeGroup
                {
                    [Fixture.Create<string>().Replace(" ", "_")] = new CategoryNode
                    {
                        Subcategories = Fixture.CreateMany<string>(2).ToList(),
                        Attributes = Fixture.CreateMany<string>(2).ToList()
                    }
                }
            }
        };

        var saveOld = await repo.SaveTaxonomyAsync(oldDoc);
        saveOld.IsSuccess.Should().BeTrue(saveOld.ErrorMessage);

        // Act: trigger repository to check seed and upgrade if newer
        var latestResult = await repo.GetLatestTaxonomyAsync();

        // Assert
        latestResult.IsSuccess.Should().BeTrue(latestResult.ErrorMessage);
        latestResult.Data.Should().NotBeNull();
        latestResult.Data!.Version.Should().Be(expectedSeedVersion);

        // Also confirm there are now two versions present (v1.0 and v2.0)
        int count = 0;
        using (var it = container.GetItemQueryIterator<dynamic>("SELECT VALUE COUNT(1) FROM c"))
        {
            while (it.HasMoreResults)
            {
                var page = await it.ReadNextAsync();
                foreach (var c in page)
                {
                    count = (int)c;
                }
            }
        }
        count.Should().BeGreaterThanOrEqualTo(2);
    }
}
