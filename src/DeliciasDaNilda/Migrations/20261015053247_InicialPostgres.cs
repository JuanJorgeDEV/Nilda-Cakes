using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DeliciasDaNilda.Migrations
{
    /// <inheritdoc />
    public partial class InicialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Administradores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomeCompleto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(300)", unicode: false, maxLength: 300, nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Administradores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NomeCompleto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    WhatsApp = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    Cep = table.Column<string>(type: "character varying(8)", unicode: false, maxLength: 8, nullable: true),
                    Bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Cidade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WhatsAppBotAtivo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    WhatsAppLoja = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ingredientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    UnidadeMedida = table.Column<string>(type: "character varying(10)", unicode: false, maxLength: 10, nullable: false),
                    SaldoAtual = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false, defaultValue: 0m),
                    EstoqueMinimoAlerta = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    Categoria = table.Column<int>(type: "integer", nullable: false, defaultValue: 99),
                    CustoPorUnidade = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MensagensBot",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Chave = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Texto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MensagensBot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sabores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    PrecoPorKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Disponivel = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ImagemUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CustoExtraPorKg = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    ExcluidoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sabores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FichasTecnicas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SaborId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')"),
                    AtualizadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichasTecnicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichasTecnicas_Sabores_SaborId",
                        column: x => x.SaborId,
                        principalTable: "Sabores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    SaborId = table.Column<int>(type: "integer", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    PrecoPorKgUtilizado = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    DataEntrega = table.Column<DateOnly>(type: "date", nullable: false),
                    HorarioRetirada = table.Column<TimeOnly>(type: "time", nullable: true),
                    StatusPedido = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PendenteConfirmacao"),
                    StatusPagamento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "SemPagamento"),
                    ConfirmadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmadoPorAdministradorId = table.Column<int>(type: "integer", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EhAniversario = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    NomeAniversariante = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IdadeAniversariante = table.Column<int>(type: "integer", nullable: true),
                    Tema = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Personalizacao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.CheckConstraint("CK_Pedidos_Peso_Positivo", "\"Peso\" > 0");
                    table.ForeignKey(
                        name: "FK_Pedidos_Administradores_ConfirmadoPorAdministradorId",
                        column: x => x.ConfirmadoPorAdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pedidos_Sabores_SaborId",
                        column: x => x.SaborId,
                        principalTable: "Sabores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FichaTecnicaItens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FichaTecnicaId = table.Column<int>(type: "integer", nullable: false),
                    IngredienteId = table.Column<int>(type: "integer", nullable: false),
                    QuantidadePorKg = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichaTecnicaItens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichaTecnicaItens_FichasTecnicas_FichaTecnicaId",
                        column: x => x.FichaTecnicaId,
                        principalTable: "FichasTecnicas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FichaTecnicaItens_Ingredientes_IngredienteId",
                        column: x => x.IngredienteId,
                        principalTable: "Ingredientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimentosEstoque",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IngredienteId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    PedidoId = table.Column<int>(type: "integer", nullable: true),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AdministradorId = table.Column<int>(type: "integer", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosEstoque", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_Administradores_AdministradorId",
                        column: x => x.AdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_Ingredientes_IngredienteId",
                        column: x => x.IngredienteId,
                        principalTable: "Ingredientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosEstoque_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Pagamentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PedidoId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    FormaPagamento = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    RegistradoPorAdministradorId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "(now() at time zone 'utc')")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pagamentos", x => x.Id);
                    table.CheckConstraint("CK_Pagamentos_Valor_Positivo", "\"Valor\" > 0");
                    table.ForeignKey(
                        name: "FK_Pagamentos_Administradores_RegistradoPorAdministradorId",
                        column: x => x.RegistradoPorAdministradorId,
                        principalTable: "Administradores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Pagamentos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Administradores_Email",
                table: "Administradores",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Clientes_WhatsApp",
                table: "Clientes",
                column: "WhatsApp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FichasTecnicas_SaborId",
                table: "FichasTecnicas",
                column: "SaborId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FichaTecnicaItens_IngredienteId",
                table: "FichaTecnicaItens",
                column: "IngredienteId");

            migrationBuilder.CreateIndex(
                name: "UX_FichaTecnicaItens_Ficha_Ingrediente",
                table: "FichaTecnicaItens",
                columns: new[] { "FichaTecnicaId", "IngredienteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Ingredientes_Nome",
                table: "Ingredientes",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MensagensBot_Chave",
                table: "MensagensBot",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_AdministradorId",
                table: "MovimentosEstoque",
                column: "AdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_IngredienteId_CriadoEm",
                table: "MovimentosEstoque",
                columns: new[] { "IngredienteId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosEstoque_PedidoId",
                table: "MovimentosEstoque",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_PedidoId",
                table: "Pagamentos",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Pagamentos_RegistradoPorAdministradorId",
                table: "Pagamentos",
                column: "RegistradoPorAdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ConfirmadoPorAdministradorId",
                table: "Pedidos",
                column: "ConfirmadoPorAdministradorId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_DataEntrega_StatusPedido",
                table: "Pedidos",
                columns: new[] { "DataEntrega", "StatusPedido" });

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_SaborId",
                table: "Pedidos",
                column: "SaborId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_StatusPedido_DataEntrega",
                table: "Pedidos",
                columns: new[] { "StatusPedido", "DataEntrega" });

            migrationBuilder.CreateIndex(
                name: "UX_Sabores_Nome",
                table: "Sabores",
                column: "Nome",
                unique: true,
                filter: "\"ExcluidoEm\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Configuracoes");

            migrationBuilder.DropTable(
                name: "FichaTecnicaItens");

            migrationBuilder.DropTable(
                name: "MensagensBot");

            migrationBuilder.DropTable(
                name: "MovimentosEstoque");

            migrationBuilder.DropTable(
                name: "Pagamentos");

            migrationBuilder.DropTable(
                name: "FichasTecnicas");

            migrationBuilder.DropTable(
                name: "Ingredientes");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "Administradores");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Sabores");
        }
    }
}
