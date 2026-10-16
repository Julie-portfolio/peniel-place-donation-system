using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Data;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Security;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Services;

public interface IDonationService
{
    Task<IReadOnlyList<CategoryOption>> CategoriesAsync(CancellationToken ct = default);

    Task<(IReadOnlyList<DonationRow> Rows, bool Truncated)> ListAsync(
        DateTime? from, DateTime? to, int? categoryId, int? donorId, CancellationToken ct = default);

    /// <summary>Loads one donation for the void confirmation screen (null when it does not exist).</summary>
    Task<DonationRow?> GetRowAsync(int donationId, CancellationToken ct = default);

    /// <summary>
    /// Voids a donation: it stays on record with who/when/why, but is excluded from every total.
    /// Throws <see cref="ValidationFailureException"/> for a bad reason or an already-voided donation,
    /// and returns false when the donation does not exist.
    /// </summary>
    Task<bool> VoidAsync(int donationId, string reason, string? userId, string userName, CancellationToken ct = default);

    /// <summary>Validates, encrypts and saves a donation and issues its receipt. Returns the donation id.</summary>
    Task<int> CreateAsync(DonationCreateViewModel model, string? userId, string issuedBy, CancellationToken ct = default);
}

/// <summary>Donation recording, filtering, validation, and receipt creation.</summary>
public sealed class DonationService(AppDbContext db, IFieldEncryptionService enc) : IDonationService
{
    public const int MaxListRows = 500;
    private const decimal MaxAmount = 10_000_000m;

