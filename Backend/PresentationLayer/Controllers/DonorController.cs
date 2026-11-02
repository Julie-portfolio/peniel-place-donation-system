using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Services;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Controllers;

[Authorize(Roles = Roles.Staff)]
public sealed class DonorController(IDonorService donors, ILogger<DonorController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DonorLookupViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            model.Results = Array.Empty<DonorListItem>();
            return View(model);
        }

        var query = model.Query?.Trim();
        model.HasSearched = !string.IsNullOrWhiteSpace(query);
        model.Results = model.HasSearched
            ? await donors.SearchAsync(query!, ct)
            : Array.Empty<DonorListItem>();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? term, CancellationToken ct)
    {
        term = term?.Trim();
        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return Json(Array.Empty<object>());
        if (term.Length > 100)
            return BadRequest(new { error = "Search text must be 100 characters or fewer." });

        return Json(await donors.QuickSearchAsync(term, ct));
    }

    [HttpGet]
    public IActionResult Create(string? returnUrl = null) =>
        View(new DonorFormViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DonorFormViewModel model, CancellationToken ct)
    {
        model.FullName = model.FullName?.Trim() ?? string.Empty;
        model.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        model.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        model.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;

        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var donor = await donors.CreateAsync(model, User.FindFirstValue(ClaimTypes.NameIdentifier), ct);

            TempData["Success"] = "Donor saved.";
            return model.ReturnUrl is not null
                ? Redirect(model.ReturnUrl)
                : RedirectToAction(nameof(Details), new { id = donor.DonorId });
        }
        catch (ValidationFailureException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            return View(model);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Could not save donor record");
            ModelState.AddModelError(string.Empty, "The donor could not be saved. Check the details and try again.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await donors.GetDetailsAsync(id, ct);
        return details is null ? NotFound() : View(details);
    }
}
