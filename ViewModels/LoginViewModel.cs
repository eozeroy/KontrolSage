using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Services;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly AuthViewModel _authViewModel;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public LoginViewModel(IAuthService authService, AuthViewModel authViewModel)
        {
            _authService = authService;
            _authViewModel = authViewModel;
        }

        [RelayCommand]
        private async Task Login()
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter email and password.";
                return;
            }

            var user = await _authService.LoginAsync(Email, Password);
            if (user != null)
            {
                if (_authService.IsTrialValid(user))
                {
                    // Trigger navigation to Main Window via AuthViewModel event
                    _authViewModel.OnLoginSuccess(user);
                }
                else
                {
                    ErrorMessage = "Your 30-day trial has expired.";
                }
            }
            else
            {
                ErrorMessage = "Invalid email or password.";
            }
        }

        [RelayCommand]
        private void GoToRegister()
        {
            _authViewModel.NavigateToRegister();
        }
    }
}
