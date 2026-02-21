using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresupuestoPro.Models.Pricing;
using PresupuestoPro.Services.Pricing;
using System.Windows;


namespace PresupuestoPro.ViewModels.Pricing
{
    public partial class PricingNormViewModel : ObservableObject
    {
        private readonly PricingNormService _service;
        // Eliminamos _originalRules y HasChanges

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

        public PricingNormViewModel()
        {
            _service = new PricingNormService();
            LoadNormNames();
            LoadSelectedNorm();
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
                // 👇 NOTIFICAR QUE LA NORMA CAMBIÓ
                PricingNormChangedService.NotifyNormChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
