namespace Bookshelf.Shared.Client;

public interface IConnectivityState
{
    bool IsOnline { get; }

    event EventHandler? Changed;
}
