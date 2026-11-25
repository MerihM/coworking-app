using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table refresh_tokens: hashed refresh tokens with rotation history (CW-10).</summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(
            "refresh_tokens",
            table =>
            {
                // Exactly one owner. num_nonnulls(...) is a PostgreSQL function that counts how many
                // of its arguments are NOT NULL: 0 = no owner, 2 = both – both are rejected.
                table.HasCheckConstraint(
                    "ck_refresh_tokens_single_owner",
                    "num_nonnulls(member_id, employee_id) = 1"
                );

                // octet_length = size in bytes. A SHA-256 hash is always 32 bytes.
                table.HasCheckConstraint(
                    "ck_refresh_tokens_hash_length",
                    "octet_length(token_hash) = 32"
                );

                table.HasCheckConstraint(
                    "ck_refresh_tokens_expires_after_created",
                    "expires_at > created_at"
                );
            }
        );

        // byte[] is mapped to PostgreSQL "bytea" (raw bytes). Unique, and this index is also what
        // makes "find the token by its hash" fast on every refresh.
        builder.Property(t => t.TokenHash).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // Revoking a whole family (reuse detection) filters by family_id.
        builder.HasIndex(t => t.FamilyId);

        // The daily cleanup deletes by expiry time.
        builder.HasIndex(t => t.ExpiresAt);

        // Owners. No navigation property on RefreshToken: tokens are always looked up by hash,
        // and Member/Employee don't need a Tokens collection. Cascade: if a member or employee is
        // ever hard-deleted (CW-12), their tokens mean nothing and go with them.
        builder
            .HasOne<Member>()
            .WithMany()
            .HasForeignKey(t => t.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Employee>()
            .WithMany()
            .HasForeignKey(t => t.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Rotation chain: old token → the token that replaced it (one-to-one, so EF adds a UNIQUE
        // index on replaced_by_token_id). SET NULL: when the cleanup deletes the newer token, the
        // older one just loses the link instead of blocking the delete.
        builder
            .HasOne(t => t.ReplacedBy)
            .WithOne()
            .HasForeignKey<RefreshToken>(t => t.ReplacedByTokenId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
