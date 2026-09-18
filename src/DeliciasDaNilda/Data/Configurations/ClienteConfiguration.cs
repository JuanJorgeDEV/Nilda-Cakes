using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>Mapeamento de Cliente (RN01) — docs/modelo-de-dados.md, seção 2.1.</summary>
public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.NomeCompleto)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.WhatsApp)
            .IsRequired()
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(c => c.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(c => c.WhatsApp)
            .IsUnique()
            .HasDatabaseName("UX_Clientes_WhatsApp");

        // Cliente 1-N Pedido: nunca deletar em cascata (histórico de negócio).
        builder.HasMany(c => c.Pedidos)
            .WithOne(p => p.Cliente)
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
