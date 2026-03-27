using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PresupuestoPro.Services.Pricing;
using PresupuestoPro.Services.Project;
using PresupuestoPro.Models.Project; // 👈 Añadir este using

namespace PresupuestoPro.ViewModels.Project
{
    // ViewModels/Project/ProjectItemViewModel.cs
    public partial class ProjectItemViewModel : ObservableObject
    {
        private readonly ProjectPricingService? _pricingService;
        private bool _isSyncing = false;
        private string _itemKey = string.Empty;

        public ProjectItemViewModel(ProjectPricingService? pricingService = null)
        {
            _pricingService = pricingService;
            Resources.CollectionChanged += OnResourcesCollectionChanged;
            GlobalItemService.ItemConfigurationChanged += OnGlobalItemChanged;
        }

        [ObservableProperty]
        private string _code = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _unit = string.Empty;

        [ObservableProperty]
        private decimal _quantity = 1;

        [ObservableProperty]
        private decimal _unitPrice;

        [ObservableProperty]
        private ObservableCollection<ProjectResourceViewModel> _resources = new();

        [ObservableProperty]
        private decimal _total;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isDuplicate;

        [ObservableProperty]
        private string _duplicateWarning = string.Empty;

        public void InitializeWithGlobalConfiguration()
        {
            _itemKey = GlobalItemService.GetItemKey(Code, Description, Unit);

            if (GlobalItemService.HasItemConfiguration(Code, Description, Unit))
            {
                var config = GlobalItemService.GetItemConfiguration(Code, Description, Unit);
                if (config != null)
                {
                    LoadConfiguration(config);
                }
            }
            else
            {
                SaveCurrentConfiguration();
            }
        }

        private void LoadConfiguration(ItemConfiguration config)
        {
            _isSyncing = true;
            try
            {
                Quantity = config.Quantity;
                Resources.Clear();

                foreach (var resourceConfig in config.Resources)
                {
                    var resource = new ProjectResourceViewModel(() => RecalculateUnitPrice())
                    {
                        ResourceType = resourceConfig.ResourceType,
                        ResourceName = resourceConfig.ResourceName,
                        Unit = resourceConfig.Unit,
                        Performance = resourceConfig.Performance,
                        UnitPrice = resourceConfig.UnitPrice
                    };
                    resource.InitializeWithGlobalPrice();
                    Resources.Add(resource);
                }
            }
            finally
            {
                _isSyncing = false;
            }
            RecalculateUnitPrice();
        }

        public void SaveCurrentConfiguration()
        {
            var config = new ItemConfiguration
            {
                Quantity = Quantity,
                Resources = Resources.Select(r => new ResourceConfiguration
                {
                    ResourceType = r.ResourceType,
                    ResourceName = r.ResourceName,
                    Unit = r.Unit,
                    Performance = r.Performance,
                    UnitPrice = r.UnitPrice
                }).ToList()
            };

            GlobalItemService.SetItemConfiguration(Code, Description, Unit, config);
        }

        private void OnGlobalItemChanged(string changedKey)
        {
            if (changedKey == _itemKey && !_isSyncing)
            {
                var config = GlobalItemService.GetItemConfiguration(Code, Description, Unit);
                if (config != null)
                {
                    LoadConfiguration(config);
                }
            }
        }

        private void OnResourcesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!_isSyncing)
            {
                SaveCurrentConfiguration();
            }
        }

        partial void OnQuantityChanged(decimal value)
        {
            CalculateTotal();
            if (!_isSyncing)
            {
                SaveCurrentConfiguration();
            }
        }

        partial void OnCodeChanged(string value)
        {
            UpdateItemKey();
        }

        partial void OnDescriptionChanged(string value)
        {
            UpdateItemKey();
        }

        partial void OnUnitChanged(string value)
        {
            UpdateItemKey();
        }

        private void UpdateItemKey()
        {
            var oldKey = _itemKey;
            _itemKey = GlobalItemService.GetItemKey(Code, Description, Unit);

            if (!string.IsNullOrEmpty(oldKey) && oldKey != _itemKey)
            {
                SaveCurrentConfiguration();
            }
        }

        public void RecalculateUnitPrice()
        {
            if (_pricingService != null)
            {
                UnitPrice = _pricingService.CalculateItemUnitPrice(this);
                CalculateTotal();
                OnPropertyChanged(nameof(UnitPrice));
                OnPropertyChanged(nameof(Total));
            }
        }

        private void CalculateTotal()
        {
            Total = Quantity * UnitPrice;
        }
        // Método para validar duplicados
        public void ValidateDuplicates(ObservableCollection<ProjectItemViewModel> allItemsInModule)
        {
            if (string.IsNullOrEmpty(Code) || string.IsNullOrEmpty(Description) || string.IsNullOrEmpty(Unit))
            {
                IsDuplicate = false;
                DuplicateWarning = string.Empty;
                return;
            }

            var duplicates = allItemsInModule
                .Where(item => item != this &&
                              item.Code == Code &&
                              item.Description == Description &&
                              item.Unit == Unit)
                .ToList();

            if (duplicates.Any())
            {
                IsDuplicate = true;
                DuplicateWarning = $"⚠️ Item duplicado: Ya existe en este módulo";
            }
            else
            {
                IsDuplicate = false;
                DuplicateWarning = string.Empty;
            }
        }
    }
}
