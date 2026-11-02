using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Services;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Controllers;

[Authorize(Roles = Roles.Staff)]
public sealed class DonationController(
    IDonationService donations,
    IDonorService donors,
    ILogger<DonationController> logger) : Controller
{
    /// <summary>Set by the offline queue in site.js when it replays a saved donation.</summary>
    private bool IsOfflineReplay => Request.Headers.ContainsKey("X-Offline-Sync");

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? from, DateTime? to, int? categoryId, int? donorId, CancellationToken ct)
    {
        var model = new DonationListViewModel { From = from, To = to, CategoryId = categoryId, DonorId = donorId };

        try
        {
            var (rows, truncated) = await donations.ListAsync(from, to, categoryId, donorId, ct);
            model.Rows = rows;
            model.Truncated = truncated;
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        model.Categories = await donations.CategoriesAsync(ct);
        model.Donors = await donors.OptionsAsync(ct);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Void(int id, CancellationToken ct)
    {
        var donation = await donations.GetRowAsync(id, ct);
        if (donation is null) return NotFound();

        if (donation.IsVoided)
        {
            TempData["Error"] = "That donation has already been voided.";
            return RedirectToAction("Details", "Receipt", new { id });
        }

        return View(new VoidDonationViewModel { DonationId = id, Donation = donation });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Void(VoidDonationViewModel model, CancellationToken ct)
    {
        model.Donation = await donations.GetRowAsync(model.DonationId, ct);
        if (model.Donation is null) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var who = User.Identity?.Name ?? "Unknown";

                if (!await donations.VoidAsync(model.DonationId, model.Reason, userId, who, ct))
                    return NotFound();

                logger.LogWarning("Donation {DonationId} VOIDED by {User} ({UserId})", model.DonationId, who, userId);
                TempData["Success"] = "Donation voided. It no longer counts toward any totals.";
                return RedirectToAction("Details", "Receipt", new { id = model.DonationId });
            }
            catch (ValidationFailureException ex)
            {
                ModelState.AddModelError(ex.Field, ex.Message);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Could not void donation {DonationId}", model.DonationId);
                ModelState.AddModelError(string.Empty, "The donation could not be voided. Please try again.");
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? donorId = null, CancellationToken ct = default)
    {
        var model = new DonationCreateViewModel { DonationDate = DateTime.Today };

        if (donorId is > 0)
        {
            var name = await donors.GetNameAsync(donorId.Value, ct);
            if (name is not null)
            {
                model.DonorId = donorId.Value;
                model.DonorName = name;
            }
        }

        model.Categories = await donations.CategoriesAsync(ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DonationCreateViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var issuedBy = User.Identity?.Name ?? "System";
                var id = await donations.CreateAsync(model, userId, issuedBy, ct);

                TempData["Success"] = "Donation recorded and receipt issued.";
                return RedirectToAction("Details", "Receipt", new { id });
            }
            catch (ValidationFailureException ex)
            {
                ModelState.AddModelError(ex.Field, ex.Message);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Could not save donation record");
                ModelState.AddModelError(string.Empty,
                    "The donation could not be saved. Please check the details and try again.");
            }
        }

        if (IsOfflineReplay)
            return BadRequest(new { error = "The donation was rejected." });

        if (model.DonorId is > 0 && !model.IsAnonymous)
            model.DonorName = await donors.GetNameAsync(model.DonorId.Value, ct);
        else
            model.DonorName = null;

        model.Categories = await donations.CategoriesAsync(ct);
        return View(model);
    }
}
