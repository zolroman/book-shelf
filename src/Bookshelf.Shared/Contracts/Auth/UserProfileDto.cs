namespace Bookshelf.Shared.Contracts.Auth;

public sealed record UserProfileDto(
    int Id,
    string Login,
    string DisplayName);
