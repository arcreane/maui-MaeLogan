using FindABar.Pages;

namespace FindABar;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new NavigationPage(new LoginPage());    }
}