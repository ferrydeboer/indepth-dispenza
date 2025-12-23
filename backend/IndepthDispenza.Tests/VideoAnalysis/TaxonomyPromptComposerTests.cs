using System.Text.Json;
using InDepthDispenza.Functions.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Prompting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis;

public class TaxonomyPromptComposerTests
{
    private static string GetFunctionsOutputPath()
    {
        var assemblyLocation = typeof(TaxonomyPromptComposer).Assembly.Location;
        return Path.GetDirectoryName(assemblyLocation)!;
    }

    private static string LoadTemplateFirstLine()
    {
        var baseDir = GetFunctionsOutputPath();
        var templatePath = Path.Combine(baseDir, "VideoAnalysis", "Prompting", "taxonomy-prompt.md");
        Assert.That(File.Exists(templatePath), Is.True, $"Template file not found at {templatePath}");
        var firstLine = File.ReadLines(templatePath).First();
        return firstLine.TrimEnd('\r');
    }

    [Test]
    public async Task ComposeAsync_InsertsTaxonomyAndKeepsTemplateHeader()
    {
        // Arrange
        var mockRepo = new Mock<ITaxonomyRepository>();
        var doc = new TaxonomyDocument
        {
            Version = "v1.0",
            UpdatedAt = DateTimeOffset.UtcNow,
            Changes = Array.Empty<string>()
        };
        doc.Taxonomy["simple_root"] = new AchievementTypeGroup
        {
            ["child"] = new CategoryNode()
        };

        mockRepo
            .Setup(r => r.GetLatestTaxonomyAsync())
            .ReturnsAsync(ServiceResult<TaxonomyDocument?>.Success(doc));

        var composer = new TaxonomyPromptComposer(NullLogger<TaxonomyPromptComposer>.Instance, mockRepo.Object);
        var prompt = new Prompt();

        // Act
        await composer.ComposeAsync(prompt, "vid-1");
        var built = prompt.Build();

        // Assert: taxonomy placeholder replaced
        Assert.That(built.Contains("{taxonomy}"), Is.False, "Placeholder {taxonomy} should be replaced");
        Assert.That(built.Contains("\"simple_root\""), Is.True, "Inserted taxonomy JSON should be present");

        // Assert: first line equals template first line
        var templateFirstLine = LoadTemplateFirstLine();
        Assert.That(built.Contains(templateFirstLine), Is.True, "Prompt should contain the template header line");
    }
}
