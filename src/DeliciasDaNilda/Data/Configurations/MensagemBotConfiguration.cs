using DeliciasDaNilda.Domain.Entities;
using DeliciasDaNilda.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliciasDaNilda.Data.Configurations;

/// <summary>
/// Mapeamento de MensagemBot (tabela MensagensBot) — overrides dos textos do
/// bot de WhatsApp. Não corresponde a uma RN numerada.
/// </summary>
public class MensagemBotConfiguration : IEntityTypeConfiguration<MensagemBot>
{
    public void Configure(EntityTypeBuilder<MensagemBot> builder)
    {
        builder.ToTable("MensagensBot");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Chave)
            .IsRequired()
            .HasMaxLength(MensagensBotPadrao.TamanhoMaximoChave);

        builder.HasIndex(m => m.Chave).IsUnique();

        // Mesmo limite validado no service; a coluna precisa comportá-lo.
        builder.Property(m => m.Texto)
            .IsRequired()
            .HasMaxLength(MensagensBotPadrao.TamanhoMaximoTexto);

        builder.Property(m => m.AtualizadoEm)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");
    }
}
