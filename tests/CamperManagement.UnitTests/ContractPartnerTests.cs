using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
namespace CamperManagement.UnitTests;

public class ContractPartnerTests
{
    [Theory]
    [InlineData("ZweiterVorname")]
    [InlineData("ZweiterNachname")]
    [InlineData("ZweiteStraße")]
    [InlineData("ZweitePLZ")]
    [InlineData("ZweiterOrt")]
    public void SeparateAddressRequiresCompleteSecondHolder(string field)
    {
        var value = Pair();
        typeof(CamperDisplayModel).GetProperty(field)!.SetValue(value, "  ");
        Assert.Throws<ArgumentException>(() => BillingRules.ValidateCamper(value));
        value.HatZweitenVertragsnehmer = false;
        BillingRules.ValidateCamper(value);
    }
    [Fact]
    public void SharedAddressDoesNotRequireDuplicatedAddressFields()
    {
        var value = Pair();
        value.GemeinsameAdresse = true;
        value.ZweiteStraße = value.ZweitePLZ = value.ZweiterOrt = null;
        BillingRules.ValidateCamper(value);
        Assert.Equal(value.Straße, value.Rechnungsstraße);
        Assert.False(value.NutztZweiteRechnungsadresse);
    }
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task FormSavesBothNamesAndChosenAddress(bool shared, int address)
    {
        var db = new FakeDatabase();
        var original = Pair();
        var vm = new EditCamperViewModel(new(db), db, original) { GemeinsameAdresse = shared, RechnungsadresseIndex = address, ZweiterVorname = "Anna" };
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, db.Updates);
        Assert.Equal("Anna", db.SavedCamper!.ZweiterVorname);
        Assert.Equal(shared, db.SavedCamper.GemeinsameAdresse);
        Assert.Equal(!shared && address == 1, db.SavedCamper.NutztZweiteRechnungsadresse);
        Assert.Equal("Alex", original.ZweiterVorname);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HidingSecondAddressResetsBillingChoice(bool removeHolder)
    {
        var db = new FakeDatabase();
        var vm = new EditCamperViewModel(new(db), db, Pair());
        Assert.Equal(1, vm.RechnungsadresseIndex);
        if (removeHolder) vm.HatZweitenVertragsnehmer = false;
        else vm.GemeinsameAdresse = true;
        Assert.Equal(0, vm.RechnungsadresseIndex);
        Assert.False(vm.HatSeparateAdresse);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(db.SavedCamper!.NutztZweiteRechnungsadresse);
    }
    [Fact]
    public async Task InvalidSecondHolderDoesNotWrite()
    {
        var db = new FakeDatabase();
        var vm = new EditCamperViewModel(new(db), db, Pair()) { ZweiterNachname = "" };
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, db.Updates);
        Assert.Contains("zweiten Vertragsnehmers", vm.StatusMessage);
    }
    [Theory]
    [InlineData("Schneider")]
    [InlineData("Nebenweg")]
    [InlineData("01235")]
    public async Task SearchIncludesSecondHolder(string query)
    {
        var db = new FakeDatabase(); db.Campers.Add(Pair());
        var vm = new CamperViewModel(new(db)) { CamperSearchQuery = query };
        await vm.LoadDataAsync();
        Assert.Single(vm.FilteredCamperList);
    }
    private static CamperDisplayModel Pair()
    {
        var value = Data.Camper();
        value.HatZweitenVertragsnehmer = true; value.GemeinsameAdresse = false;
        value.ZweiterVorname = "Alex"; value.ZweiterNachname = "Schneider";
        value.ZweiteStraße = "Nebenweg 2"; value.ZweitePLZ = "01235"; value.ZweiterOrt = "Zweitort";
        value.RechnungsadresseZweiterVertragsnehmer = true;
        return value;
    }
}
