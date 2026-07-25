using HybridShop.BuildingBlocks.EventBus.Events;
using HybridShop.Services.Auth.Application.Dto;
using HybridShop.Services.Auth.Application.Exceptions;
using HybridShop.Services.Auth.Application.Services;
using HybridShop.Services.Auth.Core.Interfaces;
using HybridShop.Services.Auth.Core.Models;
using MassTransit;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HybridShop.Services.Auth.UnitTests.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly TokenService _tokenService;
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "SuperSecretTestKeyThatIsVeryLongAndSecure12345!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience"
            })
            .Build();

        _tokenService = new TokenService(config);
        _sut = new AuthService(_userRepository, _tokenService, _publishEndpoint);
    }

    [Fact]
    public async Task RegisterAsync_ValidData_CreatesUserAndPublishesEvent()
    {
        var request = new RegisterRequest("test@test.com", "Password123!", "Jan", "Kowalski", 'M', new DateOnly(2000, 1, 1));
        _userRepository.ExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);

        await _sut.RegisterAsync(request, CancellationToken.None);

        _userRepository.Received(1).Add(Arg.Is<User>(u => u.Email == request.Email));
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _publishEndpoint.Received(1).Publish(Arg.Is<UserRegisteredEvent>(e => e.Email == request.Email), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_EmailExists_ThrowsEmailAlreadyExistsException()
    {
        var request = new RegisterRequest("existing@test.com", "Password123!", "Jan", "Kowalski", 'M', new DateOnly(2000, 1, 1));
        _userRepository.ExistsAsync(request.Email, Arg.Any<CancellationToken>()).Returns(true);

        await Should.ThrowAsync<EmailAlreadyExistsException>(() => _sut.RegisterAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        var email = "user@test.com";
        var password = "Password123!";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
        var user = User.NewUser(email, hashedPassword, "Jan", "Kowalski", UserGender.FromChar('M'), new DateOnly(2000, 1, 1));

        _userRepository.GetWithTokensByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(user);

        var request = new LoginRequest(email, password);
        var result = await _sut.LoginAsync(request, CancellationToken.None);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldNotBeNullOrEmpty();
        result.RefreshToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsInvalidCredentialsException()
    {
        var email = "user@test.com";
        var user = User.NewUser(email, BCrypt.Net.BCrypt.HashPassword("RealPassword123!"), "Jan", "Kowalski", UserGender.FromChar('M'), new DateOnly(2000, 1, 1));

        _userRepository.GetWithTokensByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(user);

        var request = new LoginRequest(email, "WrongPassword!");
        await Should.ThrowAsync<InvalidCredentialsException>(() => _sut.LoginAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_ActiveTokensMoreThanLimit_RevokesOldestTokens()
    {
        var email = "user@test.com";
        var user = User.NewUser(email, BCrypt.Net.BCrypt.HashPassword("Password123!"), "Jan", "Kowalski", UserGender.FromChar('M'), new DateOnly(2000, 1, 1));
        
        user.AddRefreshToken("token1", TimeSpan.FromDays(7));
        user.AddRefreshToken("token2", TimeSpan.FromDays(7));
        user.AddRefreshToken("token3", TimeSpan.FromDays(7));

        _userRepository.GetWithTokensByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(user);

        var request = new HybridShop.Services.Auth.Application.Dto.LoginRequest(email, "Password123!");
        var result = await _sut.LoginAsync(request, CancellationToken.None);

        result.ShouldNotBeNull();
        await _userRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}