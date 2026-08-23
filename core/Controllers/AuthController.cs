using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Models.DTO;
using OpenClient.Services;

namespace OpenClient.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("log-in")]
    public async Task<IActionResult> Login([FromBody] LoginModel model)
    {
        var user = await _authService.ValidateCredentialsAsync(model);

        if (user is null)
            return Unauthorized();

        var claims = _authService.CreateClaims(user);

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        return Ok();
    }

    [HttpPost("log-out")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return Redirect("/log-in");
    }
}