using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class ProjectListViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly User _currentUser;
        private readonly DashboardViewModel _dashboardViewModel; // To navigate

        [ObservableProperty]
        private ObservableCollection<Project> _projects = new();

        [ObservableProperty]
        private bool _isLoading;

        public ProjectListViewModel(IProjectService projectService, User currentUser, DashboardViewModel dashboardViewModel)
        {
            _projectService = projectService;
            _currentUser = currentUser;
            _dashboardViewModel = dashboardViewModel;
            LoadProjectsCommand.Execute(null);
        }

        [RelayCommand]
        private async Task LoadProjects()
        {
            IsLoading = true;
            var list = await _projectService.GetProjectsByUserAsync(_currentUser.Id!);
            Projects = new ObservableCollection<Project>(list);
            IsLoading = false;
        }

        [RelayCommand]
        private async Task DeleteProject(Project project)
        {
            if (project.Id != null)
            {
                await _projectService.DeleteProjectAsync(project.Id);
                Projects.Remove(project);
                
                // Refresh dashboard dropdown
                await _dashboardViewModel.LoadProjectsAsync();
            }
        }

        [RelayCommand]
        private void AddProject()
        {
            // Navigate to Add Project View
            _dashboardViewModel.CurrentContent = new ProjectAddViewModel(_projectService, _currentUser, _dashboardViewModel);
        }

        [RelayCommand]
        private void EditProject(Project project)
        {
             _dashboardViewModel.CurrentContent = new ProjectAddViewModel(_projectService, _currentUser, _dashboardViewModel, project);
        }
    }
}
