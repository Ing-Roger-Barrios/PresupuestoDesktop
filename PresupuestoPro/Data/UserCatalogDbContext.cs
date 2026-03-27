// Data/UserCatalogDbContext.cs
using Microsoft.EntityFrameworkCore;
using PresupuestoPro.Models.UserCatalog;
using System;
using System.IO;

namespace PresupuestoPro.Data
{
    public class UserCatalogDbContext : DbContext
    {
        public DbSet<UserCategoria> Categorias { get; set; }
        public DbSet<UserModulo> Modulos { get; set; }
        public DbSet<UserItem> Items { get; set; }
        public DbSet<UserRecurso> Recursos { get; set; }

        // Ruta de la BD: %AppData%\PresupuestoPro\user_catalog.db
        public static string DbPath
        {
            get
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PresupuestoPro");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "user_catalog.db");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            // Índices para búsquedas rápidas
            model.Entity<UserCategoria>()
                .HasIndex(c => c.Nombre);

            model.Entity<UserModulo>()
                .HasIndex(m => m.CategoriaId);

            model.Entity<UserItem>()
                .HasIndex(i => i.ModuloId);

            model.Entity<UserItem>()
                .HasIndex(i => i.Descripcion);

            model.Entity<UserRecurso>()
                .HasIndex(r => r.ItemId);

            model.Entity<UserRecurso>()
                .HasIndex(r => new { r.Tipo, r.Nombre });
        }
    }
}
