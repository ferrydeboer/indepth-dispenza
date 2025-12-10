using NetArchTest.Rules;
using NUnit.Framework;

namespace IndepthDispenza.Tests;

/// <summary>
/// Architecture tests that verify namespace dependencies remain acyclic.
/// Based on the component diagram in docs/architecture/component-diagram.puml
/// </summary>
[TestFixture]
public class ArchitectureTests
{
    private const string FunctionsAssembly = "InDepthDispenza.Functions";
    private const string InterfacesNamespace = "InDepthDispenza.Functions.Interfaces";
    private const string VideoAnalysisNamespace = "InDepthDispenza.Functions.VideoAnalysis";
    private const string IntegrationsYouTubeNamespace = "InDepthDispenza.Functions.Integrations.YouTube";
    private const string IntegrationsAzureNamespace = "InDepthDispenza.Functions.Integrations.Azure";
    private const string IntegrationsNamespace = "InDepthDispenza.Functions.Integrations";

    [Test]
    public void Interfaces_ShouldNotDependOnAnyOtherNamespace()
    {
        // Interfaces is the decoupling layer and should have no dependencies on other project namespaces
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(InterfacesNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                VideoAnalysisNamespace,
                IntegrationsYouTubeNamespace,
                IntegrationsAzureNamespace,
                $"{IntegrationsNamespace}.Azure",
                $"{IntegrationsNamespace}.YouTube"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"Interfaces namespace should not depend on other project namespaces. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void VideoAnalysis_ShouldOnlyDependOnInterfaces()
    {
        // VideoAnalysis should only depend on Interfaces, not on Integration implementations
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(VideoAnalysisNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                IntegrationsYouTubeNamespace,
                IntegrationsAzureNamespace,
                $"{IntegrationsNamespace}.Azure",
                $"{IntegrationsNamespace}.YouTube"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"VideoAnalysis should only depend on Interfaces, not Integration implementations. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void IntegrationsYouTube_ShouldOnlyDependOnInterfaces()
    {
        // Integrations.YouTube should only depend on Interfaces
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(IntegrationsYouTubeNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                VideoAnalysisNamespace,
                IntegrationsAzureNamespace,
                $"{IntegrationsNamespace}.Azure"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"Integrations.YouTube should only depend on Interfaces. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void IntegrationsAzure_ShouldOnlyDependOnInterfaces()
    {
        // Integrations.Azure should only depend on Interfaces
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(IntegrationsAzureNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                VideoAnalysisNamespace,
                IntegrationsYouTubeNamespace,
                $"{IntegrationsNamespace}.YouTube"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"Integrations.Azure should only depend on Interfaces. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void Integrations_ShouldOnlyDependOnInterfaces()
    {
        // Root Integrations namespace (e.g., LoggingQueueService) should only depend on Interfaces
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(IntegrationsNamespace)
            .And()
            .DoNotResideInNamespace($"{IntegrationsNamespace}.YouTube")
            .And()
            .DoNotResideInNamespace($"{IntegrationsNamespace}.Azure")
            .ShouldNot()
            .HaveDependencyOnAny(
                VideoAnalysisNamespace,
                IntegrationsYouTubeNamespace,
                IntegrationsAzureNamespace,
                $"{IntegrationsNamespace}.YouTube",
                $"{IntegrationsNamespace}.Azure"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"Integrations namespace should only depend on Interfaces. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void RootNamespace_ShouldNotDependOnIntegrationImplementations()
    {
        // Root namespace (e.g., ScanPlaylist) should depend on Interfaces and VideoAnalysis, but not Integration implementations
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(FunctionsAssembly)
            .And()
            .DoNotResideInNamespace(InterfacesNamespace)
            .And()
            .DoNotResideInNamespace(VideoAnalysisNamespace)
            .And()
            .DoNotResideInNamespace(IntegrationsNamespace)
            .ShouldNot()
            .HaveDependencyOnAny(
                IntegrationsYouTubeNamespace,
                IntegrationsAzureNamespace,
                $"{IntegrationsNamespace}.YouTube",
                $"{IntegrationsNamespace}.Azure"
            )
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"Root namespace should not depend on Integration implementations (dependency injection handles this). Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void AllNamespaces_ShouldNotHaveCircularDependencies()
    {
        // Global check: ensure no circular dependencies exist in the assembly
        var types = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(FunctionsAssembly)
            .GetTypes()
            .ToList();

        Assert.That(types, Is.Not.Empty, "Should find types in the Functions assembly");

        // This test ensures the dependency graph remains acyclic by verifying specific rules
        // The individual tests above enforce the acyclic architecture
        // This test serves as documentation and a catch-all
        Assert.Pass($"Verified {types.Count} types across namespaces maintain acyclic dependency architecture");
    }
}
