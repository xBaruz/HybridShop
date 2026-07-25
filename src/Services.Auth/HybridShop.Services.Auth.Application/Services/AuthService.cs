using HybridShop.Services.Auth.Application.Dto;
using HybridShop.Services.Auth.Core.Models;
using HybridShop.Services.Auth.Core.Interfaces;
using HybridShop.Services.Auth.Application.Exceptions;
using HybridShop.BuildingBlocks.EventBus.Events;
using MassTransit;

namespace HybridShop.Services.Auth.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly TokenService _tokenService;
    private readonly IPublishEndpoint _publishEndpoint;

    public AuthService(
        IUserRepository userRepository,
        TokenService tokenService,
        IPublishEndpoint publishEndpoint
    )
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _publishEndpoint = publishEndpoint;
    }

    public async Task RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _userRepository.ExistsAsync(request.Email, cancellationToken);
        if (exists)
            throw new EmailAlreadyExistsException(request.Email);
        
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var userGender = UserGender.FromChar(request.Gender);

        var user = User.NewUser(
            request.Email,
            hashedPassword,
            request.Name,
            request.Lastname,
            userGender,
            request.Birthday
        );

        _userRepository.Add(user);
        await _userRepository.SaveChangesAsync(cancellationToken);

        await _publishEndpoint.Publish(
            new UserRegisteredEvent(user.Id, user.Email, user.Name, user.Lastname),
            cancellationToken
        );
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetWithTokensByEmailAsync(request.Email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) 
            throw new InvalidCredentialsException();

        var activeTokens = user.RefreshTokens
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.CreatedAt)
            .ToList();

        if (activeTokens.Count >= 3)
        {
            var tokensToRevoke = activeTokens.Skip(2); 
            foreach (var token in tokensToRevoke)
            {
                token.Revoke();
            }
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenString = _tokenService.GenerateRefreshTokenString();
        
        user.AddRefreshToken(refreshTokenString, TimeSpan.FromDays(7));
        
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(accessToken, refreshTokenString);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (user is null) 
            throw new UserNotFoundException();

        var activeToken = user.RefreshTokens.FirstOrDefault(t => t.Token == request.RefreshToken);
        if (activeToken == null || !activeToken.IsActive || user.IsDeleted) 
            throw new InvalidTokenException();

        user.RevokeRefreshToken(request.RefreshToken);

        var newAccessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshTokenString = _tokenService.GenerateRefreshTokenString();

        user.AddRefreshToken(newRefreshTokenString, TimeSpan.FromDays(7));
        
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse(newAccessToken, newRefreshTokenString);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetWithTokensByIdAsync(userId, cancellationToken);

        if (user is null) 
            throw new UserNotFoundException();

        user.RevokeAllRefreshTokens();
        
        await _userRepository.SaveChangesAsync(cancellationToken);
    }
}