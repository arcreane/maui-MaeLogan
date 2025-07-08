using Newtonsoft.Json;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FindABar.Pages;

public partial class LoginPage : ContentPage
{
    // Clé API Firebase récupérée depuis appsettings.json
    private string firebaseApiKey = "";

    // Constructeur de la page de connexion
    public LoginPage()
    {
        Console.WriteLine("LoginPage constructor START");

        var json = string.Empty;

        // Lecture du fichier de configuration JSON embarqué (appsettings.json)
        using (var stream = FileSystem.OpenAppPackageFileAsync("appsettings.json").Result)
        using (var reader = new StreamReader(stream))
        {
            json = reader.ReadToEnd();
        }

        Console.WriteLine($"json read: {json}");

        // Parse du JSON pour extraire la clé API Firebase
        var jsonDoc = System.Text.Json.JsonDocument.Parse(json);
        firebaseApiKey = jsonDoc.RootElement
            .GetProperty("Firebase")
            .GetProperty("ApiKey")
            .GetString() ?? throw new InvalidOperationException("API key missing");

        Console.WriteLine($"Firebase API: {firebaseApiKey}");

        // Initialisation des composants XAML (éléments visuels)
        InitializeComponent();
    }

    // Gestionnaire de l'événement clic sur le bouton de connexion
    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text;
        var password = PasswordEntry.Text;

        // Vérification que les champs ne sont pas vides
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Erreur", "Remplissez les champs.", "OK");
            return;
        }

        var client = new HttpClient();
        
        // Contenu de la requête JSON pour l'authentification
        var requestContent = new
        {
            email,
            password,
            returnSecureToken = true
        };
        // Sérialisation de l'objet en JSON
        var json = JsonConvert.SerializeObject(requestContent);
        // Appel HTTP POST à l'API Firebase pour se connecter
        var response = await client.PostAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:" +
            $"signInWithPassword?key={firebaseApiKey}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        if (response.IsSuccessStatusCode)
        {
            // Si l'authentification réussit, navigation vers la liste des bars
            await Navigation.PushAsync(new BarsListPage());
        }
        else
        {
            // En cas d'erreur, affichage d'une alerte et log de l'erreur
            var error = await response.Content.ReadAsStringAsync();
            await DisplayAlert("Erreur", "Authentification échouée", "OK");
            Console.WriteLine(error);
        }
    }

    // Gestionnaire de l'événement clic sur le bouton d'inscription
    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text;
        var password = PasswordEntry.Text;

        // Vérification que les champs ne sont pas vides
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Erreur", "Remplissez les champs.", "OK");
            return;
        }

        var client = new HttpClient();
        // Contenu de la requête JSON pour l'inscription
        var requestContent = new
        {
            email,
            password,
            returnSecureToken = true
        };

        // Sérialisation de l'objet en JSON
        var json = JsonConvert.SerializeObject(requestContent);

        // Appel HTTP POST à l'API Firebase pour créer un compte
        var response = await client.PostAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={firebaseApiKey}",
            new StringContent(json, Encoding.UTF8, "application/json")
        );

        if (response.IsSuccessStatusCode)
        {
            // Si la création réussit, informer l'utilisateur de succès
            await DisplayAlert("Succès", "Compte créé ! Connectez-vous.", "OK");
        }
        else
        {
            // En cas d'erreur, affichage d'une alerte et log de l'erreur
            var error = await response.Content.ReadAsStringAsync();
            await DisplayAlert("Erreur", "Création de compte échouée", "OK");
            Console.WriteLine(error);
        }
    }
}
