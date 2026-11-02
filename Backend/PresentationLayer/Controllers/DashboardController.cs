using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Services;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Controllers;

[Authorize(Roles = Roles.Leadership)]
public sealed class DashboardController(IReportingService reporting) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var start = from?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = to?.Date ?? DateTime.Today;

        if (!DateRules.IsValidRange(start, end))
        {
            ModelState.AddModelError(string.Empty,
                "Choose a date range from 1 January 2000 through today, with the start date no later than the end date.");
            return View(new DashboardViewModel { From = start, To = end });
        }

        return View(await reporting.BuildDashboardAsync(start, end, ct));
    }
}
