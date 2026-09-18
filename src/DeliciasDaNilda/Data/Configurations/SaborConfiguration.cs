using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>Mapeamento de Sabor (RN03, RN04) — docs/modelo-de-dados.md, seção 2.3.</summary>
public class SaborConfiguration : IEntityTypeConfiguration<Sabor>
{
    public void Configure(EntityTypeBuilder<Sabor> builder)
    {
        builder.ToTable("Sabores");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nome)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(s => s.PrecoPorKg)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(s => s.Disponivel)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(s => s.Nome)
            .IsUnique()
            .HasDatabaseName("UX_Sabores_Nome");

        // Sabor 1-N Pedido: nunca deletar em cascata (histórico de negócio).
        builder.HasMany(s => s.Pedidos)
            .WithOne(p => p.Sabor)
            .HasForeignKey(p => p.SaborId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
