using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class ProjectAddViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly User _currentUser;
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly bool _isEditMode;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private DateTimeOffset _startDate = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset _endDate = DateTimeOffset.Now.AddMonths(1);

        [ObservableProperty]
        private string _title = "Nuevo Proyecto";

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        private Project? _existingProject;

        public ProjectAddViewModel(IProjectService projectService, User currentUser, DashboardViewModel dashboardViewModel, Project? existingProject = null)
        {
            _projectService = projectService;
            _currentUser = currentUser;
            _dashboardViewModel = dashboardViewModel;
            _existingProject = existingProject;

            if (_existingProject != null)
            {
                _isEditMode = true;
                Title = "Editar Proyecto";
                Name = _existingProject.Name;
                Description = _existingProject.Description;
                StartDate = _existingProject.StartDate;
                EndDate = _existingProject.EndDate;
            }
        }

        [RelayCommand]
        private async Task Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "El nombre es requerido.";
                return;
            }

            if (_isEditMode && _existingProject != null)
            {
                _existingProject.Name = Name;
                _existingProject.Description = Description;
                _existingProject.StartDate = StartDate.DateTime;
                _existingProject.EndDate = EndDate.DateTime;
                
                await _projectService.UpdateProjectAsync(_existingProject);
                
                // Refresh top bar project list in Dashboard
                await _dashboardViewModel.LoadProjectsAsync(_existingProject.Id);
            }
            else
            {
                var newProject = new Project
                {
                    OwnerId = _currentUser.Id,
                    Name = Name,
                    Description = Description,
                    StartDate = StartDate.DateTime,
                    EndDate = EndDate.DateTime,
                    Status = "Pending"
                };
                await _projectService.CreateProjectAsync(newProject);
                
                // Refresh top bar project list in Dashboard and auto-select the new project
                await _dashboardViewModel.LoadProjectsAsync(newProject.Id);
            }

            // Navigate back to list
            _dashboardViewModel.ShowProjectList();
        }

        [RelayCommand]
        private void Cancel()
        {
            _dashboardViewModel.ShowProjectList();
        }
    }
}
