using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Models.DTO;
using OpenClient.Services;

namespace OpenClient.Controllers;

// Autenticacion por HTTP tradicional (formularios POST + redirects 302).
//
// Por que NO un endpoint JSON consumido por fetch:
//   1. SignInAsync debe escribir la cookie Set-Cookie en una respuesta HTTP
//      real; en un circuito Blazor la respuesta ya fue enviada.
//   2. Un POST de formulario lleva el token antiforgery nativamente
//      (__RequestVerificationToken); un fetch de JS interop no.
//   3. El redirect 302 del navegador evita cualquier navegacion desde el
//      circuito, eliminando los errores "WebSocket is not in the OPEN state".
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    private readonly IAntiforgery _antiforgery;

    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthService authService,
        IAntiforgery antiforgery,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _antiforgery = antiforgery;
        _logger = logger;
    }

    // Un GET directo al endpoint de login solo debe mostrar el formulario.
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
        // Validación antiforgery explícita con IAntiforgery.
        //
        // No se usa [ValidateAntiForgeryToken]: ese filtro requiere los
        // servicios de MVC ViewFeatures (AddControllersWithViews), que una
        // Blazor Web App no registra. IAntiforgery es el mismo mecanismo que
        // usa el middleware UseAntiforgery() y ya está registrado.
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

        _logger.LogInformation(
            "Sesion iniciada para UserId={UserId} via cookie.",
            user.Id);

        return LocalRedirect(SanitizeReturnUrl(returnUrl));
    }

    // Logout por GET con navegacion forzada desde Blazor:
    //
    // - GET y no POST: <AntiforgeryToken /> no es fiable dentro de un circuito
    //   interactivo (el token solo se emite durante SSR con HttpContext real),
    //   y un POST fallaria con 400 tras una navegacion client-side.
    // - El riesgo de logout-CSRF se acepta documentadamente: el peor caso es
    //   cerrar la sesion de otra persona (molesta, sin escalada de privilegios)
    //   y el destino del redirect esta fijado a /log-in.
    // - Idempotente: si no hay sesion, SignOutAsync no hace nada y redirige.
    [HttpGet("log-out")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        _logger.LogInformation(
            "Sesion cerrada; cookie de autenticacion eliminada.");

        return Redirect("/log-in");
    }

    // Solo URLs locales: bloquea open redirect ("//evil.com", "/\evil.com",
    // "https://evil.com").
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
