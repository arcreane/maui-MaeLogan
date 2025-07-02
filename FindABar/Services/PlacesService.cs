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
                
                var street = tags.TryGetProperty("addr:street", out var s) ? s.GetString() : "";
                var number = tags.TryGetProperty("addr:housenumber", out var h) ? h.GetString() : "";
                var city = tags.TryGetProperty("addr:city", out var c) ? c.GetString() : "";

                var address = $"{number} {street}, {city}".Trim().Replace(" ,", "").Trim(',');

                bars.Add(new Bar
                {
                    Name = name,
                    Address = address,  // OSM ne fournit pas toujours l’adresse dans le node
                    Latitude = lat,
                    Longitude = lon,
                    Distance = 0 // à calculer plus tard
                });
                
                foreach (var bar in bars)
                {
                    var reverseUrl = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={bar.Latitude}&lon={bar.Longitude}";

                    // IMPORTANT: identifiez-vous, Nominatim l'exige
                    var reverseclient = new HttpClient();
                    reverseclient.DefaultRequestHeaders.Add("User-Agent", "FindABarApp/1.0 (mael.dabard@gmail.com)");

                    var rerverseresponse = await reverseclient.GetAsync(reverseUrl);

                    if (rerverseresponse.IsSuccessStatusCode)
                    {
                        var reversejson = await rerverseresponse.Content.ReadAsStringAsync();
                        var reversedoc = JsonDocument.Parse(reversejson);

                        var displayName = reversedoc.RootElement
                            .GetProperty("display_name")
                            .GetString();

                        bar.Address = displayName ?? "NO ADDRESS";
                    }
                }
            }
        }
        else
        {
            Console.WriteLine($"Erreur Overpass: {response.StatusCode}");
        }

        return bars;
    }
}