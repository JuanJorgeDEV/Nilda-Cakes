using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeliciasDaNilda.Migrations
{
    /// <inheritdoc />
    public partial class ClienteEnderecoEOcasiao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EhAniversario",
                table: "Pedidos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "IdadeAniversariante",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeAniversariante",
                table: "Pedidos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Personalizacao",
                table: "Pedidos",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tema",
                table: "Pedidos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppLoja",
                table: "Configuracoes",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bairro",
                table: "Clientes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Clientes",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cidade",
                table: "Clientes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // WhatsApp passa a ser gravado sempre com DDI 55 (igual ao que o bot
            // recebe). Ajusta os números brasileiros gravados só com DDD, sem
            // mexer em quem já tenha o mesmo número com 55 (índice único).
            migrationBuilder.Sql(@"
UPDATE c SET c.WhatsApp = '55' + c.WhatsApp
FROM Clientes c
WHERE LEN(c.WhatsApp) IN (10, 11)
  AND NOT EXISTS (SELECT 1 FROM Clientes c2 WHERE c2.WhatsApp = '55' + c.WhatsApp);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EhAniversario",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "IdadeAniversariante",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "NomeAniversariante",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Personalizacao",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Tema",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "WhatsAppLoja",
                table: "Configuracoes");

            migrationBuilder.DropColumn(
                name: "Bairro",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Cidade",
                table: "Clientes");
        }
    }
}
