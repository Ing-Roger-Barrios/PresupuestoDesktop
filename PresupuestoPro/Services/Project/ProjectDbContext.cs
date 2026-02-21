using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.Models.Project;
using SQLite;

namespace PresupuestoPro.Services.Project
{
    public class ProjectDbContext
    {
        private readonly string _dbPath;

        public ProjectDbContext()
        {
            _dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "presupuesto_projects.db"
            );
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(_dbPath);
            connection.CreateTable<Proyecto>();
            connection.CreateTable<ProjectModule>();
            connection.CreateTable<ProjectItem>();
            connection.CreateTable<ProjectResource>();
        }

        public SQLiteConnection GetConnection() => new SQLiteConnection(_dbPath);
    }
}
