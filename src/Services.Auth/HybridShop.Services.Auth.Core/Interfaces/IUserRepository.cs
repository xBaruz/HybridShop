namespace HybridShop.Services.Auth.Core.Interfaces;

using HybridShop.Services.Auth.Application.Dto;
using HybridShop.Services.Auth.Core.Dto;
using HybridShop.Services.Auth.Core.Models;

public interface IUserRepository
{
    Task<User?> GetWithTokensByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> GetAdminDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<UserDto?> GetDtoByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AdminUserDto?> GetAdminDtoByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetWithTokensByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<ICollection<UserDto>> BrowseDtoUsers(int skip, int take, CancellationToken cancellationToken = default);
    Task<ICollection<AdminUserDto>> BrowseAdminDtoUsers(int skip, int take, CancellationToken cancellationToken = default);
    void DeleteRefreshToken(RefreshToken token);
    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);
    void Add(User user);
    void Update(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}