using FindABar.Models;
using FindABar.Services;
using Microsoft.Maui.Devices.Sensors;
using System.Collections.ObjectModel;
using Microsoft.Maui.Animations;

namespace FindABar.Pages;

public partial class BarsListPage : ContentPage
{
    public Command<Bar> OpenMapCommand { get; }
    private double _currentLatitude;
    private double _currentLongitude;
    private readonly int[] _distanceValues = { 1000, 3000, 5000, 10000 }; // distances en mètres
    
    public ObservableCollection<Bar> Bars { get; set; } = new ObservableCollection<Bar>();
    
    // Variables pour la détection de secousse
    private bool _isShakeEnabled = true;
    private DateTime _lastShakeTime = DateTime.MinValue;
    private readonly TimeSpan _shakeDelay = TimeSpan.FromSeconds(1); // Délai entre les secousses
    
    // Animation pour le chargement
    private CancellationTokenSource _loadingAnimationCancellation;
    
    public BarsListPage()
    {
        InitializeComponent();
        OpenMapCommand = new Command<Bar>(OpenMap);
        BindingContext = this;

        // Définir la distance par défaut (5 km)
        DistancePicker.SelectedIndex = 2;
        
        // Lier la collection observable à la CollectionView
        BarsCollectionView.ItemsSource = Bars;
        
        // Initialiser l'accéléromètre
        InitializeAccelerometer();
        
        // Charger les bars de manière asynchrone
        _ = LoadBarsWithAnimation();
    }

