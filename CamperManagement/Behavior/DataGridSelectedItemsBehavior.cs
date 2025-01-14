using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace CamperManagement.Behavior
{
    public class DataGridSelectedItemsBehavior : Behavior<DataGrid>
    {
        public static readonly StyledProperty<IList?> SelectedItemsProperty =
            AvaloniaProperty.Register<DataGridSelectedItemsBehavior, IList?>(nameof(SelectedItems));

        public IList? SelectedItems
        {
            get => GetValue(SelectedItemsProperty);
            set => SetValue(SelectedItemsProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.SelectionChanged += OnSelectionChanged;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject != null)
            {
                AssociatedObject.SelectionChanged -= OnSelectionChanged;
            }
        }

        private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (SelectedItems is IList targetList)
            {
                foreach (var item in e.AddedItems)
                {
                    targetList.Add(item);
                }

                foreach (var item in e.RemovedItems)
                {
                    targetList.Remove(item);
                }
            }
        }
    }
}
