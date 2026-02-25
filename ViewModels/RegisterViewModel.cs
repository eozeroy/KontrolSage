using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Services;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class RegisterViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private readonly AuthViewModel _authViewModel;

        [ObservableProperty]
        private string _username = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public RegisterViewModel(IAuthService authService, AuthViewModel authViewModel)
        {
            _authService = authService;
            _authViewModel = authViewModel;
        }

        [RelayCommand]
        private async Task Register()
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please fill in all fields.";
                return;
            }

            var success = await _authService.RegisterAsync(Username, Password, Email);
            if (success)
            {
                // After registration, go to login so user can log in
                _authViewModel.NavigateToLogin();
            }
            else
            {
                ErrorMessage = "Username already exists.";
            }
        }

        [RelayCommand]
        private void GoToLogin()
        {
            _authViewModel.NavigateToLogin();
        }
    }
}
