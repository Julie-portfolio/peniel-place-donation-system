using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenielPlaceMinistriesCSharp.Models;

namespace PenielPlaceMinistriesCSharp.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    /// <summary>Sends each visitor to the right starting page for their role.</summary>
    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        return User.IsInRole(Roles.Leadership)
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Create", "Donation");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        ViewData["RequestId"] = requestId;
        return View(new ErrorViewModel { RequestId = requestId });
    }
}
