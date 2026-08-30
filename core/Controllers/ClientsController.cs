using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Interfaces;
using OpenClient.Models.DTO;

namespace OpenClient.Controllers;

/// <summary>
/// API JSON de solo lectura sobre el catálogo de clientes. La comparte el
/// producto (sección "API") y sirve para exportaciones; el panel Blazor
/// consume <see cref="IClientService"/> directamente, sin pasar por HTTP.
/// </summary>
[ApiController]
[Authorize]
[Route("api/clients")]
[Produces("application/json")]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService;
    }

    /// <summary>GET /api/clients?search=&amp;industry=&amp;sortBy=&amp;page=&amp;pageSize=</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> GetClients(
        [FromQuery] ClientSearchFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _clientService.GetClientsAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/clients/industries</summary>
    [HttpGet("industries")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetIndustries(
        CancellationToken cancellationToken)
    {
        var industries = await _clientService.GetIndustriesAsync(cancellationToken);
        return Ok(industries);
    }
}
