using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _welcomeMessage;
        
        [ObservableProperty]
        private bool _isPaneOpen = true; 

        [ObservableProperty]
        private ViewModelBase _currentContent;

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private Project? _selectedProject;

        public User User { get; }
        
        public string AppVersion { get; } = "v1.0.0";
        public string ConnectionStatus { get; } = "Online";

        // Event to notify the App/MainWindow that the user has logged out
        public event System.Action? RequestLogout;

        // We need a way to resolve services for child viewmodels. 
        // For MVP, we can hack it or inject IServiceProvider. 
        // Ideally, we inject a Factory.
        private readonly IProjectService _projectService;
        private readonly IEdoService _edoService;
        private readonly IEdcService _edcService;
        private readonly IEdtService _edtService;
        private readonly IEdtImportService _edtImportService;
        private readonly IPriceCatalogService _catalogService;
        private readonly IDirectCostService _directCostService;
        private readonly IAvanceService _avanceService;
        private readonly ICostoRealService _costoRealService;
        private readonly IEvmService _evmService;

        public DashboardViewModel(User user)
        {
            User = user;
            WelcomeMessage = $"Bienvenido, {user.Username}!";
        }

        // Constructor with Dependency Injection
        public DashboardViewModel(User user, IProjectService projectService, IEdoService edoService, IEdcService edcService, IEdtService edtService, IEdtImportService edtImportService, IPriceCatalogService catalogService, IDirectCostService directCostService, IAvanceService avanceService, ICostoRealService costoRealService, IEvmService evmService) : this(user)
        {
            _projectService = projectService;
            _edoService = edoService;
            _edcService = edcService;
            _edtService = edtService;
            _edtImportService = edtImportService;
            _catalogService = catalogService;
            _directCostService = directCostService;
            _avanceService = avanceService;
            _costoRealService = costoRealService;
            _evmService = evmService;
            // Default view
            ShowHome();
            _ = LoadProjectsAsync();
        }

        public async Task LoadProjectsAsync(string? selectedProjectId = null)
        {
            if (_projectService != null && User.Id != null)
            {
                var userProjects = await _projectService.GetProjectsByUserAsync(User.Id);
                var currentSelectedId = selectedProjectId ?? SelectedProject?.Id;
                
                Projects.Clear();
                foreach (var proj in userProjects)
                {
                    Projects.Add(proj);
                }
                
                if (Projects.Count > 0)
                {
                    SelectedProject = System.Linq.Enumerable.FirstOrDefault(Projects, p => p.Id == currentSelectedId) ?? Projects[0];
                }
                else
                {
                    SelectedProject = null;
                }
            }
        }

        [RelayCommand]
        private void Logout()
        {
            // Trigger an event so the main application loop can change the window
            RequestLogout?.Invoke();
        }

        [RelayCommand]
        private void TogglePane()
        {
            IsPaneOpen = !IsPaneOpen;
        }

        [RelayCommand]
        public void ShowHome()
        {
            if (_evmService != null && _directCostService != null && SelectedProject != null)
                CurrentContent = new DashboardKpiViewModel(_evmService, _directCostService, SelectedProject);
            else
                CurrentContent = new HomeViewModel(User);
        }

        [RelayCommand]
        public void ShowProjectList()
        {
            if (_projectService != null)
            {
                CurrentContent = new ProjectListViewModel(_projectService, User, this);
            }
        }
        
        [RelayCommand]
        public void ShowProjectAdd(Project? project = null)
        {
             if (_projectService != null)
            {
                CurrentContent = new ProjectAddViewModel(_projectService, User, this, project);
            }
        }

        [RelayCommand]
        public void ShowEdo()
        {
            if (_edoService != null && SelectedProject != null)
            {
                CurrentContent = new EdoViewModel(_edoService, SelectedProject);
            }
            else if (SelectedProject == null)
            {
                ShowHome();
            }
        }

        [RelayCommand]
        public void ShowEdc()
        {
            if (_edcService != null && _edoService != null && SelectedProject != null)
            {
                CurrentContent = new EdcViewModel(_edcService, _edoService, SelectedProject);
            }
            else if (SelectedProject == null)
            {
                ShowHome();
            }
        }

        [RelayCommand]
        public void ShowEdt()
        {
            if (_edtService != null && _edtImportService != null && _edcService != null && SelectedProject != null && _directCostService != null)
            {
                CurrentContent = new EdtViewModel(_edtService, _edtImportService, _edcService, _directCostService, SelectedProject);
            }
            else if (SelectedProject == null)
            {
                ShowHome();
            }
        }

        [RelayCommand]
        public void ShowMasterCatalog()
        {
            if (_catalogService != null)
            {
                CurrentContent = new MasterCatalogViewModel(_catalogService);
            }
        }

        [RelayCommand]
        public void ShowDirectCost()
        {
            if (_directCostService != null && _edtService != null && _catalogService != null && SelectedProject != null)
            {
                CurrentContent = new DirectCostListViewModel(_directCostService, _edtService, _catalogService, SelectedProject);
            }
            else if (SelectedProject == null)
            {
                ShowHome();
            }
        }

        [RelayCommand]
        public void ShowAvance()
        {
            if (_avanceService != null && _directCostService != null && _edtService != null && SelectedProject != null)
            {
                CurrentContent = new AvanceListViewModel(_avanceService, _directCostService, _edtService, SelectedProject);
            }
            else if (SelectedProject == null)
            {
                ShowHome();
            }
        }

        [RelayCommand]
        public void ShowCostoReal()
        {
            if (_costoRealService != null && _edcService != null && SelectedProject != null)
            {
                CurrentContent = new CostoRealListViewModel(_costoRealService, _edcService, SelectedProject);
            }
            else if (SelectedProject == null)
            {
                ShowHome();
            }
        }

        partial void OnSelectedProjectChanged(Project? value)
        {
            // If the user changes the project while looking at EDO, EDC, or EDT, refresh.
            if (CurrentContent is EdoViewModel)
            {
                ShowEdo();
            }
            else if (CurrentContent is EdcViewModel)
            {
                ShowEdc();
            }
            else if (CurrentContent is EdtViewModel)
            {
                ShowEdt();
            }
            else if (CurrentContent is DirectCostListViewModel)
            {
                ShowDirectCost();
            }
            else if (CurrentContent is AvanceListViewModel)
            {
                ShowAvance();
            }
            else if (CurrentContent is CostoRealListViewModel)
            {
                ShowCostoReal();
            }
        }
    }

    // Simple placeholder for Home content
    public class HomeViewModel : ViewModelBase
    {
        public string Welcome { get; }
        public HomeViewModel(User user) => Welcome = $"Bienvenido al Dashboard, {user.Username}";
    }
}
