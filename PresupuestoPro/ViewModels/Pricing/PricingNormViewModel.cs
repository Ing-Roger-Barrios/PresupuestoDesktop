using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresupuestoPro.Models.Pricing;
using PresupuestoPro.Services.Pricing;
using System.Windows;
using System.Linq;


namespace PresupuestoPro.ViewModels.Pricing
{
    public partial class PricingNormViewModel : ObservableObject
    {
        private readonly PricingNormService _service;
        private readonly Action<string>? _applyNormToProject;

        [ObservableProperty]
        private ObservableCollection<PricingRule> _rules = new();

        [ObservableProperty]
        private string _currentName = "Norma SABS Bolivia";

        [ObservableProperty]
        private bool _isDefault;

        [ObservableProperty]
        private ObservableCollection<string> _normNames = new();

        [ObservableProperty]
        private string? _selectedNorm;

        [ObservableProperty]
        private string _projectNormName = string.Empty;

        public PricingNormViewModel(
            PricingNormService service,
            string projectNormName,
            Action<string>? applyNormToProject = null)
        {
            _service = service;
            _applyNormToProject = applyNormToProject;
            ProjectNormName = projectNormName;
            LoadNormNames();
            SelectedNorm = NormNames.Contains(projectNormName)
                ? projectNormName
                : NormNames.FirstOrDefault();
        }

        private void LoadNormNames()
        {
            var names = _service.GetNormNames();
            NormNames = new ObservableCollection<string>(names);
            SelectedNorm = names.FirstOrDefault();
        }

        private void LoadSelectedNorm()
        {
            if (!string.IsNullOrEmpty(SelectedNorm))
            {
                LoadNorm(SelectedNorm);
            }
        }

        private void LoadNorm(string name)
        {
            var rules = _service.GetNormRules(name);
            Rules = new ObservableCollection<PricingRule>(rules);
            CurrentName = name;
            // IsDefault se puede determinar si es la norma predeterminada
            IsDefault = (name == "Norma SABS Bolivia");
        }

        partial void OnSelectedNormChanged(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                LoadNorm(value);
            }
        }

        [RelayCommand]
        private void SaveNorm()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(CurrentName))
                {
                    MessageBox.Show("Escriba un nombre para la norma.", "Nombre requerido",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Si el nombre actual no está en la lista, es una nueva norma
                bool isNewNorm = !NormNames.Contains(CurrentName);

                _service.SaveNorm(CurrentName, Rules.ToList(), IsDefault);

                // Si es una nueva norma, actualizar la lista
                if (isNewNorm)
                {
                    LoadNormNames();
                    SelectedNorm = CurrentName;
                }

                MessageBox.Show("Norma guardada exitosamente.", "Éxito",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                PricingNormChangedService.NotifyNormChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ApplySelectedNorm()
        {
            var normToApply = !string.IsNullOrWhiteSpace(SelectedNorm)
                ? SelectedNorm
                : CurrentName;

            if (string.IsNullOrWhiteSpace(normToApply))
            {
                MessageBox.Show("Seleccione una norma para aplicar al proyecto.", "Norma requerida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _applyNormToProject?.Invoke(normToApply);
            ProjectNormName = normToApply;

            MessageBox.Show($"La norma \"{normToApply}\" ahora está asignada al proyecto.",
                "Norma aplicada", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void CreateNewNorm()
        {
            var baseRules = Rules.Select(rule => new PricingRule
            {
                Code = rule.Code,
                Description = rule.Description,
                Percentage = rule.Percentage,
                Formula = rule.Formula,
                IsEditable = rule.IsEditable
            }).ToList();

            Rules = new ObservableCollection<PricingRule>(baseRules);
            SelectedNorm = null;
            IsDefault = false;

            var baseName = "Nueva norma";
            var suffix = 1;
            var candidate = baseName;
            while (NormNames.Contains(candidate))
            {
                suffix++;
                candidate = $"{baseName} {suffix}";
            }

            CurrentName = candidate;
        }
    }
}
