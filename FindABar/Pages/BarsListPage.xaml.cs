using FindABar.Models;
using Microsoft.Maui.Devices.Sensors;

namespace FindABar.Pages;

public partial class BarsListPage : ContentPage
{
    public BarsListPage()
    {
        InitializeComponent();
        LoadBars();
    }

    private async void LoadBars()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("Erreur", "Permission de localisation refusée", "OK");
            return;
        }

        Console.WriteLine("Localisation autorisée, récupération...");

        var location = await Geolocation.GetLastKnownLocationAsync();

        if (location == null)
        {
            location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.High,
                Timeout = TimeSpan.FromSeconds(10)
            });
        }

        if (location == null)
        {
            await DisplayAlert("Erreur", "Impossible de déterminer la position", "OK");
            return;
        }

        double latitude = location.Latitude;
        double longitude = location.Longitude;

        Console.WriteLine($"Coordonnées récupérées : {latitude}, {longitude}");

        var placesService = new Services.PlacesService();
        var bars = await placesService.GetNearbyBarsAsync(latitude, longitude);

        BarsCollectionView.ItemsSource = bars;
    }
}