using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using Avalonia;
using Avalonia.Android;

namespace CamperManagement.Android;

[Activity(
    Label = "CamperManagement.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const int RequestStoragePermissionId = 100;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Berechtigungen überprüfen und anfordern
        RequestStoragePermissions();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    private void RequestStoragePermissions()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.M) return;
        // Überprüfen, ob die Berechtigung bereits erteilt wurde
#pragma warning disable CA1416
        if (CheckSelfPermission(Manifest.Permission.WriteExternalStorage) != Permission.Granted ||
#pragma warning restore CA1416
#pragma warning disable CA1416
            CheckSelfPermission(Manifest.Permission.ReadExternalStorage) != Permission.Granted)
#pragma warning restore CA1416
        {
            // Berechtigung anfordern
#pragma warning disable CA1416
            RequestPermissions(
                [
                    Manifest.Permission.WriteExternalStorage,
                    Manifest.Permission.ReadExternalStorage
                ],
                RequestStoragePermissionId);
#pragma warning restore CA1416
        }
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
#pragma warning disable CA1416
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
#pragma warning restore CA1416

        if (requestCode != RequestStoragePermissionId) return;
        if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
        {
            Toast.MakeText(this, "Speicherzugriff gewährt", ToastLength.Short)?.Show();
        }
        else
        {
            // Optional: Erklären, warum Berechtigung benötigt wird, und erneut anfragen
        }
    }
}
