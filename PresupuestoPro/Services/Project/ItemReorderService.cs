using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.ViewModels.Project;

namespace PresupuestoPro.Services.Project
{
    public class ItemReorderService : IItemReorderService
    {
        public void ReorderItems(ObservableCollection<ProjectItemViewModel> items,
                               List<ProjectItemViewModel> itemsToMove,
                               int insertIndex)
        {
            if (!itemsToMove.Any()) return;

            // Obtener índices actuales
            var currentIndices = itemsToMove.Select(item => items.IndexOf(item))
                                           .Where(idx => idx >= 0)
                                           .OrderByDescending(idx => idx)
                                           .ToList();

            // Remover items
            foreach (var item in itemsToMove.ToList())
            {
                items.Remove(item);
            }

            // Ajustar índice de inserción
            foreach (var currentIndex in currentIndices)
            {
                if (currentIndex < insertIndex)
                {
                    insertIndex--;
                }
            }

            // Insertar en nueva posición
            insertIndex = Math.Max(0, Math.Min(insertIndex, items.Count));
            for (int i = 0; i < itemsToMove.Count; i++)
            {
                items.Insert(insertIndex + i, itemsToMove[i]);
            }
        }
    }
}
