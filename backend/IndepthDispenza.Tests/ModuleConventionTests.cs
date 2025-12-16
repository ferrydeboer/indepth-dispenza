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
/// Each integration module (e.g., YouTube, Azure) should be self-contained with:
/// 1. A {ModuleName}Module static class with Add{ModuleName}Module() extension method
/// 2. A {ModuleName}Options class for configuration
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
    private const string FunctionsAssembly = "InDepthDispenza.Functions";
    private const string IntegrationsNamespace = "InDepthDispenza.Functions.Integrations";

    [Test]
    public void YouTube_Module_ShouldFollowConvention()
    {
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;

        // 1. Should have a YouTubeModule static class
        var moduleType = assembly.GetType("InDepthDispenza.Functions.Integrations.YouTube.YouTubeModule");
        Assert.That(moduleType, Is.Not.Null, "YouTubeModule class should exist");
        Assert.That(moduleType!.IsClass && moduleType.IsAbstract && moduleType.IsSealed,
            Is.True, "YouTubeModule should be a static class");

        // 2. Should have AddYouTubeModule extension method
        var extensionMethod = moduleType.GetMethod("AddYouTubeModule",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(IServiceCollection), typeof(IConfiguration) },
            null);

        Assert.That(extensionMethod, Is.Not.Null, "AddYouTubeModule(IServiceCollection, IConfiguration) method should exist");
        Assert.That(extensionMethod!.ReturnType, Is.EqualTo(typeof(IServiceCollection)),
            "AddYouTubeModule should return IServiceCollection for chaining");

        // 3. Should have YouTubeOptions class
        var optionsType = assembly.GetType("InDepthDispenza.Functions.Integrations.YouTube.YouTubeOptions");
        Assert.That(optionsType, Is.Not.Null, "YouTubeOptions class should exist");
        Assert.That(optionsType!.IsClass && !optionsType.IsAbstract, Is.True,
            "YouTubeOptions should be a concrete class");

        // 4. Should have YouTubeHealthCheck class implementing IHealthCheck
        var healthCheckType = assembly.GetType("InDepthDispenza.Functions.Integrations.YouTube.YouTubeHealthCheck");
        Assert.That(healthCheckType, Is.Not.Null, "YouTubeHealthCheck class should exist");
        Assert.That(typeof(IHealthCheck).IsAssignableFrom(healthCheckType),
            Is.True, "YouTubeHealthCheck should implement IHealthCheck");
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
    public void AllModules_ShouldHaveCorrespondingOptionsClass()
    {
        // For each {ModuleName}Module class, there should be a {ModuleName}Options class in the same namespace
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var moduleTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(IntegrationsNamespace) == true)
            .Where(t => t.Name.EndsWith("Module"))
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed) // static class
            .ToList();

        foreach (var moduleType in moduleTypes)
        {
            var moduleName = moduleType.Name.Replace("Module", "");
            var expectedOptionsName = $"{moduleName}Options";

            var optionsType = assembly.GetTypes()
                .FirstOrDefault(t =>
                    t.Namespace == moduleType.Namespace &&
                    t.Name == expectedOptionsName);

            Assert.That(optionsType, Is.Not.Null,
                $"Module {moduleType.Name} should have a corresponding {expectedOptionsName} class in the same namespace");
        }
    }

    [Test]
    public void AllModules_ShouldHaveCorrespondingHealthCheck()
    {
        // For each {ModuleName}Module class, there should be a {ModuleName}HealthCheck class in the same namespace
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var moduleTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(IntegrationsNamespace) == true)
            .Where(t => t.Name.EndsWith("Module"))
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed) // static class
            .ToList();

        foreach (var moduleType in moduleTypes)
        {
            var moduleName = moduleType.Name.Replace("Module", "");
            var expectedHealthCheckName = $"{moduleName}HealthCheck";

            var healthCheckType = assembly.GetTypes()
                .FirstOrDefault(t =>
                    t.Namespace == moduleType.Namespace &&
                    t.Name == expectedHealthCheckName);

            Assert.That(healthCheckType, Is.Not.Null,
                $"Module {moduleType.Name} should have a corresponding {expectedHealthCheckName} class in the same namespace");

            if (healthCheckType != null)
            {
                Assert.That(typeof(IHealthCheck).IsAssignableFrom(healthCheckType),
                    Is.True,
                    $"{expectedHealthCheckName} should implement IHealthCheck");
            }
        }
    }

    [Test]
    public void AllModules_ShouldHaveAddModuleExtensionMethod()
    {
        // For each {ModuleName}Module class, there should be an Add{ModuleName}Module extension method
        var assembly = typeof(InDepthDispenza.Functions.Interfaces.IPlaylistService).Assembly;
        var moduleTypes = assembly.GetTypes()
            .Where(t => t.Namespace?.StartsWith(IntegrationsNamespace) == true)
            .Where(t => t.Name.EndsWith("Module"))
            .Where(t => t.IsClass && t.IsAbstract && t.IsSealed) // static class
            .ToList();

        foreach (var moduleType in moduleTypes)
        {
            var moduleName = moduleType.Name.Replace("Module", "");
            var expectedMethodName = $"Add{moduleName}Module";

            var extensionMethod = moduleType.GetMethod(expectedMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IServiceCollection), typeof(IConfiguration) },
                null);

            Assert.That(extensionMethod, Is.Not.Null,
                $"Module {moduleType.Name} should have a public static {expectedMethodName}(IServiceCollection, IConfiguration) method");

            if (extensionMethod != null)
            {
                Assert.That(extensionMethod.ReturnType, Is.EqualTo(typeof(IServiceCollection)),
                    $"{expectedMethodName} should return IServiceCollection for method chaining");
            }
        }
    }

    [Test]
    public void ModuleConvention_Documentation()
    {
        // This test serves as documentation for the module convention
        // It always passes but documents the expected structure

        var documentation = @"
Module Convention Documentation:

Each integration module should follow this structure:

1. Directory: InDepthDispenza.Functions/Integrations/{ModuleName}/

2. Required Files:
   - {ModuleName}Module.cs       : Static class with Add{ModuleName}Module() extension method
   - {ModuleName}Options.cs      : Configuration class
   - {ModuleName}HealthCheck.cs  : Health check implementing IHealthCheck
   - Other implementation files as needed

3. Module Class Structure:
   ```csharp
   public static class {ModuleName}Module
   {
       public static IServiceCollection Add{ModuleName}Module(
           this IServiceCollection services,
           IConfiguration configuration)
       {
           // 1. Load configuration
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

Example: YouTube Module
- YouTubeModule.cs with AddYouTubeModule()
- YouTubeOptions.cs with ApiKey and ApiBaseUrl
- YouTubeHealthCheck.cs that validates config and tests API connectivity
";

        Assert.Pass(documentation);
    }
}
