using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NetArchTest.Rules;
using NUnit.Framework;
using System.Reflection;

namespace IndepthDispenza.Tests;

/// <summary>
/// Architecture tests that enforce the self-contained module convention.
///
/// Module Convention:
/// Each integration module (deepest namespace in Integrations folder) should be self-contained with:
/// 1. A {ModuleName}Module static class with Add{ModuleName}Module() extension method
/// 2. A {ModuleName}Options class for configuration (OPTIONAL - some modules may not need options)
/// 3. A {ModuleName}HealthCheck class implementing IHealthCheck
/// 4. The module is registered in Program.cs via the extension method
///
/// Example (YouTube module):
/// - YouTubeModule.cs with AddYouTubeModule(IServiceCollection, IConfiguration)
/// - YouTubeOptions.cs with configuration properties
/// - YouTubeHealthCheck.cs implementing IHealthCheck
///
/// Benefits:
/// - Encapsulation: All setup in one place
/// - Reusability: Easy to enable/disable or reuse in other projects
/// - Testability: Health check validates both config and runtime functionality
/// - Maintainability: Clear module boundaries
/// </summary>
[TestFixture]
public class ModuleConventionTests
{
    private const string IntegrationsNamespace = "InDepthDispenza.Functions.Integrations";

    /// <summary>
    /// Gets all module namespaces (deepest namespaces) in the Integrations folder.
    /// </summary>
    private static IEnumerable<string> GetModuleNamespaces()
    {
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var allNamespaces = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(IntegrationsNamespace) == true)
            .Select(t => t.Namespace!)
            .Distinct()
            .ToList();

        // Find deepest namespaces (namespaces that have no child namespaces)
        var deepestNamespaces = allNamespaces
            .Where(ns => !allNamespaces.Any(other => other != ns && other.StartsWith(ns + ".")))
            .OrderBy(ns => ns)
            .ToList();

