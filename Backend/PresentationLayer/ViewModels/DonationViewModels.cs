using System.ComponentModel.DataAnnotations;

namespace PenielPlaceMinistriesCSharp.ViewModels;

/// <summary>One row of the DonationCategory table (Tithe, Offering, Once-off gift, In-kind donation).</summary>
public record CategoryOption(int Id, string Name, bool IsInKind);

/// <summary>
/// Form model for recording a donation. Only per-field rules live here. Rules that depend on the
/// chosen category (amount vs. description, donor vs. anonymous) are enforced in DonationService,
/// which always uses the category row from the database and never trusts <see cref="IsInKind"/>.
/// </summary>
public class DonationCreateViewModel
{
    [Required(ErrorMessage = "Choose the type of gift.")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a valid type of gift.")]
    public int? CategoryId { get; set; }

    // UI hint only.
    public bool IsInKind { get; set; }

    [Range(typeof(decimal), "0.01", "10000000", ErrorMessage = "Enter an amount between R 0.01 and R 10 000 000.")]
    public decimal? Amount { get; set; }

    [StringLength(200, ErrorMessage = "Describe the items in 200 characters or fewer.")]
    [Display(Name = "What was donated")]
    public string? InKindDescription { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a valid donor.")]
    public int? DonorId { get; set; }

    // Display convenience only; the server looks the donor up by id.
    public string? DonorName { get; set; }
    public bool IsAnonymous { get; set; }

    [Required(ErrorMessage = "Choose the date of the gift.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    public DateTime? DonationDate { get; set; } = DateTime.Today;

    [StringLength(40)]
    public string? PaymentMethod { get; set; }

    [StringLength(250, ErrorMessage = "Notes must be 250 characters or fewer.")]
    public string? Notes { get; set; }

    public IReadOnlyList<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
}

/// <summary>Leadership confirmation form for voiding a donation.</summary>
public class VoidDonationViewModel
{
    public int DonationId { get; set; }

    [Required(ErrorMessage = "Explain why this donation is being voided.")]
    [StringLength(250, MinimumLength = 5, ErrorMessage = "The reason must be between 5 and 250 characters.")]
    public string Reason { get; set; } = "";

    // Display only. Reloaded from the database on every request.
    public DonationRow? Donation { get; set; }
}

/// <summary>Filterable list of recorded donations.</summary>
public class DonationListViewModel
{
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public int? CategoryId { get; set; }
    public int? DonorId { get; set; }

    public IReadOnlyList<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public IReadOnlyList<DonorOption> Donors { get; set; } = Array.Empty<DonorOption>();
    public IReadOnlyList<DonationRow> Rows { get; set; } = Array.Empty<DonationRow>();

    /// <summary>True when more rows matched than are shown.</summary>
    public bool Truncated { get; set; }
}
