using CommunityToolkit.Mvvm.ComponentModel;
using KontrolSage.Services;
using KontrolSage.Models;

namespace KontrolSage.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private ViewModelBase _currentView;

    // Parameterless constructor for design-time previewer only
    public MainWindowViewModel() 
    {
        _authService = new AuthService(); 
        // For preview, show an empty dashboard or similar
        // _currentView = new DashboardViewModel(new User { Username = "PreviewUser" });
    }

    public MainWindowViewModel(IAuthService authService)
    {
        _authService = authService;
        // Default to nothing, will be set by App.axaml.cs
    }

    // Methods for navigation within the authenticated session can go here
    public void NavigateToDashboard(User user)
    {
        CurrentView = new DashboardViewModel(user);
    }
}
