using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.Models.Project;
using PresupuestoPro.ViewModels.Project;

namespace PresupuestoPro.Services.Project
{
    public class ProjectService
    {
        private readonly ProjectDbContext _dbContext;

        public ProjectService()
        {
            _dbContext = new ProjectDbContext();
        }

        public async Task<ProjectViewModel> CreateNewProjectAsync(string projectName = "Nuevo Proyecto")
        {
            return await Task.Run(() =>
            {
                var projectVm = new ProjectViewModel { Name = projectName };
                return projectVm;
            });
        }

        public async Task SaveProjectAsync(ProjectViewModel projectVm)
        {
            await Task.Run(() =>
            {
                using var connection = _dbContext.GetConnection();
                connection.BeginTransaction();

                try
                {
                    // Guardar proyecto
                    var project = new Proyecto { Name = projectVm.Name };
                    var projectId = connection.Insert(project);

                    // Guardar módulos e items recursivamente
                    foreach (var moduleVm in projectVm.Modules)
                    {
                        var module = new ProjectModule
                        {
                            ProjectId = (int)projectId,
                            Name = moduleVm.Name
                        };
                        var moduleId = connection.Insert(module);

                        foreach (var itemVm in moduleVm.Items)
                        {
                            var item = new ProjectItem
                            {
                                ModuleId = (int)moduleId,
                                Code = itemVm.Code,
                                Description = itemVm.Description,
                                Unit = itemVm.Unit,
                                Quantity = itemVm.Quantity,
                                UnitPrice = itemVm.UnitPrice
                            };
                            var itemId = connection.Insert(item);

                            foreach (var resourceVm in itemVm.Resources)
                            {
                                var resource = new ProjectResource
                                {
                                    ItemId = (int)itemId,
                                    ResourceType = resourceVm.ResourceType,
                                    ResourceName = resourceVm.ResourceName,
                                    Unit = resourceVm.Unit,
                                    Performance = resourceVm.Performance,
                                    UnitPrice = resourceVm.UnitPrice
                                };
                                connection.Insert(resource);
                            }
                        }
                    }

                    connection.Commit();
                }
                catch (Exception)
                {
                    connection.Rollback();
                    throw;
                }
            });
        }
    }
}
