using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Data;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Security;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Services;

public sealed record DonorLookupHit(int Id, string Name, string? Detail);

public interface IDonorService
{
    Task<IReadOnlyList<DonorListItem>> SearchAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<DonorLookupHit>> QuickSearchAsync(string term, CancellationToken ct = default);
    Task<IReadOnlyList<DonorOption>> OptionsAsync(CancellationToken ct = default);
    Task<DonorDetailsViewModel?> GetDetailsAsync(int id, CancellationToken ct = default);
    Task<string?> GetNameAsync(int id, CancellationToken ct = default);
    Task<Donor> CreateAsync(DonorFormViewModel model, string? userId, CancellationToken ct = default);
}

/// <summary>
/// Donor management. Names and contact details are encrypted at rest, so searching decrypts
/// in memory after authorisation rather than using SQL LIKE.
/// </summary>
public sealed class DonorService(AppDbContext db, IFieldEncryptionService enc) : IDonorService
{
    public async Task<IReadOnlyList<DonorListItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        var donors = await db.Donors.AsNoTracking().ToListAsync(ct);

        var gifts = (await db.Donations.AsNoTracking()
                .Where(d => d.DonorId != null && !d.IsVoided)
                .Select(d => new { d.DonorId, d.Date, d.AmountProtected })
                .ToListAsync(ct))
            .GroupBy(d => d.DonorId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return donors
            .Select(d =>
            {
                gifts.TryGetValue(d.DonorId, out var related);
                related ??= [];
                return new DonorListItem
                {
                    DonorId = d.DonorId,
                    FullName = enc.Unprotect(d.FullNameProtected) ?? "Donor",
                    Email = enc.Unprotect(d.EmailProtected),
                    Phone = enc.Unprotect(d.PhoneProtected),
                    DonationCount = related.Count,
                    TotalGiven = related.Sum(x => enc.UnprotectAmount(x.AmountProtected) ?? 0m),
                    LastDonationDate = related.Count == 0 ? null : related.Max(x => (DateTime?)x.Date)
                };
            })
            .Where(d => Matches(d.FullName, d.Email, d.Phone, query))
            .OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();
    }

    public async Task<IReadOnlyList<DonorLookupHit>> QuickSearchAsync(string term, CancellationToken ct = default)
    {
        var donors = await db.Donors.AsNoTracking().OrderBy(d => d.DonorId).ToListAsync(ct);

        return donors
            .Select(d => new
            {
                d.DonorId,
                Name = enc.Unprotect(d.FullNameProtected) ?? "Donor",
                Email = enc.Unprotect(d.EmailProtected),
                Phone = enc.Unprotect(d.PhoneProtected)
            })
            .Where(d => Matches(d.Name, d.Email, d.Phone, term))
            .Take(8)
            .Select(d => new DonorLookupHit(
                d.DonorId, d.Name, string.IsNullOrWhiteSpace(d.Phone) ? d.Email : d.Phone))
            .ToList();
    }

    public async Task<IReadOnlyList<DonorOption>> OptionsAsync(CancellationToken ct = default)
    {
        var donors = await db.Donors.AsNoTracking().Select(d => new { d.DonorId, d.FullNameProtected }).ToListAsync(ct);

        return donors
            .Select(d => new DonorOption(d.DonorId, enc.Unprotect(d.FullNameProtected) ?? "Donor"))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<DonorDetailsViewModel?> GetDetailsAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var donor = await db.Donors.AsNoTracking().SingleOrDefaultAsync(d => d.DonorId == id, ct);
        if (donor is null) return null;

        var donations = await db.Donations.AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Donor)
            .Include(d => d.Receipt)
            .Where(d => d.DonorId == id)
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.DonationId)
            .ToListAsync(ct);

        var rows = donations.Select(d => Mapping.ToRow(d, enc)).ToList();

        return new DonorDetailsViewModel
        {
            DonorId = donor.DonorId,
            FullName = enc.Unprotect(donor.FullNameProtected) ?? "Donor",
            Email = enc.Unprotect(donor.EmailProtected),
            Phone = enc.Unprotect(donor.PhoneProtected),
            Notes = enc.Unprotect(donor.NotesProtected),
            TotalGiven = rows.Where(r => !r.IsInKind && !r.IsVoided).Sum(r => r.Amount ?? 0m),
            Donations = rows
        };
    }

    public async Task<string?> GetNameAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var protectedName = await db.Donors.AsNoTracking()
            .Where(d => d.DonorId == id)
            .Select(d => d.FullNameProtected)
            .SingleOrDefaultAsync(ct);

        return protectedName is null ? null : enc.Unprotect(protectedName);
    }

    public async Task<Donor> CreateAsync(DonorFormViewModel model, string? userId, CancellationToken ct = default)
    {
        var name = model.FullName.Trim();
        if (name.Length == 0)
            throw new ValidationFailureException(nameof(model.FullName), "Enter the donor's full name.");

        var donor = new Donor
        {
            FullNameProtected = enc.Protect(name)!,
            EmailProtected = enc.Protect(Mapping.EmptyToNull(model.Email)),
            PhoneProtected = enc.Protect(Mapping.EmptyToNull(model.Phone)),
            NotesProtected = enc.Protect(Mapping.EmptyToNull(model.Notes)),
            CreatedById = userId,
            CreatedUtc = DateTime.UtcNow
        };

        db.Donors.Add(donor);
        await db.SaveChangesAsync(ct);
        return donor;
    }

    private static bool Matches(string name, string? email, string? phone, string query) =>
        name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || (phone?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
}
