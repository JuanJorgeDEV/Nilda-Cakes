using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>Mapeamento de FichaTecnica (RN04) — docs/modelo-de-dados.md, seção 2.4.</summary>
public class FichaTecnicaConfiguration : IEntityTypeConfiguration<FichaTecnica>
{
    public void Configure(EntityTypeBuilder<FichaTecnica> builder)
    {
        builder.ToTable("FichasTecnicas");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        // 1-1 com Sabor (MVP não versiona — ver seção 5 do doc de modelagem).
        builder.HasOne(f => f.Sabor)
            .WithOne(s => s.FichaTecnica)
            .HasForeignKey<FichaTecnica>(f => f.SaborId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(f => f.SaborId)
            .IsUnique()
            .HasDatabaseName("UX_FichasTecnicas_SaborId");
    }
}