    private void InitializeAccelerometer()
    {
        try
        {
            if (Accelerometer.Default.IsSupported)
            {
                Accelerometer.Default.ReadingChanged += OnAccelerometerReadingChanged;
                Accelerometer.Default.Start(SensorSpeed.Game);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'initialisation de l'accéléromètre: {ex.Message}");
        }
    }

    private async void OnAccelerometerReadingChanged(object? sender, AccelerometerChangedEventArgs e)
    {
        if (!_isShakeEnabled || Bars.Count == 0) return;

        var reading = e.Reading;
        
        // Calculer la magnitude de l'accélération
        var magnitude = Math.Sqrt(reading.Acceleration.X * reading.Acceleration.X + 
                                 reading.Acceleration.Y * reading.Acceleration.Y + 
                                 reading.Acceleration.Z * reading.Acceleration.Z);

        // Détection de secousse (seuil ajustable)
        if (magnitude > 2.5 && DateTime.Now - _lastShakeTime > _shakeDelay)
        {
            _lastShakeTime = DateTime.Now;
            _isShakeEnabled = false;
            
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                // Effet de vibration
                try
                {
                    Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(100));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur vibration: {ex.Message}");
                }
                
                await ShowRandomBarWithAnimation();
            });
        }
    }

    private async Task ShowRandomBarWithAnimation()
    {
        if (Bars.Count == 0) return;

        var random = new Random();
        var randomBar = Bars[random.Next(Bars.Count)];

        // Animation de shake sur la liste
        await BarsCollectionView.TranslateTo(-10, 0, 50);
        await BarsCollectionView.TranslateTo(10, 0, 50);
        await BarsCollectionView.TranslateTo(-5, 0, 50);
        await BarsCollectionView.TranslateTo(5, 0, 50);
        await BarsCollectionView.TranslateTo(0, 0, 50);

        await ShowBarPopup(randomBar);
    }

    private async Task ShowBarPopup(Bar bar)
    {
        try
        {
            var popupPage = new RandomBarPopup(bar, _currentLatitude, _currentLongitude);
            await Navigation.PushModalAsync(popupPage);
            
            // Réactiver le shake après un délai
            await Task.Delay(2000);
            _isShakeEnabled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'ouverture du popup: {ex.Message}");
            await DisplayAlert("Erreur", "Impossible d'ouvrir le popup", "OK");
            _isShakeEnabled = true;
        }
    }

    private async Task ShowLoading(string message = "Chargement des bars...")
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                LoadingText.Text = message;
                LoadingFrame.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                EmptyStateFrame.IsVisible = false;
                
                // Animation d'apparition
                LoadingFrame.Opacity = 0;
                LoadingFrame.Scale = 0.8;
                await Task.WhenAll(
                    LoadingFrame.FadeTo(1, 300, Easing.CubicOut),
                    LoadingFrame.ScaleTo(1, 300, Easing.CubicOut)
                );
                
                // Démarrer l'animation de la barre de progression
                StartLoadingAnimation();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur ShowLoading: {ex.Message}");
            }
        });
    }

    private void StartLoadingAnimation()
    {
        _loadingAnimationCancellation = new CancellationTokenSource();
        
        Device.StartTimer(TimeSpan.FromMilliseconds(50), () =>
        {
            if (_loadingAnimationCancellation.Token.IsCancellationRequested)
                return false;
                
            // Animation de rotation de l'ActivityIndicator pourrait être ajoutée ici
            return true;
        });
    }

    private async Task HideLoading()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                _loadingAnimationCancellation?.Cancel();
                
                // Animation de disparition
                await Task.WhenAll(
                    LoadingFrame.FadeTo(0, 200, Easing.CubicIn),
                    LoadingFrame.ScaleTo(0.8, 200, Easing.CubicIn)
                );
                
                LoadingFrame.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                
                // Vérifier si la liste est vide pour afficher l'état vide
                if (Bars.Count == 0)
                {
                    await ShowEmptyState();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur HideLoading: {ex.Message}");
            }
        });
    }

    private async Task ShowEmptyState()
    {
        EmptyStateFrame.IsVisible = true;
        EmptyStateFrame.Opacity = 0;
        EmptyStateFrame.Scale = 0.8;
        await Task.WhenAll(
            EmptyStateFrame.FadeTo(1, 300, Easing.CubicOut),
            EmptyStateFrame.ScaleTo(1, 300, Easing.CubicOut)
        );
    }

    private async Task LoadBarsWithAnimation()
    {
        try
        {
            await ShowLoading("Obtention de la position...");

            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                HideLoading();
                await DisplayAlert("Permission requise", "L'accès à la localisation est nécessaire pour trouver les bars à proximité.", "OK");
                return;
            }

            var location = await Geolocation.GetLastKnownLocationAsync();
            
            if (location == null)
            {
                await ShowLoading("Localisation en cours...");
                location = await Geolocation.GetLocationAsync(new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.High,
                    Timeout = TimeSpan.FromSeconds(15)
                });
            }

            if (location == null)
            {
                HideLoading();
                await DisplayAlert("Erreur", "Impossible de déterminer votre position. Vérifiez que la localisation est activée.", "OK");
                return;
            }

            _isShakeEnabled = true;
            _currentLatitude = location.Latitude;
            _currentLongitude = location.Longitude;

            Console.WriteLine($"Coordonnées récupérées : {_currentLatitude}, {_currentLongitude}");

            await SearchBarsWithCurrentDistance();
        }
        catch (Exception ex)
        {
            HideLoading();
            await DisplayAlert("Erreur", $"Erreur lors de la récupération de la position : {ex.Message}", "OK");
            Console.WriteLine($"Erreur LoadBarsWithAnimation: {ex}");
        }
    }

    private async Task SearchBarsWithCurrentDistance()
    {
        try
        {
            if (_currentLatitude == 0 && _currentLongitude == 0)
            {
                HideLoading();
                await DisplayAlert("Erreur", "Position non disponible", "OK");
                return;
            }

            var selectedDistance = GetSelectedDistance();
            await ShowLoading($"Recherche des bars dans un rayon de {selectedDistance / 1000}km...");

            Console.WriteLine($"Recherche de bars dans un rayon de {selectedDistance}m");

            var placesService = new Services.PlacesService();
            var bars = await placesService.GetNearbyBarsAsync(_currentLatitude, _currentLongitude, selectedDistance);

            await ShowLoading("Calcul des distances...");

            // Calculer la distance réelle pour chaque bar
            foreach (var bar in bars)
            {
                bar.Distance = GeoHelper.GetDistanceKm(_currentLatitude, _currentLongitude, bar.Latitude, bar.Longitude);
            }

            // Filtrer les bars qui sont dans la distance sélectionnée et trier
            var filteredBars = bars
                .Where(b => b.Distance <= selectedDistance / 1000.0)
                .OrderBy(b => b.Distance)
                .ToList();

            // Mettre à jour la collection observable avec animation
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                Bars.Clear();
                
                // Animation d'apparition échelonnée des éléments
                foreach (var bar in filteredBars)
                {
                    Bars.Add(bar);
                    await Task.Delay(50); // Petit délai pour l'effet d'apparition échelonnée
                }
            });

            Console.WriteLine($"Nombre de bars trouvés : {filteredBars.Count}");
            
            HideLoading();
        }
        catch (Exception ex)
        {
            HideLoading();
            await DisplayAlert("Erreur", $"Erreur lors de la recherche : {ex.Message}", "OK");
            Console.WriteLine($"Erreur SearchBarsWithCurrentDistance: {ex}");
        }
    }

    private int GetSelectedDistance()
    {
        var selectedIndex = DistancePicker.SelectedIndex;
        return selectedIndex >= 0 && selectedIndex < _distanceValues.Length 
            ? _distanceValues[selectedIndex] 
            : 5000; // Par défaut 5km
    }

    private async void OnDistanceChanged(object sender, EventArgs e)
    {
        try
        {
            if (_currentLatitude != 0 && _currentLongitude != 0)
            {
                // Animation de feedback sur le picker
                await DistancePicker.ScaleTo(1.05, 100);
                await DistancePicker.ScaleTo(1, 100);
                
                await SearchBarsWithCurrentDistance();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur OnDistanceChanged: {ex.Message}");
        }
    }
    
    private async void OpenMap(Bar selectedBar)
    {
        try
        {
            string url = !string.IsNullOrWhiteSpace(selectedBar.Address)
                ? $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(selectedBar.Address)}"
                : $"https://www.google.com/maps/search/?api=1&query={selectedBar.Latitude},{selectedBar.Longitude}";

            await Launcher.Default.OpenAsync(url);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erreur", "Impossible d'ouvrir Google Maps", "OK");
            Console.WriteLine($"Erreur OpenMap: {ex}");
        }
    }
    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        try
        {
            // Animation de rotation sur le bouton de rafraîchissement
            var toolbarItem = sender as ToolbarItem;
            
            await LoadBarsWithAnimation();
            _isShakeEnabled = true;
            
            // Feedback visuel avec vibration légère
            try
            {
                Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(50));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur vibration refresh: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur OnRefreshClicked: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        try
        {
            _loadingAnimationCancellation?.Cancel();
            // L'accéléromètre reste actif pour une meilleure UX
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur OnDisappearing: {ex.Message}");
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            // Redémarrer l'accéléromètre et réactiver le shake
            if (Accelerometer.Default.IsSupported)
            {
                Accelerometer.Default.Start(SensorSpeed.Game);
            }
            _isShakeEnabled = true;
            
            // Animation d'apparition de la page
            this.Opacity = 0;
            this.FadeTo(1, 300);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur OnAppearing: {ex.Message}");
        }
    }
}