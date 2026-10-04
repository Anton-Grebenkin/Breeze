using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using CodeEditor.Shell.Editors;

namespace CodeEditor.Shell.Wpf.Views;

/// <summary>
/// Editor area: tab groups in equal columns with splitters between them (ADR 0031). Each group view is created once and
/// never re-parented when the list changes, so open editors keep their caret and scroll position.
/// </summary>
public sealed partial class EditorAreaView
{
    private const double MinGroupWidth = 160;
    private const double SplitterWidth = 4;

    private readonly Dictionary<EditorGroupViewModel, EditorGroupView> _views = [];
    private EditorAreaHost? _host;

    public EditorAreaView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_host is not null)
        {
            _host.Editors.Groups.CollectionChanged -= OnGroupsChanged;
        }

        _host = DataContext as EditorAreaHost;
        if (_host is not null)
        {
            _host.Editors.Groups.CollectionChanged += OnGroupsChanged;
        }

        Arrange();
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Arrange();

    private void Arrange()
    {
        IReadOnlyList<EditorGroupViewModel> groups = _host?.Editors.Groups ?? [];
        foreach (var removed in _views.Keys.Except(groups).ToList())
        {
            GroupsHost.Children.Remove(_views[removed]);
            _views.Remove(removed);
        }

        foreach (var splitter in GroupsHost.Children.OfType<GridSplitter>().ToList())
        {
            GroupsHost.Children.Remove(splitter);
        }

        GroupsHost.ColumnDefinitions.Clear();
        for (var index = 0; index < groups.Count; index++)
        {
            if (index > 0)
            {
                GroupsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                AddSplitter(GroupsHost.ColumnDefinitions.Count - 1);
            }

            GroupsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = MinGroupWidth });
            Grid.SetColumn(ViewOf(groups[index]), GroupsHost.ColumnDefinitions.Count - 1);
        }
    }

    private EditorGroupView ViewOf(EditorGroupViewModel group)
    {
        if (!_views.TryGetValue(group, out var view))
        {
            view = new EditorGroupView { DataContext = group, Host = _host };
            _views[group] = view;
            GroupsHost.Children.Add(view);
        }

        return view;
    }

    private void AddSplitter(int column)
    {
        var splitter = new GridSplitter
        {
            Width = SplitterWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Style = (Style)FindResource("GridSplitter.Thin"),
        };
        Grid.SetColumn(splitter, column);
        GroupsHost.Children.Add(splitter);
    }
}
