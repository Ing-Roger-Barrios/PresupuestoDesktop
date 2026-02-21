using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels.Project
{
    // ProjectResourceViewModel.cs
    using CommunityToolkit.Mvvm.ComponentModel;
    using PresupuestoPro.Services.Project;
    using System.ComponentModel;
    using System.Security.AccessControl;

    public partial class ProjectResourceViewModel : ObservableObject
    {
        private Action? _onPropertyChangedCallback;
        private bool _isSyncing = false;

        public ProjectResourceViewModel(Action? onPropertyChangedCallback = null)
        {
            _onPropertyChangedCallback = onPropertyChangedCallback;
            GlobalResourceService.ResourcePriceChanged += OnGlobalPriceChanged;
        }

        [ObservableProperty]
        private string _resourceType = string.Empty;

        [ObservableProperty]
        private string _resourceName = string.Empty;

        [ObservableProperty]
        private string _unit = string.Empty; // 👈 Ahora se usa en la clave

        [ObservableProperty]
        private decimal _performance = 1;

        [ObservableProperty]
        private decimal _unitPrice;

        [ObservableProperty]
        private decimal _partialCost;

        public bool IsMaterial => ResourceType.Equals("Material", StringComparison.OrdinalIgnoreCase);
        public bool IsManoObra => ResourceType.Equals("Mano de Obra", StringComparison.OrdinalIgnoreCase);
        public bool IsEquipo => !IsMaterial && !IsManoObra;

        public void InitializeWithGlobalPrice()
        {
            if (GlobalResourceService.HasGlobalPrice(ResourceType, ResourceName, Unit))
            {
                UnitPrice = GlobalResourceService.GetGlobalPrice(ResourceType, ResourceName, Unit);
            }
            else
            {
                GlobalResourceService.SetGlobalPrice(ResourceType, ResourceName, Unit, UnitPrice);
            }
        }

        private void OnGlobalPriceChanged(string key, decimal newPrice)
        {
            // 👇 INCLUIR UNIDAD EN LA CLAVE
            var expectedKey = GlobalResourceService.GetResourceKey(ResourceType, ResourceName, Unit);
            if (key == expectedKey && !_isSyncing)
            {
                _isSyncing = true;
                try
                {
                    UnitPrice = newPrice;
                }
                finally
                {
                    _isSyncing = false;
                }
            }
        }

        partial void OnPerformanceChanged(decimal value)
        {
            CalculatePartialCost();
            _onPropertyChangedCallback?.Invoke();
        }

        partial void OnUnitPriceChanged(decimal value)
        {
            if (!_isSyncing)
            {
                // 👇 INCLUIR UNIDAD
                GlobalResourceService.SetGlobalPrice(ResourceType, ResourceName, Unit, value);
            }
            CalculatePartialCost();
            _onPropertyChangedCallback?.Invoke();
        }

        private void CalculatePartialCost()
        {
            PartialCost = Performance * UnitPrice;
        }
    }
}
