using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.Services;

namespace PenielPlaceMinistriesCSharp.Controllers;

[Authorize(Roles = Roles.Staff)]
public sealed class ReceiptController(IReceiptService receipts) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var receipt = await receipts.GetForDonationAsync(id, ct);
        return receipt is null ? NotFound() : View(receipt);
    }
}
