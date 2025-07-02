using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Configuration;


namespace FindABar.Pages;

public partial class LoginPage : ContentPage
{
    
    private string firebaseApiKey = "";
    public LoginPage()
    {
        Console.WriteLine("LoginPage constructor START");

        var json = string.Empty;

        using (var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").Result)
        using (var reader = new StreamReader(stream))
        {
            json = reader.ReadToEnd();
        }

        Console.WriteLine($"json read: {json}");

        var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
        firebaseApiKey = jsonDoc.RootElement
            .GetProperty("Firebase")
            .GetProperty("ApiKey")
            .GetString() ?? throw new InvalidOperationException("API key missing");

        Console.WriteLine($"Firebase API: {firebaseApiKey}");

        InitializeComponent();
    }


    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text;
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Erreur", "Remplissez les champs.", "OK");
            return;
        }

        var client = new HttpClient();
        var requestContent = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var json = JsonConvert.SerializeObject(requestContent);

        var response = await client.PostAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={firebaseApiKey}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        if (response.IsSuccessStatusCode)
        {
            // Auth OK
            await Navigation.PushAsync(new BarsListPage());        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            await DisplayAlert("Erreur", "Authentification échouée", "OK");
            Console.WriteLine(error);
        }
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text;
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Erreur", "Remplissez les champs.", "OK");
            return;
        }

        var client = new HttpClient();
        var requestContent = new
        {
            email,
            password,
            returnSecureToken = true
        };

        var json = JsonConvert.SerializeObject(requestContent);

        var response = await client.PostAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={firebaseApiKey}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        if (response.IsSuccessStatusCode)
        {
            await DisplayAlert("Succès", "Compte créé ! Connectez-vous.", "OK");
        }
        else
        {
            var error = await response.Content.ReadAsStringAsync();
            await DisplayAlert("Erreur", "Création de compte échouée", "OK");
            Console.WriteLine(error);
        }
    }
}
