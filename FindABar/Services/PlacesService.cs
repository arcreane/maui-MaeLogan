using System.Text.Json;
using FindABar.Models;
using System.Globalization;

namespace FindABar.Services;

public class PlacesService
{
    public async Task<List<Bar>> GetNearbyBarsAsync(double latitude, double longitude, int radiusInMeters = 5000)
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
                               (around:{radiusInMeters},{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)});
                             out;
                             """;

        var content = new StringContent($"data={Uri.EscapeDataString(overpassQuery)}",
            System.Text.Encoding.UTF8,
            "application/x-www-form-urlencoded");

        Console.WriteLine($"Requête Overpass (rayon: {radiusInMeters}m) : " + overpassQuery);
        
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

            // Première boucle : traiter tous les éléments Overpass
            foreach (var element in elements.EnumerateArray())
            {
                var tags = element.GetProperty("tags");

                var name = tags.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "Bar sans nom";
                var lat = element.GetProperty("lat").GetDouble();
                var lon = element.GetProperty("lon").GetDouble();
                
                // Essayer d'abord les tags standard addr:*
                var street = tags.TryGetProperty("addr:street", out var s) ? s.GetString() : "";
                var number = tags.TryGetProperty("addr:housenumber", out var h) ? h.GetString() : "";
                var city = tags.TryGetProperty("addr:city", out var c) ? c.GetString() : "";
                var postcode = tags.TryGetProperty("addr:postcode", out var pc) ? pc.GetString() : "";
                
                // Si pas d'adresse standard, essayer les tags contact:*
                if (string.IsNullOrEmpty(street))
                {
                    street = tags.TryGetProperty("contact:street", out var cs) ? cs.GetString() : "";
                    number = tags.TryGetProperty("contact:housenumber", out var ch) ? ch.GetString() : "";
                    city = tags.TryGetProperty("contact:city", out var cc) ? cc.GetString() : "";
                    postcode = tags.TryGetProperty("contact:postcode", out var cpc) ? cpc.GetString() : "";
                }

                var address = "";
                if (!string.IsNullOrEmpty(street) || !string.IsNullOrEmpty(city))
                {
                    var addressParts = new List<string>();
                    if (!string.IsNullOrEmpty(number) && !string.IsNullOrEmpty(street))
                        addressParts.Add($"{number} {street}");
                    else if (!string.IsNullOrEmpty(street))
                        addressParts.Add(street);
                    
                    if (!string.IsNullOrEmpty(postcode) && !string.IsNullOrEmpty(city))
                        addressParts.Add($"{postcode} {city}");
                    else if (!string.IsNullOrEmpty(city))
                        addressParts.Add(city);
                    
                    address = string.Join(", ", addressParts);
                }

                bars.Add(new Bar
                {
                    Name = name,
                    Address = address,
                    Latitude = lat,
                    Longitude = lon,
                    Distance = 0 // à calculer plus tard
                });
            }
            
            // Deuxième boucle : récupérer les adresses via Nominatim pour les bars qui n'ont pas d'adresse complète
            var reverseClient = new HttpClient();
            reverseClient.DefaultRequestHeaders.Add("User-Agent", "FindABarApp/1.0 (contact: mael.dabard@gmail.com)");
            
            foreach (var bar in bars)
            {
                // Ne faire l'appel Nominatim que si l'adresse est vide ou incomplète
                if (string.IsNullOrWhiteSpace(bar.Address) || bar.Address.Length < 5)
                {
                    try
                    {
                        // Formater les coordonnées avec la culture invariante pour éviter les problèmes de virgule/point
                        var lat = bar.Latitude.ToString("F7", CultureInfo.InvariantCulture);
                        var lon = bar.Longitude.ToString("F7", CultureInfo.InvariantCulture);
                        
                        var reverseUrl = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat}&lon={lon}&zoom=18&addressdetails=1";

                        Console.WriteLine($"Appel Nominatim pour {bar.Name}: {reverseUrl}");

                        var reverseResponse = await reverseClient.GetAsync(reverseUrl);

                        if (reverseResponse.IsSuccessStatusCode)
                        {
                            var reverseJson = await reverseResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"Réponse Nominatim pour {bar.Name}: {reverseJson}");
                            
                            var reverseDoc = JsonDocument.Parse(reverseJson);

                            // Essayer d'obtenir une adresse formatée ou le display_name
                            if (reverseDoc.RootElement.TryGetProperty("display_name", out var displayNameProp))
                            {
                                var displayName = displayNameProp.GetString();
                                bar.Address = displayName ?? "Adresse non disponible";
                            }
                            else
                            {
                                bar.Address = "Adresse non disponible";
                            }
                        }
                        else
                        {
                            var errorContent = await reverseResponse.Content.ReadAsStringAsync();
                            Console.WriteLine($"Erreur Nominatim pour {bar.Name}: {reverseResponse.StatusCode} - {errorContent}");
                            bar.Address = "Adresse non disponible";
                        }
                        
                        // Pause plus longue pour respecter les limites de taux de Nominatim (1 req/sec max)
                        await Task.Delay(1100);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur lors de la récupération de l'adresse pour {bar.Name}: {ex.Message}");
                        bar.Address = "Adresse non disponible";
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