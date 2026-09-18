using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>
/// Mapeamento de FichaTecnicaItem (RN04, RN12, RN14/RN15) —
/// docs/modelo-de-dados.md, seção 2.5.
/// </summary>
public class FichaTecnicaItemConfiguration : IEntityTypeConfiguration<FichaTecnicaItem>
{
    public void Configure(EntityTypeBuilder<FichaTecnicaItem> builder)
    {
        builder.ToTable("FichaTecnicaItens");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.QuantidadePorKg)
            .IsRequired()
            .HasPrecision(10, 4);

        builder.HasOne(i => i.FichaTecnica)
            .WithMany(f => f.Itens)
            .HasForeignKey(i => i.FichaTecnicaId)
            .OnDelete(DeleteBehavior.Cascade); // itens pertencem à ficha; sem valor de auditoria isolado.

        builder.HasOne(i => i.Ingrediente)
            .WithMany(ing => ing.FichaTecnicaItens)
            .HasForeignKey(i => i.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.FichaTecnicaId, i.IngredienteId })
            .IsUnique()
            .HasDatabaseName("UX_FichaTecnicaItens_Ficha_Ingrediente");
    }
}
