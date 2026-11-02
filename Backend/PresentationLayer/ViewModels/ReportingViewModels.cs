using System.ComponentModel.DataAnnotations;

namespace PenielPlaceMinistriesCSharp.ViewModels;

// Keep these view-model types defined in ONE file only.
public sealed record CategorySummary(string CategoryName, decimal Total, int Count);
public sealed record MonthSummary(DateTime Month, decimal Total);
public sealed record DonorSummary(int DonorId, string DonorName, int Count, decimal Total);
public sealed record ReportRow(string Label, int Count, decimal Total);

public class DashboardViewModel : IValidatableObject
{
    [DataType(DataType.Date)]
    public DateTime From { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [DataType(DataType.Date)]
    public DateTime To { get; set; } = DateTime.Today;

    public decimal TotalMonetary { get; set; }
    public int DonationCount { get; set; }
    public int InKindCount { get; set; }
    public int DonorCount { get; set; }
    public IReadOnlyList<CategorySummary> ByCategory { get; set; } = Array.Empty<CategorySummary>();
    public IReadOnlyList<MonthSummary> ByMonth { get; set; } = Array.Empty<MonthSummary>();
    public IReadOnlyList<DonorSummary> TopDonors { get; set; } = Array.Empty<DonorSummary>();
    public IReadOnlyList<DonationRow> Recent { get; set; } = Array.Empty<DonationRow>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (To.Date < From.Date)
            yield return new ValidationResult(
                "The end date must be on or after the start date.", new[] { nameof(To) });
    }
}

public class ReportViewModel : IValidatableObject
{
    [DataType(DataType.Date)]
    public DateTime From { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [DataType(DataType.Date)]
    public DateTime To { get; set; } = DateTime.Today;

    [RegularExpression("^(category|donor|month)$", ErrorMessage = "Choose a valid report grouping.")]
    public string GroupBy { get; set; } = "category";

    /// <summary>Optional filter: only this category.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Optional filter: only this donor.</summary>
    public int? DonorId { get; set; }

    public IReadOnlyList<CategoryOption> Categories { get; set; } = Array.Empty<CategoryOption>();
    public IReadOnlyList<DonorOption> Donors { get; set; } = Array.Empty<DonorOption>();

    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public IReadOnlyList<ReportRow> Rows { get; set; } = Array.Empty<ReportRow>();
    public decimal GrandTotal { get; set; }
    public int GrandCount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (To.Date < From.Date)
            yield return new ValidationResult(
                "The end date must be on or after the start date.", new[] { nameof(To) });
    }
}

public class ReceiptViewModel
{
    public int DonationId { get; set; }
    public int ReceiptId { get; set; }
    public string ReceiptNumber { get; set; } = "";
    public string DonorName { get; set; } = "Anonymous";
    public DateTime DonationDate { get; set; }
    public string CategoryName { get; set; } = "";
    public bool IsInKind { get; set; }
    public string? InKindDescription { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? Amount { get; set; }
    public DateTime IssuedDate { get; set; }
    public string IssuedBy { get; set; } = "";

    public bool IsVoided { get; set; }
    public DateTime? VoidedDate { get; set; }
    public string? VoidedBy { get; set; }
    public string? VoidReason { get; set; }
}
