using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// A connection in the connections panel: its name, address and key state, and when expanded the models its service
/// lists. The models the user switched on are the ones in the model picker; a model the service does not list can be
/// added by its id.
/// </summary>
public sealed partial class ConnectionRowViewModel : ObservableObject
{
    private readonly ConnectionServices _services;
    private IReadOnlyList<ServiceModel> _listed = [];
    private readonly ConnectionModelActions _actions;
    private List<ConnectionModelRowViewModel> _rows = [];
    private CancellationTokenSource? _loading;
    private bool _legacyMaxTokens;

    public ConnectionRowViewModel(AgentConnection connection, ConnectionServices services)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _services = services;
        _actions = new ConnectionModelActions(_ => Save(), Use, CheckAsync, report => _legacyMaxTokens |= report.LegacyMaxTokens);
        Id = connection.Id;
        Update(connection);
    }

    public string Id { get; }

    [ObservableProperty]
    public partial string Title { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Endpoint { get; private set; } = string.Empty;

    /// <summary>"key set", "no key" or "no key needed".</summary>
    [ObservableProperty]
    public partial string KeyText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string? Status { get; private set; }

    /// <summary>Narrows the model list; a model id the service does not list can be added from here.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddModelCommand))]
    public partial string Filter { get; set; } = string.Empty;

    public ObservableCollection<ConnectionModelRowViewModel> Models { get; } = [];

    /// <summary>The settings changed: the name, the address and the key state follow; the model rows stay as the user left them.</summary>
    public void Update(AgentConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Title = connection.Name is { Length: > 0 } name ? name : connection.Id;
        Endpoint = connection.Endpoint;
        KeyText = connection.Keyless ? Strings.ConnectionKeyless : _services.Store.HasKey(connection) ? Strings.ConnectionKeySet : Strings.ConnectionKeyMissing;
        var current = CurrentModel();
        foreach (var row in _rows)
        {
            row.IsCurrent = string.Equals(row.Id, current, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Shows the models the check of a new connection has just listed.</summary>
    public void Show(IReadOnlyList<ServiceModel> models)
    {
        _listed = models;
        IsExpanded = true;
        Rebuild();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_services.Store.Find(Id) is not { } connection)
        {
            return;
        }

        _loading?.Cancel();
        var loading = _loading = new CancellationTokenSource();
        IsLoading = true;
        Status = Strings.ModelsLoading;
        try
        {
            _listed = await _services.Probe.ListModelsAsync(connection, connection.Keyless ? null : _services.Store.Key(connection), loading.Token);
            Rebuild();
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (ConnectionErrors.IsExpected(exception))
        {
            // The models already in the picker stay usable while the service is away.
            Rebuild();
            Status = string.Format(CultureInfo.CurrentCulture, Strings.ModelsLoadFailed, ConnectionErrors.Describe(exception, connection.Endpoint));
        }
        finally
        {
            if (ReferenceEquals(_loading, loading))
            {
                (_loading, IsLoading) = (null, false);
            }

            loading.Dispose();
        }
    }

    [RelayCommand]
    private void SetKey()
    {
        if (_services.Store.Find(Id) is not { } connection
            || _services.Dialogs.PromptSecret(Strings.ApiKeyDialogTitle, string.Format(CultureInfo.CurrentCulture, Strings.ConnectionKeyPrompt, Title)) is not { Length: > 0 } key)
        {
            return;
        }

        _services.Store.SetKey(Id, key.Trim());
        Update(connection);
        _services.StatusBar.Message = Strings.ApiKeySaved;
    }

    [RelayCommand]
    private void Remove()
    {
        if (_services.Dialogs.Confirm(string.Format(CultureInfo.CurrentCulture, Strings.ConnectionRemoveQuestion, Title), Strings.ConnectionRemoveDetails, Strings.Delete)
            && !_services.Store.TryRemove(Id, out var error))
        {
            Status = error;
        }
    }

    /// <summary>Adds a model by the id typed into the filter and puts it into the picker.</summary>
    [RelayCommand(CanExecute = nameof(CanAddModel))]
    private void AddModel()
    {
        _listed = [.. _listed, new ServiceModel(Filter.Trim())];
        Filter = string.Empty;
        Rebuild();
        _rows.First(row => row.Id == _listed[^1].Id).IsEnabled = true;
    }

    private bool CanAddModel() => Filter.Trim() is { Length: > 0 } id && !_rows.Any(row => string.Equals(row.Id, id, StringComparison.OrdinalIgnoreCase));

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && _rows.Count == 0 && !IsLoading)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnFilterChanged(string value) => ShowRows();

    // The models of the picker first, then the rest by name; a described model the service no longer lists stays.
    private void Rebuild()
    {
        var connection = _services.Store.Find(Id);
        var described = connection?.Models ?? [];
        var current = CurrentModel();
        _rows =
        [
            .. _listed.Concat(described.Select(static model => new ServiceModel(model.Id)))
                .DistinctBy(static model => model.Id, StringComparer.OrdinalIgnoreCase)
                .Select(model => new ConnectionModelRowViewModel(
                    model,
                    described.FirstOrDefault(description => string.Equals(description.Id, model.Id, StringComparison.OrdinalIgnoreCase)),
                    string.Equals(model.Id, current, StringComparison.OrdinalIgnoreCase),
                    _actions))
                .OrderByDescending(static row => row.IsEnabled)
                .ThenBy(static row => row.Id, StringComparer.OrdinalIgnoreCase),
        ];
        ShowRows();
        Status = Summary();
    }

    private void ShowRows()
    {
        var filter = Filter.Trim();
        Models.Clear();
        foreach (var row in _rows.Where(row => filter.Length == 0 || row.Id.Contains(filter, StringComparison.OrdinalIgnoreCase) || row.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            Models.Add(row);
        }
    }

    private void Save()
    {
        if (_services.Store.Find(Id) is not { } connection)
        {
            return;
        }

        var saved = new AgentConnection
        {
            Id = connection.Id, Name = connection.Name, Endpoint = connection.Endpoint, Api = connection.Api, Headers = connection.Headers,
            Keyless = connection.Keyless, LegacyMaxTokens = connection.LegacyMaxTokens || _legacyMaxTokens,
            Models = [.. _rows.Select(static row => row.ToDescription()).OfType<ModelDescription>()],
        };
        Status = _services.Store.TrySave(saved, out var error) ? Summary() : error;
    }

    private void Use(ConnectionModelRowViewModel row)
    {
        Save();
        var reference = Id + ModelRoutes.Separator + row.Id;
        _services.StatusBar.Message = _services.Settings.TrySetUserValue(ModelSettingsViewModel.ModelKey, reference, out var error)
            ? string.Format(CultureInfo.CurrentCulture, Strings.ModelChanged, row.Name.Length > 0 ? row.Name : row.Id)
            : error;
    }

    private Task<ModelProbeReport> CheckAsync(ConnectionModelRowViewModel row, CancellationToken cancellationToken) =>
        _services.Store.Find(Id) is { } connection
            ? _services.ModelProbe.RunAsync(connection, connection.Keyless ? null : _services.Store.Key(connection), row.Id, cancellationToken)
            : Task.FromResult(new ModelProbeReport([]));

    // The conversation model's id at the service, if it is a model of this connection.
    private string? CurrentModel() =>
        ModelRoutes.For(_services.Options.CurrentValue) is { Connection: { } connection } route && string.Equals(connection.Id, Id, StringComparison.OrdinalIgnoreCase)
            ? route.Model
            : null;

    private string Summary() =>
        string.Format(CultureInfo.CurrentCulture, Strings.ConnectionModelsSummary, _rows.Count, _rows.Count(static row => row.IsEnabled));
}
