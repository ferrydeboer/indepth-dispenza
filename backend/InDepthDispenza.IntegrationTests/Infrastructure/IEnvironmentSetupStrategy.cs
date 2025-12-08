namespace InDepthDispenza.IntegrationTests.Infrastructure;

public interface IEnvironmentSetupStrategy
{
    Task<EnvironmentSetupOutput> SetupAsync();
    Task TeardownAsync();
}