    public async Task<IReadOnlyList<CategoryOption>> CategoriesAsync(CancellationToken ct = default) =>
        await db.DonationCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DonationCategoryId)
            .Select(c => new CategoryOption(c.DonationCategoryId, c.Name, c.IsInKind))
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<DonationRow> Rows, bool Truncated)> ListAsync(
        DateTime? from, DateTime? to, int? categoryId, int? donorId, CancellationToken ct = default)
    {
        if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
            throw new ArgumentException("Start date cannot be after end date.");
        if (categoryId.HasValue && categoryId.Value < 1)
            throw new ArgumentException("Invalid donation category filter.");
        if (donorId.HasValue && donorId.Value < 1)
            throw new ArgumentException("Invalid donor filter.");

        var query = db.Donations.AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Donor)
            .Include(d => d.Receipt)
            .AsQueryable();

        if (from.HasValue)
        {
            var start = from.Value.Date;
            query = query.Where(d => d.Date >= start);
        }

        if (to.HasValue)
        {
            var end = to.Value.Date;
            query = query.Where(d => d.Date <= end);
        }

        if (categoryId.HasValue)
            query = query.Where(d => d.DonationCategoryId == categoryId.Value);

        if (donorId.HasValue)
            query = query.Where(d => d.DonorId == donorId.Value);

        var records = await query
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.DonationId)
            .Take(MaxListRows + 1)
            .ToListAsync(ct);

        var truncated = records.Count > MaxListRows;
        var rows = records.Take(MaxListRows).Select(d => Mapping.ToRow(d, enc)).ToList();
        return (rows, truncated);
    }

    public async Task<DonationRow?> GetRowAsync(int donationId, CancellationToken ct = default)
    {
        if (donationId <= 0) return null;

        var record = await db.Donations.AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Donor)
            .Include(d => d.Receipt)
            .SingleOrDefaultAsync(d => d.DonationId == donationId, ct);

        return record is null ? null : Mapping.ToRow(record, enc);
    }

    public async Task<bool> VoidAsync(
        int donationId, string reason, string? userId, string userName, CancellationToken ct = default)
    {
        var text = Mapping.EmptyToNull(reason);
        if (text is null || text.Length < 5 || text.Length > 250)
            throw new ValidationFailureException(nameof(VoidDonationViewModel.Reason),
                "The reason must be between 5 and 250 characters.");

        if (donationId <= 0) return false;

        // Tracked load: we are updating this row.
        var record = await db.Donations.SingleOrDefaultAsync(d => d.DonationId == donationId, ct);
        if (record is null) return false;

        if (record.IsVoided)
            throw new ValidationFailureException(string.Empty, "This donation has already been voided.");

        record.IsVoided = true;
        record.VoidedUtc = DateTime.UtcNow;
        record.VoidedById = userId;
        record.VoidedByName = userName;
        record.VoidReasonProtected = enc.Protect(text);

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> CreateAsync(
        DonationCreateViewModel model, string? userId, string issuedBy, CancellationToken ct = default)
    {
        // The category row in the database is authoritative; the posted IsInKind flag is ignored.
        var category = model.CategoryId is > 0
            ? await db.DonationCategories.AsNoTracking()
                .SingleOrDefaultAsync(c => c.DonationCategoryId == model.CategoryId.Value && c.IsActive, ct)
            : null;

        if (category is null)
            throw new ValidationFailureException(nameof(model.CategoryId), "Choose a valid type of gift.");

        if (model.DonationDate is null ||
            model.DonationDate.Value.Date > DateTime.Today ||
            model.DonationDate.Value.Date < DateRules.Earliest)
        {
            throw new ValidationFailureException(
                nameof(model.DonationDate), "Choose a valid date between 1 January 2000 and today.");
        }

        int? donorId = null;
        if (!model.IsAnonymous)
        {
            if (model.DonorId is not > 0)
                throw new ValidationFailureException(
                    nameof(model.DonorId), "Pick a donor, or tick 'Anonymous or donor not known'.");

            var exists = await db.Donors.AsNoTracking().AnyAsync(d => d.DonorId == model.DonorId.Value, ct);
            if (!exists)
                throw new ValidationFailureException(
                    nameof(model.DonorId), "That donor could not be found. Choose a donor again.");

            donorId = model.DonorId.Value;
        }

        string? amountProtected = null;
        string? descriptionProtected = null;
        string? paymentProtected = null;

        if (category.IsInKind)
        {
            var description = Mapping.EmptyToNull(model.InKindDescription);
            if (description is null)
                throw new ValidationFailureException(nameof(model.InKindDescription), "Describe what was donated.");

            descriptionProtected = enc.Protect(description);
        }
        else
        {
            if (model.Amount is null || model.Amount < 0.01m || model.Amount > MaxAmount)
                throw new ValidationFailureException(
                    nameof(model.Amount), "Enter an amount between R 0.01 and R 10 000 000.");

            if (decimal.Round(model.Amount.Value, 2) != model.Amount.Value)
                throw new ValidationFailureException(
                    nameof(model.Amount), "The amount may have at most two decimal places.");

            amountProtected = enc.ProtectAmount(model.Amount);

            var method = Mapping.EmptyToNull(model.PaymentMethod);
            if (method is not null)
            {
                if (!PaymentMethods.IsValid(method))
                    throw new ValidationFailureException(nameof(model.PaymentMethod), "Choose a valid payment method.");

                // Store the canonical spelling.
                method = PaymentMethods.All.First(m => m.Equals(method, StringComparison.OrdinalIgnoreCase));
            }

            paymentProtected = enc.Protect(method);
        }

        var now = DateTime.UtcNow;
        var record = new DonationRecord
        {
            Date = model.DonationDate.Value.Date,
            DonationCategoryId = category.DonationCategoryId,
            DonorId = donorId,
            IsAnonymous = model.IsAnonymous,
            AmountProtected = amountProtected,
            InKindDescriptionProtected = descriptionProtected,
            PaymentMethodProtected = paymentProtected,
            NotesProtected = enc.Protect(Mapping.EmptyToNull(model.Notes)),
            CreatedById = userId,
            CreatedUtc = now
        };

        // Save the donation and its receipt together. If either save fails, neither is kept.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        db.Donations.Add(record);
        await db.SaveChangesAsync(ct);

        db.Receipts.Add(new Receipt
        {
            DonationId = record.DonationId,
            ReceiptNumber = $"PPM-{now:yyyyMMdd}-{record.DonationId:D6}",
            IssuedAtUtc = now,
            IssuedBy = issuedBy
        });
        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
        return record.DonationId;
    }
}
