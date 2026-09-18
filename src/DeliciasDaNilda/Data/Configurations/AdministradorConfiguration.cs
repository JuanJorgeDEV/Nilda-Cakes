using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>Mapeamento de Administrador (RN02) — docs/modelo-de-dados.md, seção 2.2.</summary>
public class AdministradorConfiguration : IEntityTypeConfiguration<Administrador>
{
    public void Configure(EntityTypeBuilder<Administrador> builder)
    {
        builder.ToTable("Administradores");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.NomeCompleto)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(a => a.Email)
            .IsRequired()
            .HasMaxLength(200)
            .IsUnicode(false);

        builder.Property(a => a.SenhaHash)
            .IsRequired()
            .HasMaxLength(300)
            .IsUnicode(false);

        builder.Property(a => a.Ativo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.CriadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(a => a.Email)
            .IsUnique()
            .HasDatabaseName("UX_Administradores_Email");

        // Administrador é referenciado por auditoria em MovimentoEstoque, Pedido e
        // Pagamento — nunca deletar em cascata a partir daqui (preserva histórico).
        builder.HasMany(a => a.MovimentosEstoque)
            .WithOne(m => m.Administrador)
            .HasForeignKey(m => m.AdministradorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.PedidosConfirmados)
            .WithOne(p => p.ConfirmadoPorAdministrador)
            .HasForeignKey(p => p.ConfirmadoPorAdministradorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.PagamentosRegistrados)
            .WithOne(p => p.RegistradoPorAdministrador)
            .HasForeignKey(p => p.RegistradoPorAdministradorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
