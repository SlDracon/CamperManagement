using System.Globalization;
using CamperManagement.Models;
using CamperManagement.Services;
using CamperManagement.Tests;
using CamperManagement.ViewModels;
namespace CamperManagement.UnitTests;

public class BillingTests
{
    [Theory]
    [InlineData("12.5", "0.5", "6.25")]
    [InlineData("12.5", "8", "100")]
    [InlineData("1", "1.005", "1.01")]
    [InlineData("1", "1.015", "1.02")]
    [InlineData("1", "1.004", "1")]
    [InlineData("1", "1.006", "1.01")]
    [InlineData("-1", "1.005", "-1.01")]
    [InlineData("0", "8", "0")]
    public void R01_R05_CentRounding(string use, string factor, string amount) => Assert.Equal(D(amount), BillingRules.Amount(D(use), D(factor)));
    private static decimal D(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
    [Fact]
    public void R05_R10_RoundPositionsBeforeSumming()
    {
        Assert.Equal(2.02m, BillingRules.Amount(1, 1.005m) + BillingRules.Amount(1, 1.005m));
        Assert.Equal(3m, new KostenEintrag { PlatzNr = "1", Vorname = "A", Nachname = "B", WasserBetrag = 1, StromBetrag = 2, Vertragskosten = 100 }.Gesamtbetrag);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R02_R03_FactorAloneDoesNotRecalculate(bool edit)
    {
        var db = new FakeDatabase();
        var main = new MainViewModel(db);
        InvoiceFormViewModel vm = edit ? new EditRechnungViewModel(main, db, Data.Invoice()) : new AddRechnungViewModel(main, db);
        vm.Alt = 0;
        vm.Neu = 10;
        vm.Faktor = 0.5m;
        vm.Neu = 11;
        Assert.Equal(5.5m, vm.Betrag);
        vm.Faktor = 0.6m;
        Assert.Equal(5.5m, vm.Betrag);
        vm.Neu = 10;
        Assert.Equal(6m, vm.Betrag);
        vm.Alt = 1;
        Assert.Equal(5.4m, vm.Betrag);
    }
    [Theory]
    [InlineData("Wasser", "8")]
    [InlineData("Strom", "0.5")]
    public async Task R04_ArtChangeOverridesManualFactor(string art, string expected)
    {
        var db = new FakeDatabase();
        var vm = new AddRechnungViewModel(new(db), db);
        vm.SelectedArt = art == "Wasser" ? "Strom" : "Wasser";
        await vm.SelectionTask;
        vm.Faktor = 99;
        vm.SelectedArt = art;
        await vm.SelectionTask;
        Assert.Equal(D(expected), vm.Faktor);
    }
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public async Task R06_ReverseReadingWarnsAndAllowsSave(int reading, bool warning)
    {
        var db = new FakeDatabase();
        var vm = new AddRechnungViewModel(new(db), db);
        vm.SelectedPlatznummer = "1";
        await vm.SelectionTask;
        vm.Neu = reading;
        Assert.Equal(warning, vm.WarningMessage != null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, db.Adds);
        Assert.Equal(reading - 10, db.SavedInvoice!.Verbrauch);
    }
    [Theory]
    [InlineData("Wasser", "1,250")]
    [InlineData("wasser", "1,250")]
    [InlineData("Strom", "1,25")]
    [InlineData(null, "1,25")]
    public void R07_R08_GermanDisplay(string? art, string expected)
    {
        var r = Data.Invoice();
        r.Art = art;
        r.Alt = 1.25m;
        Assert.Equal(expected, r.AltDisplay);
    }
    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("")]
    public void R09_CultureIndependent(string culture)
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(1.01m, BillingRules.Amount(1, 1.005m));
            Assert.Equal("12,500", Data.Invoice().VerbrauchDisplay);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
    [Fact] public void R10_OverflowExplicit() => Assert.Throws<OverflowException>(() => BillingRules.Amount(decimal.MaxValue, 2));
    [Theory][InlineData("1,25", true)][InlineData("1.25", true)][InlineData("-2", true)][InlineData("", false)][InlineData("abc", false)][InlineData("1,2,3", false)] public void V03_ParseNumbers(string text, bool valid) => Assert.Equal(valid, BillingRules.TryDecimal(text, out _));
    [Theory][InlineData(2024, false)][InlineData(2025, true)][InlineData(2026, true)][InlineData(2027, false)] public void V03_YearRange(int year, bool valid) => Assert.Equal(valid, BillingRules.IsAllowedYear(year, new FixedClock()));
    [Fact]
    public void V03_NewYearMovesWindow()
    {
        var clock = new FixedClock { Now = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        Assert.False(BillingRules.IsAllowedYear(2025, clock));
        Assert.True(BillingRules.IsAllowedYear(2027, clock));
    }

    [Fact]
    public void X08_ExplicitTestConnectionNeverFallsBack()
    {
        Assert.Throws<ArgumentException>(() => new DatabaseService(""));
        Assert.Throws<ArgumentException>(() => new DatabaseService(null!));
    }
}
