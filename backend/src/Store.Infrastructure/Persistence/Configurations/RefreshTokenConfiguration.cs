using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain.Entities;

namespace Store.Infrastructure.Persistence.Configurations;

/// <summary>data-model.md "New: RefreshToken". Relationship to User is configured in <see cref="UserConfiguration"/>.</summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    private const int HashLength = 64;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash).HasMaxLength(HashLength).IsUnicode(false);
        builder.Property(t => t.ReplacedByTokenHash).HasMaxLength(HashLength).IsUnicode(false);

        // Rotation saves "UPDATE ... WHERE Id = @id AND RevokedAt IS NULL"; a concurrent refresh
        // that already revoked the token changes 0 rows and raises a concurrency conflict (research R4).
        builder.Property(t => t.RevokedAt).IsConcurrencyToken();

        builder.Ignore(t => t.IsRotated);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}
