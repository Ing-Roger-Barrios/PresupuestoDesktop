// Models/UserCatalog/UserCatalogModels.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PresupuestoPro.Models.UserCatalog
{
    [Table("UserCategorias")]
    public class UserCategoria
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;

        public List<UserModulo> Modulos { get; set; } = new();
    }

    [Table("UserModulos")]
    public class UserModulo
    {
        [Key]
        public int Id { get; set; }

        public int CategoriaId { get; set; }

        [Required, MaxLength(200)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Descripcion { get; set; }

        public int Orden { get; set; } = 0;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CategoriaId))]
        public UserCategoria? Categoria { get; set; }

        public List<UserItem> Items { get; set; } = new();
    }

    [Table("UserItems")]
    public class UserItem
    {
        [Key]
        public int Id { get; set; }

        public int ModuloId { get; set; }

        [MaxLength(100)]
        public string Codigo { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Unidad { get; set; } = string.Empty;

        public decimal Rendimiento { get; set; } = 1;
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        public DateTime FechaModificacion { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ModuloId))]
        public UserModulo? Modulo { get; set; }

        public List<UserRecurso> Recursos { get; set; } = new();
    }

    [Table("UserRecursos")]
    public class UserRecurso
    {
        [Key]
        public int Id { get; set; }

        public int ItemId { get; set; }

        [Required, MaxLength(300)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Tipo { get; set; } = "Material"; // Material, ManoObra, Equipo

        [MaxLength(50)]
        public string Unidad { get; set; } = string.Empty;

        public decimal Rendimiento { get; set; } = 1;
        public decimal Precio { get; set; } = 0;

        [ForeignKey(nameof(ItemId))]
        public UserItem? Item { get; set; }
    }
}