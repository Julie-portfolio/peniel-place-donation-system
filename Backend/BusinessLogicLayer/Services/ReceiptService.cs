using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Data;
using PenielPlaceMinistriesCSharp.Security;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Services;

public interface IReceiptService
{
    Task<ReceiptViewModel?> GetForDonationAsync(int donationId, CancellationToken ct = default);
}

/// <summary>Receipt retrieval.</summary>
public sealed class ReceiptService(AppDbContext db, IFieldEncryptionService enc) : IReceiptService
{
    public async Task<ReceiptViewModel?> GetForDonationAsync(int donationId, CancellationToken ct = default)
    {
        if (donationId <= 0) return null;

        var donation = await db.Donations.AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Donor)
            .Include(d => d.Receipt)
            .SingleOrDefaultAsync(d => d.DonationId == donationId, ct);

        if (donation?.Receipt is null) return null;

        return new ReceiptViewModel
        {
            DonationId = donation.DonationId,
            ReceiptId = donation.Receipt.ReceiptId,
            ReceiptNumber = donation.Receipt.ReceiptNumber,
            DonorName = Mapping.DonorName(donation, enc),
            DonationDate = donation.Date,
            CategoryName = donation.Category.Name,
            IsInKind = donation.Category.IsInKind,
            InKindDescription = enc.Unprotect(donation.InKindDescriptionProtected),
            PaymentMethod = enc.Unprotect(donation.PaymentMethodProtected),
            Amount = enc.UnprotectAmount(donation.AmountProtected),
            IssuedDate = donation.Receipt.IssuedAtUtc,
            IssuedBy = string.IsNullOrWhiteSpace(donation.Receipt.IssuedBy) ? "System" : donation.Receipt.IssuedBy,
            IsVoided = donation.IsVoided,
            VoidedDate = donation.VoidedUtc,
            VoidedBy = donation.VoidedByName,
            VoidReason = donation.IsVoided ? enc.Unprotect(donation.VoidReasonProtected) : null
        };
    }
}
