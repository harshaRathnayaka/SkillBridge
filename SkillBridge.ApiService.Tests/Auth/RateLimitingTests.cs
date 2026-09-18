using System.Net;
using System.Net.Http.Json;
using SkillBridge.ApiService.Auth.Contracts;
using SkillBridge.ApiService.Tests.TestSupport;

namespace SkillBridge.ApiService.Tests.Auth;

// Every other test in this project uses ApiServiceTestFactory's default, deliberately
// generous rate limit so it never interferes with correctness/stress testing — this is the
// one place that configures a small, real limit to prove throttling actually happens.
public class RateLimitingTests
{
    [Fact]
    public async Task Exceeding_the_configured_request_limit_returns_429()
    {
        using var factory = new ApiServiceTestFactory(rateLimitPermitLimit: 3);
        using var client = factory.CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 5; i++)
        {
            responses.Add(await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest("nobody@example.com", "whatever", $"device-{i}")));
        }

        Assert.DoesNotContain(responses.Take(3), r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.Contains(responses.Skip(3), r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task A_generous_limit_does_not_throttle_normal_use()
    {
        using var factory = new ApiServiceTestFactory();
        using var client = factory.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new LoginRequest("nobody@example.com", "whatever", $"device-{i}"));
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }
}
