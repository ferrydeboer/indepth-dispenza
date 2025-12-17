using System.Text.Json;
using InDepthDispenza.Functions.VideoAnalysis.Interfaces;
using InDepthDispenza.Functions.VideoAnalysis.Prompting;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace IndepthDispenza.Tests.VideoAnalysis;

public class OutputPromptComposerTests
{
    private static string GetFunctionsOutputPath()
    {
        var assemblyLocation = typeof(OutputPromptComposer).Assembly.Location;
        return Path.GetDirectoryName(assemblyLocation)!;
    }

    private static string LoadTemplateFirstLine()
    {
        var baseDir = GetFunctionsOutputPath();
        var templatePath = Path.Combine(baseDir, "VideoAnalysis", "Prompting", "output-prompt.md");
        Assert.That(File.Exists(templatePath), Is.True, $"Template file not found at {templatePath}");
        var firstLine = File.ReadLines(templatePath).First();
        return firstLine.TrimEnd('\r');
    }

    [Test]
    public async Task ComposeAsync_InsertsExampleAndKeepsTemplateHeader()
    {
        // Arrange
        var composer = new OutputPromptComposer(NullLogger<OutputPromptComposer>.Instance);
        var prompt = new Prompt();

        // Act
        await composer.ComposeAsync(prompt, "vid-out-test");
        var built = prompt.Build();

        // Assert: placeholder replaced
        Assert.That(built.Contains("{format}"), Is.False, "Placeholder {format} should be replaced");

        // Assert: header present
        var templateFirstLine = LoadTemplateFirstLine();
        Assert.That(built.Contains(templateFirstLine), Is.True, "Prompt should contain the template header line");

        // Extract JSON between ```json and ``` and validate structure
        var startMarker = "```json";
        var startIdx = built.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.That(startIdx, Is.GreaterThanOrEqualTo(0), "JSON block start not found");
        startIdx += startMarker.Length;
        var endIdx = built.IndexOf("```", startIdx, StringComparison.Ordinal);
        Assert.That(endIdx, Is.GreaterThan(startIdx), "JSON block end not found");
        var json = built.Substring(startIdx, endIdx - startIdx).Trim();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.That(root.TryGetProperty("analysis", out var analysis), Is.True, "analysis property missing");
        Assert.That(analysis.TryGetProperty("achievements", out _), Is.True, "achievements missing");
        Assert.That(analysis.TryGetProperty("practices", out _), Is.True, "practices missing");
        Assert.That(root.TryGetProperty("proposals", out var proposals), Is.True, "proposals property missing");
        Assert.That(proposals.TryGetProperty("taxonomy", out _), Is.True, "proposals.taxonomy missing");
        var taxonomy = proposals.GetProperty("taxonomy");
        Assert.That(taxonomy.ValueKind, Is.EqualTo(JsonValueKind.Array), "proposals.taxonomy should be an array");
        var firstProposal = taxonomy[0];
        // Should contain dynamic key "healing" and a sibling "justification"
        Assert.That(firstProposal.TryGetProperty("justification", out var justification), Is.True, "justification missing on taxonomy proposal");
        // Find the dynamic parent domain by checking known key
        Assert.That(firstProposal.TryGetProperty("healing", out var healing), Is.True, "expected dynamic parent key 'healing' missing on taxonomy proposal");
        // under healing -> should contain neurological node
        Assert.That(healing.TryGetProperty("neurological", out var neuro), Is.True, "neurological node missing under healing");
        Assert.That(neuro.TryGetProperty("subcategories", out var subs), Is.True, "subcategories missing under neurological");
        Assert.That(subs.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(subs.EnumerateArray().Any(e => e.GetString() == "tinnitus"), Is.True, "expected subcategory 'tinnitus'");
    }
}
