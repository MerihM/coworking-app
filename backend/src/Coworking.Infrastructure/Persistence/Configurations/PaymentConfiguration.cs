using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table payments: who recorded which payment (revenue report, FR11).</summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(
            "payments",
            table => table.HasCheckConstraint("ck_payments_amount", "amount >= 0")
        );

        // Zaposlenik 1 – 0..* Uplata. Restrict: an employee with recorded payments can't be deleted
        // (that's why employees are deactivated instead).
        builder
            .HasOne(p => p.Employee)
            .WithMany()
            .HasForeignKey(p => p.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
