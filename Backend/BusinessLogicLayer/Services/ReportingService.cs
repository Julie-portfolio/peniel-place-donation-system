using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Data;
using PenielPlaceMinistriesCSharp.Security;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Services;

public sealed record DonationFact(
    int DonationId,
    DateTime Date,
    int DonationCategoryId,
    string CategoryName,
    int? DonorId,
    string DonorName,
    bool IsAnonymous,
    bool IsInKind,
    decimal? Amount,
    string? InKindDescription,
    string? PaymentMethod,
    string? ReceiptNumber);

public interface IReportingService
{
    Task<IReadOnlyList<DonationFact>> GetFactsAsync(
        DateTime? from, DateTime? to, int? categoryId = null, int? donorId = null, CancellationToken ct = default);

    Task<DashboardViewModel> BuildDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default);

    Task<ReportViewModel> BuildReportAsync(
        DateTime from, DateTime to, string groupBy, int? categoryId, int? donorId, CancellationToken ct = default);

    byte[] BuildCsv(ReportViewModel report);

    bool IsAllowedGroup(string? groupBy);
}

/// <summary>
/// Loads and decrypts donation records for reporting. Date/category/donor filters run in SQL
/// (those columns are not encrypted); monetary aggregation happens in memory because amounts
/// are encrypted at rest. For a high-volume deployment, consider a design that supports
/// protected aggregate queries instead.
/// </summary>
public sealed class ReportingService(AppDbContext db, IFieldEncryptionService enc) : IReportingService
{
    public bool IsAllowedGroup(string? groupBy) => groupBy is "category" or "donor" or "month";

    public async Task<IReadOnlyList<DonationFact>> GetFactsAsync(
        DateTime? from, DateTime? to, int? categoryId = null, int? donorId = null, CancellationToken ct = default)
    {
        var query = db.Donations.AsNoTracking()
            .Include(d => d.Category)
            .Include(d => d.Donor)
            .Include(d => d.Receipt)
            .Where(d => !d.IsVoided)   // voided donations never count toward any total
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
            query = query.Where(d => d.DonorId == donorId.Value && !d.IsAnonymous);

        var records = await query
            .OrderByDescending(d => d.Date)
            .ThenByDescending(d => d.DonationId)
            .ToListAsync(ct);

        return records.Select(d => new DonationFact(
            d.DonationId,
            d.Date,
            d.DonationCategoryId,
            d.Category.Name,
            d.IsAnonymous ? null : d.DonorId,
            Mapping.DonorName(d, enc),
            d.IsAnonymous,
            d.Category.IsInKind,
            enc.UnprotectAmount(d.AmountProtected),
            enc.Unprotect(d.InKindDescriptionProtected),
            enc.Unprotect(d.PaymentMethodProtected),
            d.Receipt?.ReceiptNumber)).ToList();
    }

    public async Task<DashboardViewModel> BuildDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var facts = await GetFactsAsync(from, to, ct: ct);

        return new DashboardViewModel
        {
            From = from,
            To = to,
            TotalMonetary = facts.Sum(d => d.Amount ?? 0m),
            DonationCount = facts.Count,
            InKindCount = facts.Count(d => d.IsInKind),
            DonorCount = facts.Where(d => d.DonorId.HasValue).Select(d => d.DonorId!.Value).Distinct().Count(),
            ByCategory = facts.GroupBy(d => d.CategoryName)
                .Select(g => new CategorySummary(g.Key, g.Sum(d => d.Amount ?? 0m), g.Count()))
                .OrderByDescending(g => g.Total).ThenBy(g => g.CategoryName).ToList(),
            ByMonth = facts.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
                .Select(g => new MonthSummary(g.Key, g.Sum(d => d.Amount ?? 0m)))
                .OrderBy(g => g.Month).ToList(),
            TopDonors = facts.Where(d => d.DonorId.HasValue)
                .GroupBy(d => new { Id = d.DonorId!.Value, d.DonorName })
                .Select(g => new DonorSummary(g.Key.Id, g.Key.DonorName, g.Count(), g.Sum(d => d.Amount ?? 0m)))
                .OrderByDescending(g => g.Total).ThenBy(g => g.DonorName).Take(10).ToList(),
            Recent = facts.OrderByDescending(d => d.Date).ThenByDescending(d => d.DonationId).Take(10)
                .Select(d => new DonationRow
                {
                    DonationId = d.DonationId,
                    Date = d.Date,
                    DonorId = d.DonorId,
                    DonorName = d.DonorName,
                    CategoryName = d.CategoryName,
                    IsInKind = d.IsInKind,
                    Amount = d.Amount,
                    InKindDescription = d.InKindDescription,
                    ReceiptId = d.ReceiptNumber is null ? null : d.DonationId,
                    ReceiptNumber = d.ReceiptNumber
                }).ToList()
        };
    }

    public async Task<ReportViewModel> BuildReportAsync(
        DateTime from, DateTime to, string groupBy, int? categoryId, int? donorId, CancellationToken ct = default)
    {
        var facts = await GetFactsAsync(from, to, categoryId, donorId, ct);

        return new ReportViewModel
        {
            From = from,
            To = to,
            GroupBy = groupBy,
            CategoryId = categoryId,
            DonorId = donorId,
            GeneratedAt = DateTime.Now,
            Rows = BuildRows(facts, groupBy),
            GrandCount = facts.Count,
            GrandTotal = facts.Sum(d => d.Amount ?? 0m)
        };
    }

    public byte[] BuildCsv(ReportViewModel report)
    {
        var inv = CultureInfo.InvariantCulture;
        var csv = new StringBuilder();

        csv.AppendLine("Group,Gift count,Money received");
        foreach (var row in report.Rows)
            csv.Append(CsvCell(row.Label)).Append(',').Append(row.Count).Append(',')
               .AppendLine(row.Total.ToString("0.00", inv));

        csv.Append("Total,").Append(report.GrandCount).Append(',')
           .AppendLine(report.GrandTotal.ToString("0.00", inv));

        // UTF-8 with BOM so Excel opens names with accents correctly.
        // (GetBytes alone never writes the BOM; it has to be prepended from GetPreamble.)
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return utf8.GetPreamble().Concat(utf8.GetBytes(csv.ToString())).ToArray();
    }

    private static IReadOnlyList<ReportRow> BuildRows(IReadOnlyList<DonationFact> facts, string groupBy)
    {
        if (groupBy == "month")
        {
            return facts.GroupBy(d => new DateTime(d.Date.Year, d.Date.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new ReportRow(
                    g.Key.ToString("MMM yyyy", CultureInfo.InvariantCulture), g.Count(), g.Sum(d => d.Amount ?? 0m)))
                .ToList();
        }

        if (groupBy == "donor")
        {
            return facts.GroupBy(d => d.DonorId ?? 0)
                .Select(g => new ReportRow(g.First().DonorName, g.Count(), g.Sum(d => d.Amount ?? 0m)))
                .OrderByDescending(r => r.Total).ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return facts.GroupBy(d => d.CategoryName)
            .Select(g => new ReportRow(g.Key, g.Count(), g.Sum(d => d.Amount ?? 0m)))
            .OrderByDescending(r => r.Total).ThenBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;
        // Protect spreadsheet users from formula injection if a label ever starts with a formula marker.
        var first = text.TrimStart();
        if (first.StartsWith('=') || first.StartsWith('+') || first.StartsWith('-') || first.StartsWith('@'))
            text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
