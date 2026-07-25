using HybridShop.Services.Auth.Application.Dto;
using HybridShop.Services.Auth.Application.Exceptions;
using HybridShop.Services.Auth.Application.Services;
using HybridShop.Services.Auth.Core.Dto;
using HybridShop.Services.Auth.Core.Interfaces;
using HybridShop.Services.Auth.Core.Models;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HybridShop.Services.Auth.UnitTests.Services;

public class UserServiceTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_userRepository);
    }

    [Fact]
    public async Task GetUserDataAsync_ExistingUser_ReturnsUserDto()
    {
        var userId = Guid.NewGuid();
        var expectedDto = new UserDto { Id = userId, Email = "user@test.com", Name = "Jan" };
        _userRepository.GetDtoByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(expectedDto);

        var result = await _sut.GetUserDataAsync(userId, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(userId);
    }

    [Fact]
    public async Task GetUserDataAsync_UserNotFound_ThrowsUserNotFoundException()
    {
        var userId = Guid.NewGuid();
        _userRepository.GetDtoByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((UserDto?)null);

        await Should.ThrowAsync<UserNotFoundException>(() => _sut.GetUserDataAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task BrowseUsersAsync_InvalidRange_ThrowsInvalidRangeException()
    {
        await Should.ThrowAsync<InvalidRangeException>(() => _sut.BrowseUsersAsync(-1, 10, CancellationToken.None));
        await Should.ThrowAsync<InvalidRangeException>(() => _sut.BrowseUsersAsync(0, 150, CancellationToken.None));
    }

    [Fact]
    public async Task SoftDeleteUserAsync_AlreadyDeleted_ThrowsUserAlreadyDeletedException()
    {
        var userId = Guid.NewGuid();
        var user = User.NewUser("test@test.com", "hash", "Jan", "Kowalski", UserGender.FromChar('M'), new DateOnly(2000, 1, 1));
        user.DeleteUser();

        _userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        await Should.ThrowAsync<UserAlreadyDeletedException>(() => _sut.SoftDeleteUserAsync(userId, CancellationToken.None));
    }
}