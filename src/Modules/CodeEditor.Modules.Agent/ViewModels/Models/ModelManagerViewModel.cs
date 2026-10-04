using System.ClientModel;
using System.Collections.ObjectModel;
using System.Globalization;
using CodeEditor.Core.Settings;
using CodeEditor.Modules.Agent.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

namespace CodeEditor.Modules.Agent.ViewModels.Models;

/// <summary>
/// The model manager: all service models grouped by vendor, with search and a toggle per model; enabled ones appear in
/// the chat model picker (<c>agent.models</c>). The list opens from the cache at once and refreshes from the network.
/// Groups with enabled models, and all groups while searching, are expanded.
/// </summary>
public sealed partial class ModelManagerViewModel(
    ModelDirectory directory,
    IOptionsMonitor<AgentOptions> options,
    ISettingsService settings) : ObservableObject, IDisposable
{
    public const string ModelsKey = "agent.models";

    private readonly HashSet<string> _expanded = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _all = [];
    private CancellationTokenSource? _loading;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Load summary or error, e.g. "312 models, 14 enabled".</summary>
    [ObservableProperty]
    public partial string? Status { get; set; }

    public ObservableCollection<ModelRowViewModel> Rows { get; } = [];

    public void Dispose() => _loading?.Cancel();

    /// <summary>Shows the cached list at once and the fresh one when it arrives.</summary>
    [RelayCommand]
    private async Task OpenAsync()
    {
        Search = string.Empty;
        _all = Merge(directory.Cached());
        _expanded.Clear();
        _expanded.UnionWith(Enabled().Select(ModelVendors.VendorOf));
        Rebuild();
        IsOpen = true;
        await RefreshAsync();
    }

    [RelayCommand]
    private void Close()
    {
        _loading?.Cancel();
        IsOpen = false;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _loading?.Cancel();
        var loading = _loading = new CancellationTokenSource();
        IsLoading = true;
        Status = Strings.ModelsLoading;
        try
        {
            _all = Merge(await directory.RefreshAsync(loading.Token));
            Rebuild();
            Status = Summary();
        }
        catch (OperationCanceledException) when (loading.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (exception is AgentConfigurationException or ClientResultException or HttpRequestException
            or IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.ModelsLoadFailed, exception.Message);
        }
        finally
        {
            Finish(loading);
        }
    }

    // A cancelled load must not touch the state of a newer one, which has already replaced the token source.
    private void Finish(CancellationTokenSource loading)
    {
        if (ReferenceEquals(_loading, loading))
        {
            _loading = null;
            IsLoading = false;
        }

        loading.Dispose();
    }

    /// <summary>Adds a model missing from the service list, typed into the search as <c>vendor/model</c>.</summary>
    [RelayCommand(CanExecute = nameof(CanAddCustom))]
    private void AddCustom()
    {
        var model = Search.Trim();
        _all = Merge([.. _all, model]);
        Save([.. Enabled(), model]);
        Search = string.Empty;
        _expanded.Add(ModelVendors.VendorOf(model));
        Rebuild();
    }

    private bool CanAddCustom()
    {
        var model = Search.Trim();
        return ModelVendors.VendorOf(model).Length > 0 && !_all.Contains(model, StringComparer.OrdinalIgnoreCase);
    }

    partial void OnSearchChanged(string value)
    {
        AddCustomCommand.NotifyCanExecuteChanged();
        Rebuild();
    }

    private void Rebuild()
    {
        var enabled = Enabled().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var current = options.CurrentValue.Model;
        var query = Search.Trim();
        var groups = _all
            .Where(model => query.Length == 0 || model.Contains(query, StringComparison.OrdinalIgnoreCase))
            .GroupBy(ModelVendors.VendorOf, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => ModelVendors.Rank(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        Rows.Clear();
        foreach (var group in groups)
        {
            var expanded = query.Length > 0 || _expanded.Contains(group.Key);
            var vendor = ModelRowViewModel.Vendor(group.Key, ModelVendors.Title(group.Key), expanded, OnVendorToggled);
            vendor.Detail = string.Format(CultureInfo.CurrentCulture, Strings.ModelsVendorCount, group.Count(enabled.Contains), group.Count());
            Rows.Add(vendor);
            if (!expanded)
            {
                continue;
            }

            foreach (var model in group.OrderBy(model => model, StringComparer.OrdinalIgnoreCase))
            {
                var row = ModelRowViewModel.Model(model, ModelVendors.ShortName(model), enabled.Contains(model), OnModelToggled);
                row.IsCurrent = string.Equals(model, current, StringComparison.OrdinalIgnoreCase);
                Rows.Add(row);
            }
        }

        Status ??= Summary();
    }

    private void OnVendorToggled(ModelRowViewModel vendor)
    {
        if (!vendor.IsChecked)
        {
            _expanded.Remove(vendor.Id);
        }
        else
        {
            _expanded.Add(vendor.Id);
        }

        Rebuild();
    }

    private void OnModelToggled(ModelRowViewModel row)
    {
        var enabled = Enabled().Where(model => !string.Equals(model, row.Id, StringComparison.OrdinalIgnoreCase));
        Save(row.IsChecked ? [.. enabled, row.Id] : [.. enabled]);
        UpdateVendorCount(ModelVendors.VendorOf(row.Id));
        Status = Summary();
    }

    // The toggled model's group is expanded, so all its models are in Rows; recount without rebuilding the list.
    private void UpdateVendorCount(string vendor)
    {
        var models = Rows.Where(item => !item.IsVendor && string.Equals(ModelVendors.VendorOf(item.Id), vendor, StringComparison.OrdinalIgnoreCase)).ToList();
        if (Rows.FirstOrDefault(item => item.IsVendor && string.Equals(item.Id, vendor, StringComparison.OrdinalIgnoreCase)) is { } header)
        {
            header.Detail = string.Format(CultureInfo.CurrentCulture, Strings.ModelsVendorCount, models.Count(item => item.IsChecked), models.Count);
        }
    }

    /// <summary>
    /// Leaves only the supported models in the picker (ADR 0017). The current model stays available even if it is not
    /// one of them: changing the model is a separate user decision.
    /// </summary>
    [RelayCommand]
    private void UseSupported()
    {
        Save(ModelCatalog.DefaultModels);
        Rebuild();
        Status = Summary();
    }

    // The picker uses the manager's order: by vendor, then by name.
    private void Save(IReadOnlyList<string> models)
    {
        string[] ordered = [.. models.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => ModelVendors.Rank(ModelVendors.VendorOf(model)))
            .ThenBy(model => model, StringComparer.OrdinalIgnoreCase)];
        if (!settings.TrySetUserValue(ModelsKey, ordered, out var error))
        {
            Status = error;
        }
    }

    private IReadOnlyList<string> Enabled() => ModelCatalog.ModelsFor(options.CurrentValue);

    // Enabled and current models stay in the list even if the service did not return them.
    private IReadOnlyList<string> Merge(IReadOnlyList<string> models) =>
        [.. models.Concat(Enabled()).Distinct(StringComparer.OrdinalIgnoreCase)];

    private string Summary() =>
        string.Format(CultureInfo.CurrentCulture, Strings.ModelsSummary, _all.Count, Enabled().Count);
}
