using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
using CamperManagement.Views;
namespace CamperManagement.UiTests;

public class ContractPartnerTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BothFormsBindSecondHolderAddressAndBillingChoice(bool adding)
    {
        var db = new FakeDatabase();
        CamperFormViewModel vm = adding ? new AddCamperViewModel(new(db), db) : new EditCamperViewModel(new(db), db, Data.Camper());
        vm.SelectedPlatznummer = "1"; vm.Vorname = "Anna"; vm.Nachname = "Müller"; vm.Straße = "Weg 1"; vm.Plz = "01234"; vm.Ort = "Ort";
        UserControl view = adding ? new AddCamperView() : new EditCamperView(); view.DataContext = vm;
        var window = new Window { Content = view, Width = 390, Height = 720 }; window.Show();
        try
        {
            var fields = view.GetVisualDescendants().OfType<ContractPartnerFields>().Single();
            var details = fields.FindControl<StackPanel>("SecondPartnerDetails")!;
            var address = fields.FindControl<StackPanel>("SecondAddressDetails")!;
            Assert.False(details.IsVisible);
            fields.FindControl<CheckBox>("SecondPartnerToggle")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs(); Assert.True(details.IsVisible); Assert.False(address.IsVisible);
            fields.FindControl<TextBox>("SecondFirstName")!.Text = "Alex"; vm.ZweiterNachname = "Schneider";
            fields.FindControl<CheckBox>("SharedAddressToggle")!.IsChecked = false;
            Dispatcher.UIThread.RunJobs(); Assert.True(address.IsVisible);
            vm.ZweiteStraße = "Nebenweg 2"; vm.ZweitePlz = "01235"; vm.ZweiterOrt = "Zweitort";
            fields.FindControl<ComboBox>("BillingAddressChoice")!.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            await vm.SaveCommand.ExecuteAsync(null);
            Assert.Equal("Alex", db.SavedCamper!.ZweiterVorname); Assert.True(db.SavedCamper.NutztZweiteRechnungsadresse);
            fields.FindControl<CheckBox>("SharedAddressToggle")!.IsChecked = true;
            Dispatcher.UIThread.RunJobs(); Assert.False(address.IsVisible); Assert.Equal(0, vm.RechnungsadresseIndex);
            fields.FindControl<CheckBox>("SecondPartnerToggle")!.IsChecked = false;
            Dispatcher.UIThread.RunJobs(); Assert.False(details.IsVisible);
        }
        finally { window.Close(); }
    }
}
