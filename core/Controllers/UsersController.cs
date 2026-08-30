using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Interfaces;
using OpenClient.Models.Api;
using OpenClient.Models.DTO;
using OpenClient.Models.DTO.Users;

namespace OpenClient.Controllers;

// Administración de cuentas del panel. Autorización en backend: política
// Users.Admin (rol Admin). El servicio revalida el principal, así que ni la UI
// Blazor ni la API pueden saltarse el control.
[ApiController]
[Authorize(Policy = UserRoles.AdminPolicy)]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;

    public UsersController(IUserService users)
    {
        _users = users;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] UserSearchFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _users.GetUsersAsync(User, filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public IActionResult GetRoles() => Ok(_users.GetAvailableRoles());

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(int id, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(User, id, cancellationToken);
        return user is null
            ? NotFound(ApiErrorResponse.Create("user_not_found", "El usuario no existe."))
            : Ok(user);
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _users.CreateAsync(User, request, cancellationToken);
        if (!result.Succeeded)
        {
            return MapFailure(result);
        }

        var created = await _users.GetByIdAsync(User, result.UserId!.Value, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = result.UserId!.Value }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateUser(
        int id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _users.UpdateAsync(User, id, request, cancellationToken);
        if (!result.Succeeded)
        {
            return MapFailure(result);
        }

        var updated = await _users.GetByIdAsync(User, id, cancellationToken);
        return Ok(updated);
    }

    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken) =>
        Resolve(await _users.SetActiveAsync(User, id, active: true, cancellationToken));

    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        Resolve(await _users.SetActiveAsync(User, id, active: false, cancellationToken));

    [HttpPut("{id:int}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignRole(
        int id,
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken) =>
        Resolve(await _users.AssignRoleAsync(User, id, request.Role, cancellationToken));

    [HttpDelete("{id:int}/role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveRole(
        int id,
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken) =>
        Resolve(await _users.RemoveRoleAsync(User, id, request.Role, cancellationToken));

    [HttpPost("{id:int}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePassword(
        int id,
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken) =>
        Resolve(await _users.ChangePasswordAsync(User, id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteUser(int id, CancellationToken cancellationToken) =>
        Resolve(await _users.DeleteAsync(User, id, cancellationToken));

    // ---------- Mapa de resultados ----------

    private IActionResult Resolve(UserMutationResult result) =>
        result.Succeeded ? NoContent() : MapFailure(result);

    private IActionResult MapFailure(UserMutationResult result)
    {
        var error = ApiErrorResponse.Create(result.Code ?? "error", result.Message ?? "La operación no se pudo completar.");

        return result.Status switch
        {
            UserMutationStatus.ValidationFailed => BadRequest(new
            {
                error = new { code = result.Code, message = result.Message, details = result.ValidationErrors }
            }),
            UserMutationStatus.NotFound => NotFound(error),
            UserMutationStatus.Conflict => Conflict(error),
            UserMutationStatus.Forbidden => StatusCode(StatusCodes.Status409Conflict, error),
            _ => BadRequest(error)
        };
    }
}
