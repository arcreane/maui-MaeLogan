using FindABar.Models;
using FindABar.Services;
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

        // calculer la distance réelle pour chaque bar
        foreach (var bar in bars)
        {
            bar.Distance = GeoHelper.GetDistanceKm(latitude, longitude, bar.Latitude, bar.Longitude);
        }

        // trier
        var barsSorted = bars.OrderBy(b => b.Distance).ToList();

        BarsCollectionView.ItemsSource = barsSorted;

    }
    
    private async void OnBarSelected(object sender, SelectionChangedEventArgs e)
    {
        Console.WriteLine("CLICK ON BAR");
        if (e.CurrentSelection.FirstOrDefault() is Bar selectedBar)
        {
            var url = $"https://www.google.com/maps/search/?api=1&query={selectedBar.Latitude},{selectedBar.Longitude}";
            try
            {
                await Launcher.Default.OpenAsync(url);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erreur", "Impossible d'ouvrir Google Maps", "OK");
                Console.WriteLine(ex);
            }

            // désélectionner pour éviter bug de sélection bloquée
            BarsCollectionView.SelectedItem = null;
        }
    }
}