        return deepestNamespaces;
    }

    /// <summary>
    /// Extracts module name from namespace (e.g., "InDepthDispenza.Functions.Integrations.YouTube" -> "YouTube")
    /// For nested modules like "InDepthDispenza.Functions.Integrations.Azure.Cosmos" -> "Cosmos"
    /// </summary>
    private static string GetModuleName(string moduleNamespace)
    {
        var parts = moduleNamespace.Split('.');
        return parts[^1]; // Last segment is the module name
    }

    [TestCaseSource(nameof(GetModuleNamespaces))]
    public void Module_ShouldHaveModuleClass(string moduleNamespace)
    {
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var moduleName = GetModuleName(moduleNamespace);
        var expectedModuleTypeName = $"{moduleNamespace}.{moduleName}Module";

        // Should have a {ModuleName}Module static class
        var moduleType = assembly.GetType(expectedModuleTypeName);
        Assert.That(moduleType, Is.Not.Null,
            $"Module namespace '{moduleNamespace}' should have a {moduleName}Module class");
        Assert.That(moduleType!.IsClass && moduleType.IsAbstract && moduleType.IsSealed,
            Is.True, $"{moduleName}Module should be a static class");
    }

    [TestCaseSource(nameof(GetModuleNamespaces))]
    public void Module_ShouldHaveAddModuleExtensionMethod(string moduleNamespace)
    {
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var moduleName = GetModuleName(moduleNamespace);
        var expectedModuleTypeName = $"{moduleNamespace}.{moduleName}Module";
        var moduleType = assembly.GetType(expectedModuleTypeName);

        Assert.That(moduleType, Is.Not.Null, $"{moduleName}Module class should exist");

        // Should have Add{ModuleName}Module extension method
        var expectedMethodName = $"Add{moduleName}Module";
        var extensionMethod = moduleType!.GetMethod(expectedMethodName,
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(IServiceCollection), typeof(IConfiguration) },
            null);

        Assert.That(extensionMethod, Is.Not.Null,
            $"{moduleName}Module should have a public static {expectedMethodName}(IServiceCollection, IConfiguration) method");
        Assert.That(extensionMethod!.ReturnType, Is.EqualTo(typeof(IServiceCollection)),
            $"{expectedMethodName} should return IServiceCollection for method chaining");
    }

    [TestCaseSource(nameof(GetModuleNamespaces))]
    public void Module_ShouldHaveHealthCheck(string moduleNamespace)
    {
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var moduleName = GetModuleName(moduleNamespace);
        var expectedHealthCheckTypeName = $"{moduleNamespace}.{moduleName}HealthCheck";

        // Should have {ModuleName}HealthCheck class implementing IHealthCheck
        var healthCheckType = assembly.GetType(expectedHealthCheckTypeName);
        Assert.That(healthCheckType, Is.Not.Null,
            $"Module namespace '{moduleNamespace}' should have a {moduleName}HealthCheck class");
        Assert.That(typeof(IHealthCheck).IsAssignableFrom(healthCheckType),
            Is.True, $"{moduleName}HealthCheck should implement IHealthCheck");
    }

    [Test]
    public void AllModules_WithModuleSuffix_ShouldBeStaticClasses()
    {
        // All classes ending with "Module" in Integrations namespace should be static
        var result = Types.InAssembly(typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly)
            .That()
            .ResideInNamespace(IntegrationsNamespace)
            .And()
            .HaveNameEndingWith("Module")
            .Should()
            .BeStatic()
            .GetResult();

        Assert.That(result.IsSuccessful, Is.True,
            $"All Module classes should be static. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Test]
    public void AllHealthChecks_InIntegrations_ShouldImplementIHealthCheck()
    {
        // All classes ending with "HealthCheck" in Integrations namespace should implement IHealthCheck
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var healthCheckTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(IntegrationsNamespace) == true)
            .Where(t => t.Name.EndsWith("HealthCheck"))
            .Where(t => t.IsClass && !t.IsAbstract)
            .ToList();

        Assert.That(healthCheckTypes, Is.Not.Empty,
            "Should find at least one HealthCheck class in Integrations namespace");

        foreach (var healthCheckType in healthCheckTypes)
        {
            Assert.That(typeof(IHealthCheck).IsAssignableFrom(healthCheckType),
                Is.True,
                $"{healthCheckType.Name} should implement IHealthCheck");
        }
    }

    [Test]
    public void ModuleConvention_Documentation()
    {
        // This test serves as documentation for the module convention
        // It always passes but documents the expected structure

        var documentation = @"
Module Convention Documentation:

Modules are identified by scanning the deepest namespaces in the Integrations folder.
For example:
- InDepthDispenza.Functions.Integrations.YouTube (module: YouTube)
- InDepthDispenza.Functions.Integrations.Azure.Cosmos (module: Cosmos)
- InDepthDispenza.Functions.Integrations.Azure.Storage (module: Storage)

Each integration module should follow this structure:

1. Directory: InDepthDispenza.Functions/Integrations/{Path}/

2. Required Files:
   - {ModuleName}Module.cs       : Static class with Add{ModuleName}Module() extension method
   - {ModuleName}HealthCheck.cs  : Health check implementing IHealthCheck
   - {ModuleName}Options.cs      : Configuration class (OPTIONAL)
   - Other implementation files as needed

3. Module Class Structure:
   ```csharp
   public static class {ModuleName}Module
   {
       public static IServiceCollection Add{ModuleName}Module(
           this IServiceCollection services,
           IConfiguration configuration)
       {
           // 1. Load configuration (if Options class exists)
           services.Configure<{ModuleName}Options>(configuration.GetSection('{ModuleName}'));

           // 2. Register services
           services.AddScoped<IService, Implementation>();

           // 3. Register health check
           services.AddHealthChecks()
               .AddCheck<{ModuleName}HealthCheck>('{modulename}');

           return services;
       }
   }
   ```

4. Health Check Structure:
   - Validates configuration is present and valid
   - Tests actual connectivity/functionality with external service
   - Returns Healthy/Degraded/Unhealthy with descriptive messages

5. Registration in Program.cs:
   ```csharp
   builder.Services.Add{ModuleName}Module(configuration);
   ```

Examples:
- YouTube Module: YouTubeModule, YouTubeOptions, YouTubeHealthCheck
- Cosmos Module: CosmosModule, CosmosDbOptions, CosmosHealthCheck
- Storage Module: StorageModule, StorageOptions, StorageHealthCheck
";

        Assert.Pass(documentation);
    }
}
