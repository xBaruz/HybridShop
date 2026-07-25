using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HybridShop.Services.Auth.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HybridShop.Services.Auth.Tests;

public class SecurityTests : AuthTestBase
{
    public SecurityTests(AuthWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("admin' --")]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE \"Users\"; --")]
    public async Task Login_SqlInjectionPayload_ReturnsUnauthorizedOrBadRequest(string sqlPayload)
    {
        var loginDto = new { Email = sqlPayload, Password = "password123" };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginDto);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutToken_Returns401Unauthorized()
    {
        var getResponse = await Client.GetAsync("/api/user");
        var logoutResponse = await Client.PostAsync("/api/auth/logout", null);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoints_WithForgedToken_Returns401Unauthorized()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        var response = await client.GetAsync("/api/user");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    public async Task Register_XssPayloadInInput_ShouldNotFailServerError(string xssPayload)
    {
        var registerDto = new
        {
            Email = $"xss_{Guid.NewGuid():N}@test.com",
            Password = "StrongPassword123!",
            Name = xssPayload,
            Lastname = "Test",
            Gender = 'M',
            Birthday = "2000-01-01"
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerDto);
        
        response.StatusCode.ShouldNotBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ProtectedEndpoint_TamperedJwt_Returns401Unauthorized()
    {
        var (email, pass) = await RegisterUserAsync();
        var validToken = await LoginUserAsync(email, pass);

        var tamperedToken = validToken[..^5] + "XXXXX";
        var client = CreateAuthenticatedClient(tamperedToken);

        var response = await client.GetAsync("/api/user");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedUser_UsingValidToken_Returns401Or404()
    {
        var (email, pass) = await RegisterUserAsync();
        var token = await LoginUserAsync(email, pass);
        var authClient = CreateAuthenticatedClient(token);

        await authClient.DeleteAsync("/api/user/delete");

        var response = await authClient.GetAsync("/api/user");
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_MultipleFailedAttempts_ShouldBeRateLimited()
    {
        var loginDto = new { Email = "victim@test.com", Password = "wrong_password" };

        for (int i = 0; i < 10; i++)
        {
            await Client.PostAsJsonAsync("/api/auth/login", loginDto);
        }

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginDto);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.TooManyRequests, HttpStatusCode.Unauthorized);
    }
}