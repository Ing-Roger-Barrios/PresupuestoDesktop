using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PresupuestoPro.Migrations
{
    /// <inheritdoc />
    public partial class InitUserCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCategorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCategorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserModulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Orden = table.Column<int>(type: "INTEGER", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModulos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModulos_UserCategorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "UserCategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ModuloId = table.Column<int>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Rendimiento = table.Column<decimal>(type: "TEXT", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FechaModificacion = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserItems_UserModulos_ModuloId",
                        column: x => x.ModuloId,
                        principalTable: "UserModulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRecursos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Nombre = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Unidad = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Rendimiento = table.Column<decimal>(type: "TEXT", nullable: false),
                    Precio = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecursos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRecursos_UserItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "UserItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCategorias_Nombre",
                table: "UserCategorias",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_UserItems_Descripcion",
                table: "UserItems",
                column: "Descripcion");

            migrationBuilder.CreateIndex(
                name: "IX_UserItems_ModuloId",
                table: "UserItems",
                column: "ModuloId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModulos_CategoriaId",
                table: "UserModulos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecursos_ItemId",
                table: "UserRecursos",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecursos_Tipo_Nombre",
                table: "UserRecursos",
                columns: new[] { "Tipo", "Nombre" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRecursos");

            migrationBuilder.DropTable(
                name: "UserItems");

            migrationBuilder.DropTable(
                name: "UserModulos");

            migrationBuilder.DropTable(
                name: "UserCategorias");
        }
    }
}
