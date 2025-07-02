using FindABar.Pages;
using Microsoft.Extensions.Logging;

namespace FindABar;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        
        builder.Services.AddTransient<LoginPage>();

        return builder.Build();

#if DEBUG
        builder.Logging.AddDebug();
#endif

    }
}