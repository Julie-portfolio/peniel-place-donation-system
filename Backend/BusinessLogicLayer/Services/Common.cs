using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Security;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Services;

/// <summary>Raised when user input breaks a business rule; <see cref="Field"/> is the form field to attach the message to.</summary>
public sealed class ValidationFailureException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public static class DateRules
{
    public static readonly DateTime Earliest = new(2000, 1, 1);

    /// <summary>A usable reporting range: 1 Jan 2000 through today, start no later than end.</summary>
    public static bool IsValidRange(DateTime from, DateTime to) =>
        from.Date >= Earliest && to.Date <= DateTime.Today && from.Date <= to.Date;
}

internal static class Mapping
{
    public const string AnonymousName = "Anonymous";

    public static string DonorName(DonationRecord d, IFieldEncryptionService enc) =>
        d.IsAnonymous || d.Donor is null
            ? AnonymousName
            : enc.Unprotect(d.Donor.FullNameProtected) ?? "Donor";

    public static DonationRow ToRow(DonationRecord d, IFieldEncryptionService enc) => new()
    {
        DonationId = d.DonationId,
        Date = d.Date,
        DonorId = d.IsAnonymous ? null : d.DonorId,
        DonorName = DonorName(d, enc),
        CategoryName = d.Category.Name,
        IsInKind = d.Category.IsInKind,
        Amount = enc.UnprotectAmount(d.AmountProtected),
        InKindDescription = enc.Unprotect(d.InKindDescriptionProtected),
        ReceiptId = d.Receipt is null ? null : d.DonationId,
        ReceiptNumber = d.Receipt?.ReceiptNumber,
        IsVoided = d.IsVoided
    };

    public static string? EmptyToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
