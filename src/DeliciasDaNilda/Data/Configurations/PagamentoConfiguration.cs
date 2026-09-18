using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>Mapeamento de Pagamento (RN09, RN10) — docs/modelo-de-dados.md, seção 2.9.</summary>
public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("Pagamentos", t =>
            t.HasCheckConstraint("CK_Pagamentos_Valor_Positivo", "[Valor] > 0"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Tipo)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(p => p.Valor)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(p => p.FormaPagamento)
            .HasMaxLength(30);

        builder.Property(p => p.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(p => p.Pedido)
            .WithMany(ped => ped.Pagamentos)
            .HasForeignKey(p => p.PedidoId)
            .OnDelete(DeleteBehavior.Restrict);

        // RN10: só o ADM dá baixa financeira. Sem cascade — Pagamentos são histórico.
        builder.HasOne(p => p.RegistradoPorAdministrador)
            .WithMany(a => a.PagamentosRegistrados)
            .HasForeignKey(p => p.RegistradoPorAdministradorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.PedidoId)
            .HasDatabaseName("IX_Pagamentos_PedidoId");
    }
}
