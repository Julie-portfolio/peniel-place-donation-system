using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PenielPlaceMinistriesCSharp.Models;
using PenielPlaceMinistriesCSharp.ViewModels;

namespace PenielPlaceMinistriesCSharp.Controllers;

public sealed class AccountController : Controller
{
    private const string PendingUserIdKey = "Pending2FAUserId";
    private const string PendingRememberMeKey = "Pending2FARememberMe";
    private const string PendingReturnUrlKey = "Pending2FAReturnUrl";
    private const int MaxSecondFactorAttempts = 5;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController (
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger )
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login ( string? returnUrl = null )
    {
        if ( User.Identity?.IsAuthenticated == true )
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel { ReturnUrl = SafeLocalUrl(returnUrl) });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login ( LoginViewModel model )
    {
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.ReturnUrl = SafeLocalUrl(model.ReturnUrl);

        if ( !ModelState.IsValid )
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if ( user is null )
        {
            ModelState.AddModelError(string.Empty, "The email address or password is incorrect.");
            return View(model);
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
        if ( !result.Succeeded )
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "Too many attempts. Try again after the lockout period."
                : "The email address or password is incorrect.");
            return View(model);
        }

        HttpContext.Session.SetString(PendingUserIdKey, user.Id);
        HttpContext.Session.SetString(PendingRememberMeKey, model.RememberMe ? "true" : "false");
        HttpContext.Session.SetString(PendingReturnUrlKey, model.ReturnUrl ?? string.Empty);
        HttpContext.Session.SetInt32("Pending2FAAttempts", 0);

        var authenticatorKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if ( !user.TwoFactorEnabled || string.IsNullOrWhiteSpace(authenticatorKey) )
            return RedirectToAction(nameof(SetupAuthenticator));

