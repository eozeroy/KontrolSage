using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using KontrolSage.Models;
using KontrolSage.ViewModels;
using KontrolSage.Views;
using Microsoft.Extensions.DependencyInjection;
using KontrolSage.Services;
using System;

namespace KontrolSage;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure Services
        var services = new ServiceCollection();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IEdoService, EdoService>();
        services.AddSingleton<IEdcService, EdcService>();
        services.AddSingleton<IEdtService, EdtService>();
        services.AddSingleton<IPriceCatalogService, MongoPriceCatalogService>();
        services.AddSingleton<IDirectCostService, MongoDirectCostService>();
        services.AddSingleton<IAvanceService, MongoAvanceService>();
        services.AddSingleton<ICostoRealService, MongoCostoRealService>();
        services.AddSingleton<IEvmService, EvmService>();
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AuthViewModel>();
        
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Set shutdown mode to allow window swapping
            desktop.ShutdownMode = ShutdownMode.OnLastWindowClose;

            DisableAvaloniaDataAnnotationValidation();
            
            var authService = _serviceProvider.GetRequiredService<IAuthService>();
            var authViewModel = new AuthViewModel(authService);
            
            var authWindow = new AuthWindow
            {
                DataContext = authViewModel
            };

            // Subscribe to login success event — pasa authWindow para cerrarlo al abrir el Dashboard
            authViewModel.LoginSuccess += (user) => SetupMainWindow(user, desktop, authWindow);
            
            desktop.MainWindow = authWindow;
            authWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupMainWindow(User user, IClassicDesktopStyleApplicationLifetime desktop, Window? previousWindow = null)
    {
        var projectService = _serviceProvider!.GetRequiredService<IProjectService>();
        var edoService = _serviceProvider.GetRequiredService<IEdoService>();
        var edcService = _serviceProvider.GetRequiredService<IEdcService>();
        var edtService = _serviceProvider.GetRequiredService<IEdtService>();
        var catalogService = _serviceProvider.GetRequiredService<IPriceCatalogService>();
        var directCostService = _serviceProvider.GetRequiredService<IDirectCostService>();
        var avanceService = _serviceProvider.GetRequiredService<IAvanceService>();
        var costoRealService = _serviceProvider.GetRequiredService<ICostoRealService>();
        var evmService = _serviceProvider.GetRequiredService<IEvmService>();
        
        var dashboardVm = new DashboardViewModel(user, projectService, edoService, edcService, edtService, catalogService, directCostService, avanceService, costoRealService, evmService);
        
        var mainViewModel = new MainWindowViewModel(null!);
        mainViewModel.CurrentView = dashboardVm;
        
        var mainWindow = new MainWindow
        {
            DataContext = mainViewModel
        };

        // Logout: cerrar MainWindow y mostrar nueva AuthWindow (sin reiniciar proceso)
        dashboardVm.RequestLogout += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                mainWindow.Close();

                var authService = _serviceProvider.GetRequiredService<IAuthService>();
                var freshAuthVm = new AuthViewModel(authService);
                var freshAuthWindow = new AuthWindow { DataContext = freshAuthVm };

                freshAuthVm.LoginSuccess += (freshUser) => SetupMainWindow(freshUser, desktop, freshAuthWindow);

                desktop.MainWindow = freshAuthWindow;
                freshAuthWindow.Show();
            });
        };

        // Mostrar MainWindow y cerrar la ventana anterior (AuthWindow o anterior MainWindow)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            desktop.MainWindow = mainWindow;
            mainWindow.Show();
            previousWindow?.Close();
        });
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}