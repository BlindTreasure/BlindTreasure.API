using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.EntityConfiguration
{
    public class PayoutConfiguration : IEntityTypeConfiguration<Payout>
    {
        public void Configure(EntityTypeBuilder<Payout> builder)
        {
            builder.ToTable("Payouts");

            // Primary Key
            builder.HasKey(p => p.Id);

            // Properties
            builder.Property(p => p.SellerId).IsRequired();

            builder.Property(p => p.PeriodStart).IsRequired();
            builder.Property(p => p.PeriodEnd).IsRequired();

            builder.Property(p => p.GrossAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(p => p.PlatformFeeRate)
                .HasColumnType("decimal(5,2)")
                .IsRequired();

            builder.Property(p => p.PlatformFeeAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(p => p.NetAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(p => p.StripeTransferId)
                .HasMaxLength(255);

            builder.Property(p => p.StripeDestinationAccount)
                .HasMaxLength(255);

            builder.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(p => p.PeriodType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(p => p.Notes)
                .HasMaxLength(1000);

            builder.Property(p => p.FailureReason)
                .HasMaxLength(500);

            builder.Property(p => p.RetryCount)
                .HasDefaultValue(0);

            // Relationships
            builder.HasOne(p => p.Seller)
                .WithMany()
                .HasForeignKey(p => p.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.PayoutDetails)
                .WithOne(pd => pd.Payout)
                .HasForeignKey(pd => pd.PayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p => p.PayoutLogs)
                .WithOne(pl => pl.Payout)
                .HasForeignKey(pl => pl.PayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(p => p.SellerId)
                .HasDatabaseName("IX_Payouts_SellerId");

            builder.HasIndex(p => new { p.SellerId, p.PeriodStart, p.PeriodEnd })
                .HasDatabaseName("IX_Payouts_Seller_Period")
                .IsUnique();

            builder.HasIndex(p => p.Status)
                .HasDatabaseName("IX_Payouts_Status");

            builder.HasIndex(p => p.StripeTransferId)
                .HasDatabaseName("IX_Payouts_StripeTransferId");
        }


    }



    public class PayoutLogConfiguration : IEntityTypeConfiguration<PayoutLog>
    {
        public void Configure(EntityTypeBuilder<PayoutLog> builder)
        {
            builder.ToTable("PayoutLogs");

            // Primary Key
            builder.HasKey(pl => pl.Id);

            // Properties
            builder.Property(pl => pl.PayoutId).IsRequired();

            builder.Property(pl => pl.FromStatus)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(pl => pl.ToStatus)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(pl => pl.Action)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(pl => pl.Details)
                .HasMaxLength(2000);

            builder.Property(pl => pl.ErrorMessage)
                .HasMaxLength(1000);

            // Relationships
            builder.HasOne(pl => pl.Payout)
                .WithMany(p => p.PayoutLogs)
                .HasForeignKey(pl => pl.PayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pl => pl.TriggeredByUser)
                .WithMany()
                .HasForeignKey(pl => pl.TriggeredByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Indexes
            builder.HasIndex(pl => pl.PayoutId)
                .HasDatabaseName("IX_PayoutLogs_PayoutId");

            builder.HasIndex(pl => pl.LoggedAt)
                .HasDatabaseName("IX_PayoutLogs_LoggedAt");
        }
    }

    public class PayoutDetailConfiguration : IEntityTypeConfiguration<PayoutDetail>
    {
        public void Configure(EntityTypeBuilder<PayoutDetail> builder)
        {
            builder.ToTable("PayoutDetails");

            // Primary Key
            builder.HasKey(pd => pd.Id);

            // Properties
            builder.Property(pd => pd.PayoutId).IsRequired();
            builder.Property(pd => pd.OrderDetailId).IsRequired();

            builder.Property(pd => pd.OriginalAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(pd => pd.DiscountAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(pd => pd.FinalAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            builder.Property(pd => pd.RefundAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            builder.Property(pd => pd.ContributedAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            // Relationships
            builder.HasOne(pd => pd.Payout)
                .WithMany(p => p.PayoutDetails)
                .HasForeignKey(pd => pd.PayoutId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pd => pd.OrderDetail)
                .WithMany()
                .HasForeignKey(pd => pd.OrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            builder.HasIndex(pd => pd.PayoutId)
                .HasDatabaseName("IX_PayoutDetails_PayoutId");

            builder.HasIndex(pd => pd.OrderDetailId)
                .HasDatabaseName("IX_PayoutDetails_OrderDetailId");

            // Unique constraint: Mỗi OrderDetail chỉ thuộc 1 payout
            builder.HasIndex(pd => pd.OrderDetailId)
                .IsUnique()
                .HasDatabaseName("UK_PayoutDetails_OrderDetailId");
        }
    }



}
