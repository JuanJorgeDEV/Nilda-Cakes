using DeliciasDaNilda.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>
/// Mapeamento de Configuracao — tabela singleton (só deve existir uma linha,
/// garantido pelo seed em Program.cs, não por constraint de banco). Não
/// corresponde a uma RN numerada; suporta a feature de bot de WhatsApp.
/// </summary>
public class ConfiguracaoConfiguration : IEntityTypeConfiguration<Configuracao>
{
    public void Configure(EntityTypeBuilder<Configuracao> builder)
    {
        builder.ToTable("Configuracoes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.WhatsAppBotAtivo)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(c => c.AtualizadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");
    }
}
