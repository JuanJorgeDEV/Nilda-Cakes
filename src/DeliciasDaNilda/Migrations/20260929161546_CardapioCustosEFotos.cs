using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliciasDaNilda.Migrations
{
    /// <inheritdoc />
    public partial class CardapioCustosEFotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Sabores_Nome",
                table: "Sabores");

            migrationBuilder.AddColumn<decimal>(
                name: "CustoExtraPorKg",
                table: "Sabores",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "Sabores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExcluidoEm",
                table: "Sabores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImagemUrl",
                table: "Sabores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "Ingredientes",
                type: "int",
                nullable: false,
                defaultValue: 99);

            migrationBuilder.AddColumn<decimal>(
                name: "CustoPorUnidade",
                table: "Ingredientes",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Sabores_Nome",
                table: "Sabores",
                column: "Nome",
                unique: true,
                filter: "[ExcluidoEm] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Sabores_Nome",
                table: "Sabores");

            migrationBuilder.DropColumn(
                name: "CustoExtraPorKg",
                table: "Sabores");

            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "Sabores");

            migrationBuilder.DropColumn(
                name: "ExcluidoEm",
                table: "Sabores");

            migrationBuilder.DropColumn(
                name: "ImagemUrl",
                table: "Sabores");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Ingredientes");

            migrationBuilder.DropColumn(
                name: "CustoPorUnidade",
                table: "Ingredientes");

            migrationBuilder.CreateIndex(
                name: "UX_Sabores_Nome",
                table: "Sabores",
                column: "Nome",
                unique: true);
        }
    }
}
