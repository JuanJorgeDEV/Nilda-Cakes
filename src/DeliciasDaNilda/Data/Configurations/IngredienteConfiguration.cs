using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>
/// Mapeamento de Ingrediente (RN11, RN12, RN13, RN14, RN15) —
/// docs/modelo-de-dados.md, seção 2.6.
/// </summary>
public class IngredienteConfiguration : IEntityTypeConfiguration<Ingrediente>
{
    public void Configure(EntityTypeBuilder<Ingrediente> builder)
    {
        builder.ToTable("Ingredientes");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Nome)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(i => i.UnidadeMedida)
            .IsRequired()
            .HasMaxLength(10)
            .IsUnicode(false);

        builder.Property(i => i.SaldoAtual)
            .IsRequired()
            .HasPrecision(12, 4)
            .HasDefaultValue(0m);

        builder.Property(i => i.EstoqueMinimoAlerta)
            .HasPrecision(12, 4);

        builder.Property(i => i.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(i => i.Nome)
            .IsUnique()
            .HasDatabaseName("UX_Ingredientes_Nome");

        // Ingrediente 1-N MovimentoEstoque: log de auditoria, nunca cascade.
        builder.HasMany(i => i.MovimentosEstoque)
            .WithOne(m => m.Ingrediente)
            .HasForeignKey(m => m.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
