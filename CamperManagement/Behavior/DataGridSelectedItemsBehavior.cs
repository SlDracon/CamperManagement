using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
namespace CamperManagement.Behavior;

public class DataGridSelectedItemsBehavior : Behavior<DataGrid>
{
    public static readonly StyledProperty<IList?> SelectedItemsProperty = AvaloniaProperty.Register<DataGridSelectedItemsBehavior, IList?>(nameof(SelectedItems));
    public IList? SelectedItems
    {
        get => GetValue(SelectedItemsProperty); set => SetValue(SelectedItemsProperty, value);
    }
    private bool _updating;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedItemsProperty)
        {
            if (change.OldValue is INotifyCollectionChanged old)
                old.CollectionChanged -= OnListChanged;
            if (AssociatedObject != null && change.NewValue is INotifyCollectionChanged value)
                value.CollectionChanged += OnListChanged;
            SyncGrid();
        }
    }
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject!.SelectionChanged += OnSelectionChanged;
        if (SelectedItems is INotifyCollectionChanged list)
            list.CollectionChanged += OnListChanged;
        SyncGrid();
    }
    protected override void OnDetaching()
    {
        AssociatedObject!.SelectionChanged -= OnSelectionChanged;
        if (SelectedItems is INotifyCollectionChanged list)
            list.CollectionChanged -= OnListChanged;
        base.OnDetaching();
    }
    private void OnListChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncGrid();
    private void SyncGrid()
    {
        if (_updating || AssociatedObject == null)
            return;
        _updating = true;
        try
        {
            var values = SelectedItems?.Cast<object>().ToArray() ?? [];
            AssociatedObject.SelectedItems.Clear();
            foreach (var value in values)
                if (!AssociatedObject.SelectedItems.Contains(value))
                    AssociatedObject.SelectedItems.Add(value);
        }
        finally { _updating = false; }
    }
    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updating || SelectedItems == null)
            return;
        _updating = true;
        try
        {
            foreach (var value in e.RemovedItems)
                SelectedItems.Remove(value);
            foreach (var value in e.AddedItems)
                if (!SelectedItems.Contains(value))
                    SelectedItems.Add(value);
        }
        finally { _updating = false; }
    }
}
