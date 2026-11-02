using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Services;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Controllers;

[Authorize(Roles = Roles.Leadership)]
public sealed class ReportController(
    IReportingService reporting,
    IDonationService donations,
    IDonorService donors) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from = null, DateTime? to = null, string groupBy = "category",
        int? categoryId = null, int? donorId = null, CancellationToken ct = default)
    {
        var start = from?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = to?.Date ?? DateTime.Today;
        groupBy = (groupBy ?? "category").Trim().ToLowerInvariant();

        ReportViewModel model;
        if (!DateRules.IsValidRange(start, end) || !reporting.IsAllowedGroup(groupBy))
        {
            ModelState.AddModelError(string.Empty, "Choose a valid date range and grouping option.");
            model = new ReportViewModel
            {
                From = start, To = end, GroupBy = reporting.IsAllowedGroup(groupBy) ? groupBy : "category",
                CategoryId = categoryId, DonorId = donorId, GeneratedAt = DateTime.Now
            };
        }
        else
        {
            model = await reporting.BuildReportAsync(start, end, groupBy, categoryId, donorId, ct);
        }

        model.Categories = await donations.CategoriesAsync(ct);
        model.Donors = await donors.OptionsAsync(ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        DateTime? from, DateTime? to, string groupBy = "category",
        int? categoryId = null, int? donorId = null, CancellationToken ct = default)
    {
        if (from is null || to is null || !DateRules.IsValidRange(from.Value, to.Value))
            return BadRequest("A valid date range is required.");

        groupBy = (groupBy ?? "category").Trim().ToLowerInvariant();
        if (!reporting.IsAllowedGroup(groupBy))
            return BadRequest("Choose a valid grouping option.");

        var report = await reporting.BuildReportAsync(from.Value.Date, to.Value.Date, groupBy, categoryId, donorId, ct);

        return File(reporting.BuildCsv(report), "text/csv; charset=utf-8", "peniel-place-donation-report.csv");
    }
}
