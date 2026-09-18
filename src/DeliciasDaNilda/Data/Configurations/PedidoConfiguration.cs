using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>
/// Mapeamento de Pedido (RN03, RN05, RN06, RN07, RN08, RN09, RN12, RN14/RN15, RN16) —
/// docs/modelo-de-dados.md, seção 2.8.
/// </summary>
public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("Pedidos", t =>
            t.HasCheckConstraint("CK_Pedidos_Peso_Positivo", "[Peso] > 0"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Peso)
            .IsRequired()
            .HasPrecision(6, 3);

        builder.Property(p => p.PrecoPorKgUtilizado)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(p => p.ValorTotal)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(p => p.DataEntrega)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(p => p.HorarioRetirada)
            .HasColumnType("time");

        builder.Property(p => p.StatusPedido)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion<string>()
            .HasDefaultValue(Domain.Enums.StatusPedido.PendenteConfirmacao);

        builder.Property(p => p.StatusPagamento)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(Domain.Enums.StatusPagamento.SemPagamento);

        builder.Property(p => p.Observacoes)
            .HasMaxLength(500);

        builder.Property(p => p.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(p => p.Cliente)
            .WithMany(c => c.Pedidos)
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Sabor)
            .WithMany(s => s.Pedidos)
            .HasForeignKey(p => p.SaborId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ConfirmadoPorAdministrador)
            .WithMany(a => a.PedidosConfirmados)
            .HasForeignKey(p => p.ConfirmadoPorAdministradorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Essencial para RN06/RN07 (contagem por data + status) e RN16 (agenda por dia).
        builder.HasIndex(p => new { p.DataEntrega, p.StatusPedido })
            .HasDatabaseName("IX_Pedidos_DataEntrega_StatusPedido");

        // Cobertura para a query de projeção RN14/RN15 (pedidos confirmados ordenados por data).
        builder.HasIndex(p => new { p.StatusPedido, p.DataEntrega })
            .HasDatabaseName("IX_Pedidos_StatusPedido_DataEntrega");

        builder.HasIndex(p => p.ClienteId)
            .HasDatabaseName("IX_Pedidos_ClienteId");
    }
}
