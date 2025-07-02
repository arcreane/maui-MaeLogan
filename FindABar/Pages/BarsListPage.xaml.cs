using FindABar.Models;

namespace FindABar.Pages;

public partial class BarsListPage : ContentPage
{
    public BarsListPage()
    {
        InitializeComponent();
        LoadBars();
    }

    private void LoadBars()
    {
        // pour l'instant des données mockées
        var bars = new List<Bar>
        {
            new Bar { Name = "Le Zinc", Address = "123 Rue du Bar", Latitude = 0, Longitude = 0, Distance = 0.5 },
            new Bar { Name = "La Buvette", Address = "456 Rue de la Soif", Latitude = 0, Longitude = 0, Distance = 1.2 },
            new Bar { Name = "Chez Marcel", Address = "789 Rue de la Fête", Latitude = 0, Longitude = 0, Distance = 2.0 },
        };

        BarsCollectionView.ItemsSource = bars;
    }
}