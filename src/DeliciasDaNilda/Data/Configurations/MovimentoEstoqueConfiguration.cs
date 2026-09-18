using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>
/// Mapeamento de MovimentoEstoque — log append-only (RN11, RN12, RN13, RN14/RN15) —
/// docs/modelo-de-dados.md, seção 2.7.
/// </summary>
public class MovimentoEstoqueConfiguration : IEntityTypeConfiguration<MovimentoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentoEstoque> builder)
    {
        builder.ToTable("MovimentosEstoque");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Tipo)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>();

        builder.Property(m => m.Quantidade)
            .IsRequired()
            .HasPrecision(12, 4);

        builder.Property(m => m.Motivo)
            .HasMaxLength(300);

        builder.Property(m => m.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(m => m.Ingrediente)
            .WithMany(i => i.MovimentosEstoque)
            .HasForeignKey(m => m.IngredienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Preenchido somente quando Tipo = SaidaConfirmacaoPedido (RN12).
        builder.HasOne(m => m.Pedido)
            .WithMany(p => p.MovimentosEstoque)
            .HasForeignKey(m => m.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Administrador)
            .WithMany(a => a.MovimentosEstoque)
            .HasForeignKey(m => m.AdministradorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.IngredienteId, m.CriadoEm })
            .HasDatabaseName("IX_MovimentosEstoque_IngredienteId_CriadoEm");

        builder.HasIndex(m => m.PedidoId)
            .HasDatabaseName("IX_MovimentosEstoque_PedidoId");
    }
}
