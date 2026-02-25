using CommunityToolkit.Mvvm.ComponentModel;
using KontrolSage.Models;
using KontrolSage.Services;
using System;

namespace KontrolSage.ViewModels
{
    public partial class AuthViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        
        [ObservableProperty]
        private ViewModelBase _currentView;

        // Event to notify App to switch to Main Window
        public event Action<User>? LoginSuccess;

        public AuthViewModel(IAuthService authService)
        {
            _authService = authService;
            // Default to Login
            _currentView = new LoginViewModel(_authService, this);
        }

        public void NavigateToLogin()
        {
            CurrentView = new LoginViewModel(_authService, this);
        }

        public void NavigateToRegister()
        {
            CurrentView = new RegisterViewModel(_authService, this);
        }
        
        public void OnLoginSuccess(User user)
        {
            LoginSuccess?.Invoke(user);
        }
    }
}
