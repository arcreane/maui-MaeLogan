using FindABar.Models;
using FindABar.Services;
using Microsoft.Maui.Devices.Sensors;

namespace FindABar.Pages;

public partial class RandomBarPopup : ContentPage
{
    private readonly Bar _selectedBar;
    private readonly double _userLatitude;
    private readonly double _userLongitude;

    private bool _isCompassActive;
    private bool _isAnimating;
    private double _lastAngle;
    private double _targetBearing;

    // Filtre pour lisser les lectures de la boussole
    private readonly Queue<double> _compassReadings = new();
    private readonly int _smoothingWindowSize = 5;

    public RandomBarPopup(Bar selectedBar, double userLatitude, double userLongitude)
    {
        InitializeComponent();

        _selectedBar = selectedBar;
        _userLatitude = userLatitude;
        _userLongitude = userLongitude;

        // Calcul du cap vers le bar
        _targetBearing = CalculateBearing(_userLatitude, _userLongitude,
                                          _selectedBar.Latitude, _selectedBar.Longitude);

        InitializeBarInfo();
        InitializeCompass();
    }

    private void InitializeBarInfo()
    {
        BarNameLabel.Text = _selectedBar.Name;
        BarAddressLabel.Text = _selectedBar.Address;
        BarDistanceLabel.Text = $"{_selectedBar.Distance:F2} km";
        DirectionLabel.Text = $"Direction: {_targetBearing:F0}°";
        CompassDirectionLabel.Text = GetCompassDirection(_targetBearing);
    }

    private async void InitializeCompass()
    {
        if (Compass.Default.IsSupported)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status == PermissionStatus.Granted)
            {
                Compass.Default.ReadingChanged += OnCompassReadingChanged;
                Compass.Default.Start(SensorSpeed.UI);
                _isCompassActive = true;
                return;
            }
        }
        
        // Si pas de boussole, afficher direction fixe
        await ShowStaticDirection();
    }

    private async Task ShowStaticDirection()
    {
        try
        {
            // Rotation vers la direction du bar (0° = haut de l'écran)
            var staticAngle = NormalizeAngle(_targetBearing);
            await DirectionIndicator.RotateTo(staticAngle, 1000, Easing.SpringOut);

            // Animation de pulsation pour indiquer que c'est statique
            _ = Task.Run(async () =>
            {
                while (!_isCompassActive)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await DirectionIndicator.ScaleTo(1.1, 1000, Easing.SinInOut);
                        await DirectionIndicator.ScaleTo(1.0, 1000, Easing.SinInOut);
                    });
                    await Task.Delay(100);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur ShowStaticDirection: {ex.Message}");
        }
    }

    private void OnCompassReadingChanged(object sender, CompassChangedEventArgs e)
    {
        var currentHeading = e.Reading.HeadingMagneticNorth;
        var smoothedHeading = SmoothHeading(currentHeading);
        
        // Calculer l'angle relatif : direction du bar - orientation actuelle
        var relativeAngle = NormalizeAngle(_targetBearing - smoothedHeading);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await UpdateCompassDirection(relativeAngle);
        });
    }

    private double SmoothHeading(double newHeading)
    {
        _compassReadings.Enqueue(newHeading);
        if (_compassReadings.Count > _smoothingWindowSize)
            _compassReadings.Dequeue();

        // Moyenne circulaire pour éviter les problèmes à 0°/360°
        var sinSum = 0.0;
        var cosSum = 0.0;
        
        foreach (var angle in _compassReadings)
        {
            sinSum += Math.Sin(GeoHelper.ToRadians(angle));
            cosSum += Math.Cos(GeoHelper.ToRadians(angle));
        }
        
        var avgAngle = Math.Atan2(sinSum, cosSum);
        return NormalizeAngle(GeoHelper.ToDegrees(avgAngle));
    }

    private async Task UpdateCompassDirection(double relativeAngle)
    {
        if (_isAnimating) return;

        // Éviter les micro-mouvements
        var angleDiff = Math.Abs(relativeAngle - _lastAngle);
        if (angleDiff < 3.0 && angleDiff > 0) return;

        _isAnimating = true;
        
        try
        {
            // Rotation fluide vers la nouvelle direction
            await DirectionIndicator.RotateTo(relativeAngle, 400, Easing.CubicOut);
            _lastAngle = relativeAngle;
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private static double NormalizeAngle(double angle)
    {
        angle %= 360;
        if (angle < 0) angle += 360;
        return angle;
    }

    private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = GeoHelper.ToRadians(lon2 - lon1);
        var lat1Rad = GeoHelper.ToRadians(lat1);
        var lat2Rad = GeoHelper.ToRadians(lat2);

        var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - 
                Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);
        
        var bearing = GeoHelper.ToDegrees(Math.Atan2(y, x));
        return NormalizeAngle(bearing);
    }
    
    private string GetCompassDirection(double bearing)
    {
        var directions = new[]
        {
            "Nord", "Nord-Est", "Est", "Sud-Est",
            "Sud", "Sud-Ouest", "Ouest", "Nord-Ouest"
        };

        var index = (int)Math.Round(bearing / 45.0) % 8;
        return directions[index];
    }

    // Méthodes pour les boutons (inchangées)
    private async void OnOpenMapsClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button?.Parent is Border border)
            {
                await border.ScaleTo(0.95, 100);
                await border.ScaleTo(1, 100);
            }

            string url = !string.IsNullOrWhiteSpace(_selectedBar.Address) 
                ? $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(_selectedBar.Address)}"
                : $"https://www.google.com/maps/search/?api=1&query={_selectedBar.Latitude},{_selectedBar.Longitude}";

            await Launcher.Default.OpenAsync(url);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", "Impossible d'ouvrir Google Maps", "OK");
            Console.WriteLine($"Erreur OnOpenMapsClicked: {ex}");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        try
        {
            var button = sender as Button;
            if (button?.Parent is Border border)
            {
                await border.ScaleTo(0.95, 100);
                await border.ScaleTo(1, 100);
            }

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur OnBackClicked: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        try
        {
            if (_isCompassActive && Compass.Default.IsSupported)
            {
                Compass.Default.ReadingChanged -= OnCompassReadingChanged;
                Compass.Default.Stop();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur OnDisappearing: {ex.Message}");
        }
    }
}