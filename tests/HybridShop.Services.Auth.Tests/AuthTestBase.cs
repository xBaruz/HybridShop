using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HybridShop.Services.Auth.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace HybridShop.Services.Auth.Tests;

public abstract class AuthTestBase : IClassFixture<AuthWebApplicationFactory>, IAsyncLifetime
{
    protected readonly AuthWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    protected AuthTestBase(AuthWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        await dbContext.Users.ExecuteDeleteAsync();
    }

    protected async Task<(string Email, string Password)> RegisterUserAsync(
        string? email = null, 
        string? password = null, 
        string name = "Jan", 
        string lastname = "Kowalski",
        char gender = 'M',
        DateOnly? birthday = null)
    {
        var userEmail = email ?? $"user_{Guid.NewGuid():N}@test.com";
        var userPassword = password ?? "StrongPassword123!";
        var userBirthday = birthday ?? new DateOnly(2000, 1, 1);

        var registerDto = new
        {
            Email = userEmail,
            Password = userPassword,
            Name = name,
            Lastname = lastname,
            Gender = gender,
            Birthday = userBirthday.ToString("yyyy-MM-dd")
        };

        var response = await Client.PostAsJsonAsync("/api/auth/register", registerDto);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Rejestracja nie powiodła się. Status: {response.StatusCode}, Odpowiedź: {errorContent}");
        }

        return (userEmail, userPassword);
    }

    protected async Task<string> LoginUserAsync(string email, string password)
    {
        var loginDto = new
        {
            Email = email,
            Password = password
        };

        var response = await Client.PostAsJsonAsync("/api/auth/login", loginDto);
        response.IsSuccessStatusCode.ShouldBeTrue($"Logowanie nie powiodło się dla: {email}");

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);

        string token = string.Empty;

        if (jsonDoc.RootElement.TryGetProperty("token", out var tokenProp))
        {
            token = tokenProp.GetString()!;
        }
        else if (jsonDoc.RootElement.TryGetProperty("accessToken", out var accessTokenProp))
        {
            token = accessTokenProp.GetString()!;
        }

        token.ShouldNotBeNullOrEmpty("Nie odnaleziono tokena JWT w odpowiedzi logowania.");
        return token;
    }

    protected HttpClient CreateAuthenticatedClient(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}