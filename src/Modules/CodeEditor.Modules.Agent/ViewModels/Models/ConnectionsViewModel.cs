using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// The connections panel over the chat (ADR 0055): the user's own model services and a form that adds one — name,
/// address, key — after checking that the address answers with a model list. Nothing here needs the settings file
/// to be edited by hand.
/// </summary>
public sealed partial class ConnectionsViewModel : ObservableObject, IDisposable
{
    private const string LocalScheme = "http://";
    private const string DefaultScheme = "https://";

    private readonly ConnectionServices _services;
    private readonly IDisposable? _subscription;
    private CancellationTokenSource? _checking;

    public ConnectionsViewModel(ConnectionServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        _subscription = services.Options.OnChange(_ => Sync());
        Sync();
    }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConnections))]
    public partial int Count { get; private set; }

    public bool HasConnections => Count > 0;

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand), nameof(AddUncheckedCommand))]
    public partial string Endpoint { get; set; } = string.Empty;

    /// <summary>The key typed into the form; it goes to the secret store, never to the settings.</summary>
    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsKeyless { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand), nameof(AddUncheckedCommand))]
    public partial bool IsChecking { get; private set; }

    /// <summary>The last check failed: the connection can still be added as it is.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddUncheckedCommand))]
    public partial bool CanSkipCheck { get; private set; }

    [ObservableProperty]
    public partial string? Status { get; private set; }

    public void Dispose()
    {
        _subscription?.Dispose();
        _checking?.Cancel();
    }

    [RelayCommand]
    private void Open()
    {
        Sync();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        _checking?.Cancel();
        IsOpen = false;
    }

    /// <summary>Asks the address for its models and adds the connection when it answers.</summary>
    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var connection = Draft();
        _checking?.Cancel();
        var checking = _checking = new CancellationTokenSource();
        (IsChecking, CanSkipCheck, Status) = (true, false, Strings.ConnectionChecking);
        try
        {
            var models = await _services.Probe.ListModelsAsync(connection, IsKeyless ? null : Key.Trim(), checking.Token);
            if (Save(connection) is { } row)
            {
                row.Show(models);
                Status = string.Format(CultureInfo.CurrentCulture, Strings.ConnectionAdded, row.Title, models.Count);
            }
        }
        catch (OperationCanceledException) when (checking.IsCancellationRequested)
        {
            Status = null;
        }
        catch (Exception exception) when (ConnectionErrors.IsExpected(exception))
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.ConnectionCheckFailed, ConnectionErrors.Describe(exception, connection.Endpoint));
            CanSkipCheck = true;
        }
        finally
        {
            if (ReferenceEquals(_checking, checking))
            {
                (_checking, IsChecking) = (null, false);
            }

            checking.Dispose();
        }
    }

    private bool CanAdd() => !IsChecking && Endpoint.Trim().Length > 0;

    /// <summary>Adds the connection although its check failed: a gateway may serve models without listing them.</summary>
    [RelayCommand(CanExecute = nameof(CanAddUnchecked))]
    private void AddUnchecked()
    {
        if (Save(Draft()) is { } row)
        {
            row.IsExpanded = true;
            Status = string.Format(CultureInfo.CurrentCulture, Strings.ConnectionAddedUnchecked, row.Title);
        }
    }

    private bool CanAddUnchecked() => CanSkipCheck && CanAdd();

    partial void OnEndpointChanged(string value) => CanSkipCheck = false;

    // "gateway.example/v1" is completed like an address in a browser: http for this machine, https otherwise.
    private AgentConnection Draft()
    {
        var endpoint = Endpoint.Trim();
        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            var local = endpoint.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || endpoint.StartsWith("127.0.0.1", StringComparison.Ordinal);
            endpoint = (local ? LocalScheme : DefaultScheme) + endpoint;
        }

        return new AgentConnection
        {
            Id = _services.Store.NewId(Name, endpoint),
            Name = Name.Trim() is { Length: > 0 } name ? name : null,
            Endpoint = endpoint,
            Keyless = IsKeyless,
        };
    }

    private ConnectionRowViewModel? Save(AgentConnection connection)
    {
        if (!_services.Store.TrySave(connection, out var error))
        {
            Status = error;
            return null;
        }

        if (!connection.Keyless && Key.Trim() is { Length: > 0 } key)
        {
            _services.Store.SetKey(connection.Id, key);
        }

        (Name, Endpoint, Key, IsKeyless, CanSkipCheck) = (string.Empty, string.Empty, string.Empty, false, false);
        Sync();
        return Connections.FirstOrDefault(row => row.Id == connection.Id);
    }

    // Rows follow the settings without being rebuilt: an expanded list and a half-typed name must survive a save.
    private void Sync()
    {
        var connections = _services.Store.Connections;
        for (var index = Connections.Count - 1; index >= 0; index--)
        {
            if (!connections.Any(connection => string.Equals(connection.Id, Connections[index].Id, StringComparison.OrdinalIgnoreCase)))
            {
                Connections.RemoveAt(index);
            }
        }

        foreach (var connection in connections)
        {
            if (Connections.FirstOrDefault(row => string.Equals(row.Id, connection.Id, StringComparison.OrdinalIgnoreCase)) is { } existing)
            {
                existing.Update(connection);
            }
            else
            {
                Connections.Add(new ConnectionRowViewModel(connection, _services));
            }
        }

        Count = Connections.Count;
    }
}
