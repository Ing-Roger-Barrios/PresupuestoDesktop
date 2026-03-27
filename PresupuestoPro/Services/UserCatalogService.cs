// Services/UserCatalogService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PresupuestoPro.Data;
using PresupuestoPro.Models.UserCatalog;

namespace PresupuestoPro.Services
{
    public class UserCatalogService
    {
        // ── Inicializar BD (llamar en App.OnStartup) ──────────────────
        public static async Task InitializeAsync()
        {
            using var db = new UserCatalogDbContext();
            await db.Database.MigrateAsync();
            System.Diagnostics.Debug.WriteLine(
                $"[USER_DB] BD inicializada: {UserCatalogDbContext.DbPath}");
        }

        // ═════════════════════════════════════════════════════════════
        //  CATEGORÍAS
        // ═════════════════════════════════════════════════════════════

        public async Task<List<UserCategoria>> GetAllCategoriasAsync()
        {
            using var db = new UserCatalogDbContext();
            return await db.Categorias
                .Include(c => c.Modulos)
                    .ThenInclude(m => m.Items)
                        .ThenInclude(i => i.Recursos)
                .OrderBy(c => c.Nombre)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<UserCategoria> CreateCategoriaAsync(string nombre, string? descripcion = null)
        {
            using var db = new UserCatalogDbContext();
            var cat = new UserCategoria
            {
                Nombre = nombre.Trim(),
                Descripcion = descripcion?.Trim(),
                FechaCreacion = DateTime.UtcNow,
                FechaModificacion = DateTime.UtcNow
            };
            db.Categorias.Add(cat);
            await db.SaveChangesAsync();
            System.Diagnostics.Debug.WriteLine($"[USER_DB] Categoría creada: {cat.Id} - {cat.Nombre}");
            return cat;
        }

        public async Task<UserCategoria?> UpdateCategoriaAsync(int id, string nombre, string? descripcion = null)
        {
            using var db = new UserCatalogDbContext();
            var cat = await db.Categorias.FindAsync(id);
            if (cat == null) return null;

            cat.Nombre = nombre.Trim();
            cat.Descripcion = descripcion?.Trim();
            cat.FechaModificacion = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return cat;
        }

        public async Task<bool> DeleteCategoriaAsync(int id)
        {
            using var db = new UserCatalogDbContext();
            var cat = await db.Categorias.FindAsync(id);
            if (cat == null) return false;

            db.Categorias.Remove(cat);
            await db.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════════════════════
        //  MÓDULOS
        // ═════════════════════════════════════════════════════════════

        public async Task<UserModulo> CreateModuloAsync(int categoriaId, string nombre, string? descripcion = null)
        {
            using var db = new UserCatalogDbContext();

            // Calcular orden automático
            var orden = await db.Modulos
                .Where(m => m.CategoriaId == categoriaId)
                .CountAsync();

            var modulo = new UserModulo
            {
                CategoriaId = categoriaId,
                Nombre = nombre.Trim(),
                Descripcion = descripcion?.Trim(),
                Orden = orden,
                FechaCreacion = DateTime.UtcNow
            };
            db.Modulos.Add(modulo);
            await db.SaveChangesAsync();
            return modulo;
        }

        public async Task<UserModulo?> UpdateModuloAsync(int id, string nombre, string? descripcion = null)
        {
            using var db = new UserCatalogDbContext();
            var modulo = await db.Modulos.FindAsync(id);
            if (modulo == null) return null;

            modulo.Nombre = nombre.Trim();
            modulo.Descripcion = descripcion?.Trim();
            await db.SaveChangesAsync();
            return modulo;
        }

        public async Task<bool> DeleteModuloAsync(int id)
        {
            using var db = new UserCatalogDbContext();
            var modulo = await db.Modulos.FindAsync(id);
            if (modulo == null) return false;

            db.Modulos.Remove(modulo);
            await db.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════════════════════
        //  ITEMS
        // ═════════════════════════════════════════════════════════════

        public async Task<UserItem> CreateItemAsync(
            int moduloId, string descripcion, string unidad,
            decimal rendimiento = 1, string? codigo = null)
        {
            using var db = new UserCatalogDbContext();

            // Generar código automático si no se proporciona
            if (string.IsNullOrEmpty(codigo))
            {
                var count = await db.Items.CountAsync() + 1;
                codigo = $"USR_{count:D4}";
            }

            var item = new UserItem
            {
                ModuloId = moduloId,
                Codigo = codigo,
                Descripcion = descripcion.Trim(),
                Unidad = unidad.Trim(),
                Rendimiento = rendimiento,
                FechaCreacion = DateTime.UtcNow,
                FechaModificacion = DateTime.UtcNow
            };
            db.Items.Add(item);
            await db.SaveChangesAsync();
            return item;
        }

        public async Task<UserItem?> UpdateItemAsync(
            int id, string descripcion, string unidad,
            decimal rendimiento, string? codigo = null)
        {
            using var db = new UserCatalogDbContext();
            var item = await db.Items.FindAsync(id);
            if (item == null) return null;

            if (!string.IsNullOrEmpty(codigo)) item.Codigo = codigo;
            item.Descripcion = descripcion.Trim();
            item.Unidad = unidad.Trim();
            item.Rendimiento = rendimiento;
            item.FechaModificacion = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return item;
        }

        public async Task<bool> DeleteItemAsync(int id)
        {
            using var db = new UserCatalogDbContext();
            var item = await db.Items.FindAsync(id);
            if (item == null) return false;

            db.Items.Remove(item);
            await db.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════════════════════
        //  RECURSOS
        // ═════════════════════════════════════════════════════════════

        public async Task<UserRecurso> CreateRecursoAsync(
            int itemId, string nombre, string tipo,
            string unidad, decimal rendimiento, decimal precio)
        {
            using var db = new UserCatalogDbContext();
            var recurso = new UserRecurso
            {
                ItemId = itemId,
                Nombre = nombre.Trim(),
                Tipo = tipo,
                Unidad = unidad.Trim(),
                Rendimiento = rendimiento,
                Precio = precio
            };
            db.Recursos.Add(recurso);
            await db.SaveChangesAsync();
            return recurso;
        }

        public async Task<UserRecurso?> UpdateRecursoAsync(
            int id, string nombre, string tipo,
            string unidad, decimal rendimiento, decimal precio)
        {
            using var db = new UserCatalogDbContext();
            var recurso = await db.Recursos.FindAsync(id);
            if (recurso == null) return null;

            recurso.Nombre = nombre.Trim();
            recurso.Tipo = tipo;
            recurso.Unidad = unidad.Trim();
            recurso.Rendimiento = rendimiento;
            recurso.Precio = precio;
            await db.SaveChangesAsync();
            return recurso;
        }

        public async Task<bool> DeleteRecursoAsync(int id)
        {
            using var db = new UserCatalogDbContext();
            var recurso = await db.Recursos.FindAsync(id);
            if (recurso == null) return false;

            db.Recursos.Remove(recurso);
            await db.SaveChangesAsync();
            return true;
        }

        // ═════════════════════════════════════════════════════════════
        //  GUARDAR RECURSOS EN LOTE (para crear item completo de una vez)
        // ═════════════════════════════════════════════════════════════
        public async Task SaveRecursosAsync(int itemId, List<UserRecurso> recursos)
        {
            using var db = new UserCatalogDbContext();

            // Eliminar recursos existentes del item
            var existentes = await db.Recursos
                .Where(r => r.ItemId == itemId)
                .ToListAsync();
            db.Recursos.RemoveRange(existentes);

            // Agregar los nuevos
            foreach (var r in recursos)
                r.ItemId = itemId;

            db.Recursos.AddRange(recursos);
            await db.SaveChangesAsync();
        }

        // ═════════════════════════════════════════════════════════════
        //  BÚSQUEDA
        // ═════════════════════════════════════════════════════════════
        public async Task<List<UserItem>> SearchItemsAsync(string query)
        {
            using var db = new UserCatalogDbContext();
            var q = query.ToLower();
            return await db.Items
                .Include(i => i.Recursos)
                .Where(i => i.Descripcion.ToLower().Contains(q) ||
                            i.Codigo.ToLower().Contains(q))
                .AsNoTracking()
                .ToListAsync();
        }
    }
}