using System.Text.Json;
using FindABar.Models;

namespace FindABar.Services;

public class PlacesService
{
    public async Task<List<Bar>> GetNearbyBarsAsync(double latitude, double longitude)
    {
        var bars = new List<Bar>();

        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        
        var overpassQuery = $"""
                                 [out:json];
                                 node
                                   ["amenity"="bar"]
                                   (around:1000,{latitude},{longitude});
                                 out;
                             """;

        var content = new StringContent($"data={Uri.EscapeDataString(overpassQuery)}",
            System.Text.Encoding.UTF8,
            "application/x-www-form-urlencoded");

        var response = await client.PostAsync("https://overpass-api.de/api/interpreter", content);
        Console.WriteLine($"HTTP status: {response.StatusCode}");
        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            
            Console.WriteLine($"JSON: {json}");

            if (!doc.RootElement.TryGetProperty("elements", out var elements))
            {
                Console.WriteLine("Aucun élément trouvé dans la réponse Overpass");
                return bars; // liste vide
            }

            foreach (var element in elements.EnumerateArray())
            {
                var tags = element.GetProperty("tags");

                var name = tags.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "Bar sans nom";
                var lat = element.GetProperty("lat").GetDouble();
                var lon = element.GetProperty("lon").GetDouble();

                bars.Add(new Bar
                {
                    Name = name,
                    Address = "",  // OSM ne fournit pas toujours l’adresse dans le node
                    Latitude = lat,
                    Longitude = lon,
                    Distance = 0 // à calculer plus tard
                });
            }
        }
        else
        {
            Console.WriteLine($"Erreur Overpass: {response.StatusCode}");
        }

        return bars;
    }
}