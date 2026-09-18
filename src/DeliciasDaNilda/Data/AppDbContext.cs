using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeliciasDaNilda.Data;

/// <summary>
/// DbContext do sistema Delícias da Nilda. Configuração via Fluent API
/// (ver Data/Configurations), conforme docs/modelo-de-dados.md.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Administrador> Administradores => Set<Administrador>();

    public DbSet<Sabor> Sabores => Set<Sabor>();

    public DbSet<FichaTecnica> FichasTecnicas => Set<FichaTecnica>();

    public DbSet<FichaTecnicaItem> FichaTecnicaItens => Set<FichaTecnicaItem>();

    public DbSet<Ingrediente> Ingredientes => Set<Ingrediente>();

    public DbSet<MovimentoEstoque> MovimentosEstoque => Set<MovimentoEstoque>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    /// <summary>Tabela singleton — deve conter exatamente uma linha (ver seed em Program.cs).</summary>
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
