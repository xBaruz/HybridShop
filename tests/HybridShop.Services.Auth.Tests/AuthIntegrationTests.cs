using System.Net;
using System.Net.Http.Json;
using HybridShop.Services.Auth.Application.Dto;
using Shouldly;
using Xunit;

namespace HybridShop.Services.Auth.Tests;

public class AuthIntegrationTests : AuthTestBase
{
    public AuthIntegrationTests(AuthWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Register_ValidData_Returns200OK()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var dto = new RegisterRequest(email, "StrongPassword123!", "Jan", "Kowalski", 'M', new DateOnly(1995, 5, 10));

        var response = await Client.PostAsJsonAsync("/api/auth/register", dto);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400BadRequest()
    {
        var (email, password) = await RegisterUserAsync();
        var dto = new RegisterRequest(email, password, "Jan", "Kowalski", 'M', new DateOnly(1995, 5, 10));

        var response = await Client.PostAsJsonAsync("/api/auth/register", dto);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        var (email, password) = await RegisterUserAsync();
        var loginDto = new LoginRequest(email, password);

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginDto);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_ReturnsNewTokens()
    {
        var (email, password) = await RegisterUserAsync();
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        var authData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(authData!.RefreshToken));

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newAuthData = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>();
        newAuthData.ShouldNotBeNull();
        newAuthData.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshToken_ReuseSameToken_ShouldFailOnSecondTry()
    {
        var (email, password) = await RegisterUserAsync();
        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        
        var content = await loginResponse.Content.ReadAsStringAsync();
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
        var refreshToken = jsonDoc.RootElement.GetProperty("refreshToken").GetString();

        var firstRefresh = await Client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken });
        firstRefresh.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var secondRefresh = await Client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken });
        secondRefresh.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesAllRefreshTokens()
    {
        var (email, password) = await RegisterUserAsync();
        var token = await LoginUserAsync(email, password);
        var authClient = CreateAuthenticatedClient(token);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        using var jsonDoc = System.Text.Json.JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var refreshToken = jsonDoc.RootElement.GetProperty("refreshToken").GetString();

        var logoutResponse = await authClient.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new { RefreshToken = refreshToken });
        refreshResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_UserTwice_ReturnsConflict()
    {
        var (email, password) = await RegisterUserAsync();
        var token = await LoginUserAsync(email, password);
        var authClient = CreateAuthenticatedClient(token);

        var firstDelete = await authClient.DeleteAsync("/api/user/delete");
        firstDelete.StatusCode.ShouldBe(System.Net.HttpStatusCode.NoContent);

        var secondDelete = await authClient.DeleteAsync("/api/user/delete");
        secondDelete.StatusCode.ShouldBe(System.Net.HttpStatusCode.Conflict);
    }
}