        return RedirectToAction(nameof(TwoFactor));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> SetupAuthenticator ()
    {
        var user = await GetPendingUserAsync();
        if ( user is null )
            return RedirectToAction(nameof(Login));

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if ( string.IsNullOrWhiteSpace(key) )
        {
            var reset = await _userManager.ResetAuthenticatorKeyAsync(user);
            if ( !reset.Succeeded )
            {
                _logger.LogError("Could not generate authenticator key for user {UserId}", user.Id);
                TempData["Error"] = "Could not set up two-factor authentication. Contact the administrator.";
                ClearPendingChallenge();
                return RedirectToAction(nameof(Login));
            }

            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        return View(BuildAuthenticatorModel(user, key ?? string.Empty));
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SetupAuthenticator ( AuthenticatorSetupViewModel model )
    {
        var user = await GetPendingUserAsync();
        if ( user is null )
            return RedirectToAction(nameof(Login));

        if ( !ModelState.IsValid )
            return await ShowSetupAgainAsync(user, model.Code);

        var provider = _userManager.Options.Tokens.AuthenticatorTokenProvider;
        var valid = await _userManager.VerifyTwoFactorTokenAsync(user, provider, model.Code);
        if ( !valid )
        {
            await _userManager.AccessFailedAsync(user);
            if ( await _userManager.IsLockedOutAsync(user) )
            {
                ClearPendingChallenge();
                TempData["Error"] = "Too many code attempts. Your account is temporarily locked.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(nameof(model.Code), "That code is not valid. Check the time on your device and try again.");
            return await ShowSetupAgainAsync(user, model.Code);
        }

        var enabled = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if ( !enabled.Succeeded )
        {
            _logger.LogError("Could not enable two-factor authentication for user {UserId}", user.Id);
            ModelState.AddModelError(string.Empty, "Two-factor authentication could not be enabled. Try again or contact the administrator.");
            return await ShowSetupAgainAsync(user, model.Code);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var rememberMe = HttpContext.Session.GetString(PendingRememberMeKey) == "true";
        var returnUrl = GetPendingReturnUrl();
        ClearPendingChallenge();
        await _signInManager.SignInAsync(user, isPersistent: rememberMe, authenticationMethod: "authenticator");
        return Redirect(returnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> TwoFactor ()
    {
        var user = await GetPendingUserAsync();
        if ( user is null )
            return RedirectToAction(nameof(Login));

        if ( !user.TwoFactorEnabled || string.IsNullOrWhiteSpace(await _userManager.GetAuthenticatorKeyAsync(user)) )
            return RedirectToAction(nameof(SetupAuthenticator));

        return View(new TwoFactorViewModel { ReturnUrl = GetPendingReturnUrl() });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TwoFactor ( TwoFactorViewModel model )
    {
        var user = await GetPendingUserAsync();
        if ( user is null )
            return RedirectToAction(nameof(Login));

        if ( !ModelState.IsValid )
        {
            model.ReturnUrl = GetPendingReturnUrl();
            return View(model);
        }

        var provider = _userManager.Options.Tokens.AuthenticatorTokenProvider;
        if ( !await _userManager.VerifyTwoFactorTokenAsync(user, provider, model.Code) )
        {
            await _userManager.AccessFailedAsync(user);
            var attempts = (HttpContext.Session.GetInt32("Pending2FAAttempts") ?? 0) + 1;
            HttpContext.Session.SetInt32("Pending2FAAttempts", attempts);

            if ( attempts >= MaxSecondFactorAttempts || await _userManager.IsLockedOutAsync(user) )
            {
                ClearPendingChallenge();
                TempData["Error"] = "Too many code attempts. Sign in again after the lockout period.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(nameof(model.Code), "The code is incorrect or has expired.");
            model.ReturnUrl = GetPendingReturnUrl();
            return View(model);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var rememberMe = HttpContext.Session.GetString(PendingRememberMeKey) == "true";
        var returnUrl = GetPendingReturnUrl();
        ClearPendingChallenge();
        await _signInManager.SignInAsync(user, isPersistent: rememberMe, authenticationMethod: "authenticator");
        return Redirect(returnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout ()
    {
        await _signInManager.SignOutAsync();
        ClearPendingChallenge();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied () => View();

    [HttpGet]
    [Authorize(Roles = Roles.Leadership)]
    public IActionResult CreateVolunteer () => View(new CreateVolunteerViewModel());

    [HttpPost]
    [Authorize(Roles = Roles.Leadership)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVolunteer ( CreateVolunteerViewModel model )
    {
        model.Email = model.Email?.Trim() ?? string.Empty;
        model.DisplayName = model.DisplayName?.Trim() ?? string.Empty;
        if ( !ModelState.IsValid )
            return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var createResult = await _userManager.CreateAsync(user, model.TemporaryPassword);
        if ( !createResult.Succeeded )
        {
            foreach ( var error in createResult.Errors )
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Roles.Volunteer);
        if ( !roleResult.Succeeded )
        {
            await _userManager.DeleteAsync(user);
            _logger.LogError("Could not assign Volunteer role to newly created account {Email}", model.Email);
            ModelState.AddModelError(string.Empty, "The account was not created because its role could not be assigned.");
            return View(model);
        }

        TempData["Success"] = "Volunteer account created. Give the temporary password to the volunteer securely; they will be required to set up an authenticator before accessing donation data.";
        return RedirectToAction(nameof(CreateVolunteer));
    }

    private async Task<ApplicationUser?> GetPendingUserAsync ()
    {
        var userId = HttpContext.Session.GetString(PendingUserIdKey);
        return string.IsNullOrWhiteSpace(userId) ? null : await _userManager.FindByIdAsync(userId);
    }

    private AuthenticatorSetupViewModel BuildAuthenticatorModel ( ApplicationUser user, string key )
    {
        var issuer = "Peniel Place Ministries";
        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? "account");
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var uri = $"otpauth://totp/{encodedIssuer}:{account}?secret={Uri.EscapeDataString(key)}&issuer={encodedIssuer}&digits=6&period=30";
        return new AuthenticatorSetupViewModel { SharedKey = key, AuthenticatorUri = uri };
    }

    private async Task<IActionResult> ShowSetupAgainAsync ( ApplicationUser user, string? code )
    {
        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if ( string.IsNullOrWhiteSpace(key) )
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var viewModel = BuildAuthenticatorModel(user, key ?? string.Empty);
        viewModel.Code = code ?? string.Empty;
        return View("SetupAuthenticator", viewModel);
    }

    private string GetPendingReturnUrl ()
    {
        var returnUrl = HttpContext.Session.GetString(PendingReturnUrlKey);
        return SafeLocalUrl(returnUrl) ?? Url.Action("Index", "Home")!;
    }

    private string? SafeLocalUrl ( string? url ) => !string.IsNullOrWhiteSpace(url) && Url.IsLocalUrl(url) ? url : null;

    private void ClearPendingChallenge ()
    {
        HttpContext.Session.Remove(PendingUserIdKey);
        HttpContext.Session.Remove(PendingRememberMeKey);
        HttpContext.Session.Remove(PendingReturnUrlKey);
        HttpContext.Session.Remove("Pending2FAAttempts");
    }
}

