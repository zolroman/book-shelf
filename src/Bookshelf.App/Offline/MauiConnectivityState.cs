using Bookshelf.Shared.Client;
using Microsoft.Maui.Networking;

namespace Bookshelf.Offline;

public sealed class MauiConnectivityState : IConnectivityState
{
    public MauiConnectivityState()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler? Changed;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
