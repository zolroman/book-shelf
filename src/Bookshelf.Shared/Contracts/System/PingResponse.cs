namespace Bookshelf.Shared.Contracts.System;

public sealed record PingResponse(
    string Service,
    DateTimeOffset UtcTime);
