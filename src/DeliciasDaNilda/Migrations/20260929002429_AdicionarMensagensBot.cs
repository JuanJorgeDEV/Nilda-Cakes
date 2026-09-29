using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliciasDaNilda.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMensagensBot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MensagensBot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Chave = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensagensBot", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MensagensBot_Chave",
                table: "MensagensBot",
                column: "Chave",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MensagensBot");
        }
    }
}
