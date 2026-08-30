using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Interfaces;
using OpenClient.Models.DTO;

namespace OpenClient.Controllers;

[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    private readonly IAntiforgery _antiforgery;

    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IAntiforgery antiforgery,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    [HttpGet("log-in")]
    public IActionResult LoginRedirect()
    {
        return Redirect("/log-in");
    }

    [HttpPost("log-in")]
    public async Task<IActionResult> Login(
        [FromForm] LoginModel model,
        [FromForm] string? returnUrl)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            _logger.LogWarning(
                ex,
                "Token antiforgery ausente o inválido en POST /auth/log-in.");

            return RedirectToLogin("form_expired", returnUrl);
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning(
                "Modelo de login invalido para {Email}.",
                model.Email);

            return RedirectToLogin("invalid_input", returnUrl);
        }

        var user = await _authService.ValidateCredentialsAsync(model);

        if (user is null)
        {
            return RedirectToLogin("invalid_credentials", returnUrl);
        }

        var claims = _authService.CreateClaims(user);

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });

        await _authService.RecordSuccessfulLoginAsync(user.Id, HttpContext.RequestAborted);

        _logger.LogInformation(
            "Sesion iniciada para UserId={UserId} via cookie.",
            user.Id);

        return LocalRedirect(SanitizeReturnUrl(returnUrl));
    }

    [HttpGet("log-out")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation(
            "Sesion cerrada; cookie de autenticacion eliminada.");

        return Redirect("/log-in");
    }

    private string SanitizeReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : "/dashboard";
    }

    private IActionResult RedirectToLogin(string errorCode, string? returnUrl)
    {
        var url = $"/log-in?error={errorCode}";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return Redirect(url);
    }
}