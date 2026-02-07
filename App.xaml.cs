using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using MeuGestorVODs.Models;
using MeuGestorVODs.Services;
using MeuGestorVODs.ViewModels;

namespace MeuGestorVODs;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeuGestorVODs",
            "logs",
            "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.AddSerilog();
        });

        // Configuration
        services.AddSingleton(AppConfig.Load());

        // HttpClient
        services.AddHttpClient("default", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "MeuGestorVODs/1.0");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // Services
        services.AddScoped<IM3UService>(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("default");
            var logger = provider.GetRequiredService<ILogger<M3UService>>();
            var config = provider.GetRequiredService<AppConfig>();
            return new M3UService(httpClient, logger, config);
        });

        services.AddScoped<IDownloadService>(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("default");
            var logger = provider.GetRequiredService<ILogger<DownloadService>>();
            var config = provider.GetRequiredService<AppConfig>();
            return new DownloadService(httpClient, logger, config);
        });

        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IUpdateService>(provider =>
        {
            var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("default");
            var logger = provider.GetRequiredService<ILogger<UpdateService>>();
            return new UpdateService(httpClient, logger);
        });

        // ViewModels
        services.AddScoped<MainViewModel>();

        // Views
        services.AddScoped<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
