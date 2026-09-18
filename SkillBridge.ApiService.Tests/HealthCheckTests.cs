using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
