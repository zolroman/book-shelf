using Bookshelf.Shared.Client;

namespace Bookshelf.Web.Services;

public sealed class AlwaysOnlineConnectivityState : IConnectivityState
{
    public bool IsOnline => true;

    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}
