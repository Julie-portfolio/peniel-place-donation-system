using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Models;

namespace PenielPlaceMinistriesCSharp.Data;

public sealed class AppDbContext ( DbContextOptions<AppDbContext> options )
    : IdentityDbContext<ApplicationUser>(options), IDataProtectionKeyContext
{
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<DonationCategory> DonationCategories => Set<DonationCategory>();
    public DbSet<DonationRecord> Donations => Set<DonationRecord>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating ( ModelBuilder builder )
    {
        base.OnModelCreating(builder);

        builder.Entity<Donor>(e =>
        {
            e.HasKey(x => x.DonorId);
            e.Property(x => x.FullNameProtected).HasMaxLength(1024).IsRequired();
            e.Property(x => x.EmailProtected).HasMaxLength(1024);
            e.Property(x => x.PhoneProtected).HasMaxLength(512);
            e.Property(x => x.NotesProtected).HasMaxLength(2048);
            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DonationCategory>(e =>
        {
            e.HasKey(x => x.DonationCategoryId);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new DonationCategory { DonationCategoryId = 1, Name = "Tithe", IsInKind = false, IsActive = true },
                new DonationCategory { DonationCategoryId = 2, Name = "Offering", IsInKind = false, IsActive = true },
                new DonationCategory { DonationCategoryId = 3, Name = "Once-off gift", IsInKind = false, IsActive = true },
                new DonationCategory { DonationCategoryId = 4, Name = "In-kind donation", IsInKind = true, IsActive = true });
        });

        builder.Entity<DonationRecord>(e =>
        {
            e.HasKey(x => x.DonationId);
            e.Property(x => x.AmountProtected).HasMaxLength(256);
            e.Property(x => x.InKindDescriptionProtected).HasMaxLength(1024);
            e.Property(x => x.PaymentMethodProtected).HasMaxLength(256);
            e.Property(x => x.NotesProtected).HasMaxLength(2048);
            e.Property(x => x.VoidedByName).HasMaxLength(200);
            e.Property(x => x.VoidReasonProtected).HasMaxLength(1024);
            e.HasIndex(x => x.Date);

            e.HasOne(x => x.Category)
                .WithMany(x => x.Donations)
                .HasForeignKey(x => x.DonationCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Donor)
                .WithMany(x => x.Donations)
                .HasForeignKey(x => x.DonorId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Receipt)
                .WithOne(x => x.Donation)
                .HasForeignKey<Receipt>(x => x.DonationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Receipt>(e =>
        {
            e.HasKey(x => x.ReceiptId);
            e.Property(x => x.ReceiptNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.IssuedBy).HasMaxLength(200);
            e.HasIndex(x => x.DonationId).IsUnique();
            e.HasIndex(x => x.ReceiptNumber).IsUnique();
        });
    }
}
