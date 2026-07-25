using System.Net;
using System.Net.Http.Json;
using HybridShop.Services.Auth.Application.Dto;
using HybridShop.Services.Auth.Core.Dto;
using Shouldly;
using Xunit;

namespace HybridShop.Services.Auth.Tests;

public class UserIntegrationTests : AuthTestBase
{
    public UserIntegrationTests(AuthWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetUserData_Authenticated_ReturnsUserData()
    {
        var (email, password) = await RegisterUserAsync();
        var token = await LoginUserAsync(email, password);
        var authClient = CreateAuthenticatedClient(token);

        var response = await authClient.GetAsync("/api/user");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        user.ShouldNotBeNull();
        user.Email.ShouldBe(email);
    }

    [Fact]
    public async Task Update_Authenticated_UpdatesUserData()
    {
        var (email, password) = await RegisterUserAsync();
        var token = await LoginUserAsync(email, password);
        var authClient = CreateAuthenticatedClient(token);

        var updateDto = new UpdateUserDto("Piotr", "Nowak", 'M', new DateOnly(1990, 2, 2));
        var updateResponse = await authClient.PutAsJsonAsync("/api/user/update", updateDto);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_Authenticated_SoftDeletesUser()
    {
        var (email, password) = await RegisterUserAsync();
        var token = await LoginUserAsync(email, password);
        var authClient = CreateAuthenticatedClient(token);

        var deleteResponse = await authClient.DeleteAsync("/api/user/delete");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loginResponse = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}