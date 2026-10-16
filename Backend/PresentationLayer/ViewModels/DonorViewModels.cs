using System.ComponentModel.DataAnnotations;

namespace PenielPlaceMinistriesCSharp.ViewModels;

public class DonorFormViewModel
{
    public int DonorId { get; set; }

    [Required(ErrorMessage = "Enter the donor's full name.")]
    [StringLength(100, ErrorMessage = "The name must be 100 characters or fewer.")]
    [RegularExpression(@"^[\p{L}\p{M}\s.,'’\-]+$", ErrorMessage = "Use letters, spaces, and ordinary name punctuation only.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [EmailAddress(ErrorMessage = "Enter a valid email address, like name@example.com.")]
    [StringLength(254)]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Enter a valid phone number, like 082 123 4567.")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(250, ErrorMessage = "Notes must be 250 characters or fewer.")]
    public string? Notes { get; set; }

    /// <summary>Where to go after saving (for example back to the capture form).</summary>
    public string? ReturnUrl { get; set; }
}

public class DonorListItem
{
    public int DonorId { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int DonationCount { get; set; }
    public decimal TotalGiven { get; set; }
    public DateTime? LastDonationDate { get; set; }
}

public class DonorLookupViewModel
{
    [StringLength(100)]
    public string? Query { get; set; }
    public bool HasSearched { get; set; }
    public IReadOnlyList<DonorListItem> Results { get; set; } = Array.Empty<DonorListItem>();
}

/// <summary>Used by filter drop-downs.</summary>
public record DonorOption(int Id, string Name);

public class DonationRow
{
    public int DonationId { get; set; }
    public DateTime Date { get; set; }
    public int? DonorId { get; set; }
    public string DonorName { get; set; } = "Anonymous";
    public string CategoryName { get; set; } = "";
    public bool IsInKind { get; set; }
    public decimal? Amount { get; set; }
    public string? InKindDescription { get; set; }
    public int? ReceiptId { get; set; }
    public string? ReceiptNumber { get; set; }
    public bool IsVoided { get; set; }
}

public class DonorDetailsViewModel
{
    public int DonorId { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public decimal TotalGiven { get; set; }
    public IReadOnlyList<DonationRow> Donations { get; set; } = Array.Empty<DonationRow>();
}
