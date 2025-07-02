namespace FindABar.Models;

public class Bar
{
    public string? Name { get; set; } = "";
    public string? Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Distance { get; set; } // calculé par rapport à l’utilisateur
}