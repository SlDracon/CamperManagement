using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CamperManagement.Tests;
using CamperManagement.Services;
using CamperManagement.ViewModels;
using CamperManagement.Views;
namespace CamperManagement.UiTests;

public class KeyboardTests
{
    [AvaloniaTheory]
    [InlineData(true, PhysicalKey.Enter)]
    [InlineData(true, PhysicalKey.F2)]
    [InlineData(false, PhysicalKey.Enter)]
    [InlineData(false, PhysicalKey.F2)]
    public async Task SelectedRowCanBeEditedWithKeyboard(bool invoice, PhysicalKey key)
    {
        var db = new FakeDatabase();
        db.Campers.Add(Data.Camper());
        db.Invoices.Add(Data.Invoice());
        var main = new MainViewModel(db);
        var tabs = (TabViewModel)main.CurrentView;
        var vm = (ViewModelBase)(invoice ? tabs.RechnungenView : tabs.CamperView);
        await vm.InitializeAsync();
        UserControl view = invoice ? new RechnungenView() : new CamperView();
        view.DataContext = vm;
        var window = new Window { Content = view, Width = 1000, Height = 600 };
        window.Show();
        try
        {
            var grid = view.GetVisualDescendants().OfType<DataGrid>().Single();
            Assert.True(grid.Focusable);
            grid.SelectedIndex = 0;
            Assert.True(grid.Focus());
            window.KeyPressQwerty(key, RawInputModifiers.None);
            window.KeyReleaseQwerty(key, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            await main.NavigationTask;
            if (invoice) Assert.IsType<EditRechnungViewModel>(main.CurrentView);
            else Assert.IsType<EditCamperViewModel>(main.CurrentView);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TabMovesFromSearchIntoTable(bool invoice)
    {
        var main = new MainViewModel(new FakeDatabase());
        UserControl view = invoice ? new RechnungenView { DataContext = new RechnungenViewModel(main) } : new CamperView { DataContext = new CamperViewModel(main) };
        var window = new Window { Content = view };
        window.Show();
        try
        {
            var search = view.GetLogicalDescendants().OfType<TextBox>().Single();
            var grid = view.GetLogicalDescendants().OfType<DataGrid>().Single();
            search.Focus();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(grid.IsKeyboardFocusWithin);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AddButtonsAndFormInputsHaveAccessibleNames()
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        var views = new UserControl[]
        {
            new CamperView { DataContext = new CamperViewModel(main) },
            new RechnungenView { DataContext = new RechnungenViewModel(main) },
            new AddRechnungView { DataContext = new AddRechnungViewModel(main, db) },
            new AddCamperView { DataContext = new AddCamperViewModel(main, db) },
            new SettingsView { DataContext = new SettingsViewModel(db) },
            new DatabaseConnectionView { DataContext = new DatabaseConnectionViewModel(new DatabaseConfiguration(Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".json"), () => null), () => Task.CompletedTask) }
        };
        foreach (var view in views)
        {
            var window = new Window { Content = view };
            window.Show();
            try
            {
                var controls = view.GetLogicalDescendants().OfType<Control>().Where(c => c is TextBox or ComboBox || c is Button b && Equals(b.Content, "+"));
                foreach (var control in controls) Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)), $"{view.GetType().Name}: {control.GetType().Name} {control.Name}");
            }
            finally { window.Close(); }
        }
    }
}
