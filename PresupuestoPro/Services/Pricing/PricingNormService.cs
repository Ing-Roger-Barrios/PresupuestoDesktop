using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PresupuestoPro.Models.Pricing;
using SQLite;

namespace PresupuestoPro.Services.Pricing
{
    [Table("PricingNorms")]
    public class PricingNorm
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RulesJson { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class PricingNormService
    {
        private readonly string _dbPath;

        public PricingNormService()
        {
            _dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "presupuesto_norms.db"
            );
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(_dbPath);
            connection.CreateTable<PricingNorm>();

            if (connection.Table<PricingNorm>().Count() == 0)
            {
                CreateDefaultNorm(connection);
            }
        }

        private void CreateDefaultNorm(SQLiteConnection connection)
        {
            var rules = new List<PricingRule>
        {
            new() { Code = "A", Description = "MATERIALES", Percentage =  0, Formula = "*", IsEditable = false },
            new() { Code = "B", Description = "MANO DE OBRA", Percentage = 0, Formula = "*", IsEditable = false },
            new() { Code = "C", Description = "EQUIPO, MAQUINARIA Y HERRAMIEN", Percentage = 0, Formula = "*", IsEditable = false },
            new() { Code = "D", Description = "TOTAL MATERIALES", Percentage = 0, Formula = "A", IsEditable = false },
            new() { Code = "E", Description = "SUBTOTAL MANO DE OBRA", Percentage = 0, Formula = "B", IsEditable = false },
            new() { Code = "F", Description = "Cargas Sociales", Percentage = 55.00m, Formula = "E", IsEditable = true },
            new() { Code = "G", Description = "TOTAL MANO DE OBRA", Percentage = 0, Formula = "E+F+O", IsEditable = false },
            new() { Code = "H", Description = "Herramientas menores", Percentage = 5.00m, Formula = "G", IsEditable = true },
            new() { Code = "I", Description = "TOTAL HERRAMIENTAS Y EQUIPO", Percentage = 0, Formula = "C+H", IsEditable = false },
            new() { Code = "J", Description = "SUB TOTAL", Percentage = 0, Formula = "D+G+I", IsEditable = false },
            new() { Code = "K", Description = "Imprevistos", Percentage = 0.00m, Formula = "J", IsEditable = true },
            new() { Code = "L", Description = "Gastos grales. y administrativ", Percentage = 10.00m, Formula = "J", IsEditable = true },
            new() { Code = "M", Description = "Utilidad", Percentage = 10.00m, Formula = "J+L", IsEditable = true },
            new() { Code = "N", Description = "PARCIAL", Percentage = 0, Formula = "J+L+M", IsEditable = false },
            new() { Code = "O", Description = "Impuesto al Valor Agregado", Percentage = 14.94m, Formula = "E+F", IsEditable = true },
            new() { Code = "P", Description = "Impuesto a las Transacciones", Percentage = 3.09m, Formula = "N", IsEditable = true },
            new() { Code = "Q", Description = "TOTAL PRECIO UNITARIO", Percentage = 0, Formula = "N+P", IsEditable = false }
        };

            var norm = new PricingNorm
            {
                Name = "Norma SABS Bolivia",
                RulesJson = JsonSerializer.Serialize(rules),
                IsDefault = true
            };
            connection.Insert(norm);
        }

        public List<string> GetNormNames()
        {
            using var connection = new SQLiteConnection(_dbPath);
            return connection.Table<PricingNorm>().Select(n => n.Name).ToList();
        }

        public string GetDefaultNormName()
        {
            using var connection = new SQLiteConnection(_dbPath);
            var defaultNorm = connection.Table<PricingNorm>().FirstOrDefault(n => n.IsDefault);
            if (defaultNorm != null && !string.IsNullOrWhiteSpace(defaultNorm.Name))
                return defaultNorm.Name;

            return connection.Table<PricingNorm>().Select(n => n.Name).FirstOrDefault()
                ?? "Norma SABS Bolivia";
        }

        public List<PricingRule> GetNormRules(string name)
        {
            using var connection = new SQLiteConnection(_dbPath);
            var norm = connection.Table<PricingNorm>().FirstOrDefault(n => n.Name == name);
            if (norm != null)
            {
                return JsonSerializer.Deserialize<List<PricingRule>>(norm.RulesJson) ?? new List<PricingRule>();
            }
            return new List<PricingRule>();
        }

        public void SaveNorm(string name, List<PricingRule> rules, bool isDefault = false)
        {
            using var connection = new SQLiteConnection(_dbPath);

            if (isDefault)
            {
                var currentDefaults = connection.Table<PricingNorm>().Where(n => n.IsDefault).ToList();
                foreach (var norm in currentDefaults)
                {
                    norm.IsDefault = false;
                    connection.Update(norm);
                }
            }

            var existing = connection.Table<PricingNorm>().FirstOrDefault(n => n.Name == name);

            if (existing != null)
            {
                existing.RulesJson = JsonSerializer.Serialize(rules);
                existing.IsDefault = isDefault;
                connection.Update(existing);
            }
            else
            {
                var norm = new PricingNorm
                {
                    Name = name,
                    RulesJson = JsonSerializer.Serialize(rules),
                    IsDefault = isDefault
                };
                connection.Insert(norm);
            }
        }
    }
}
