using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenClient.Interfaces;
using OpenClient.Models.DTO;

namespace OpenClient.Controllers;

/// <summary>
/// API REST del catálogo de clientes. Comparte <see cref="IClientService"/> con
/// el panel Blazor (que lo consume en proceso, sin pasar por HTTP).
/// </summary>
[ApiController]
[Authorize]
[Route("api/clients")]
[Produces("application/json")]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly ILogger<ClientsController> _logger;

    public ClientsController(
        IClientService clientService,
        ILogger<ClientsController> logger)
    {
        _clientService = clientService;
        _logger = logger;
    }

    /// <summary>GET /api/clients?search=&amp;industry=&amp;sortBy=&amp;page=&amp;pageSize=</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClientListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> GetClients(
        [FromQuery] ClientSearchFilter filter,
        CancellationToken cancellationToken)
    {
        var result = await _clientService.GetClientsAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>GET /api/clients/industries</summary>
    [HttpGet("industries")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetIndustries(
        CancellationToken cancellationToken)
    {
        var industries = await _clientService.GetIndustriesAsync(cancellationToken);
        return Ok(industries);
    }

    /// <summary>GET /api/clients/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClientDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetailDto>> GetClientById(
        int id,
        CancellationToken cancellationToken)
    {
        var client = await _clientService.GetByIdAsync(id, cancellationToken);

        return client is null
            ? NotFound()
            : Ok(ClientDetailDto.FromListItem(client));
    }

    /// <summary>POST /api/clients</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClientDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateClient(
        [FromBody] CreateClientDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _clientService.CreateAsync(dto.ToEditModel(), cancellationToken);
            var created = await _clientService.GetByIdAsync(id, cancellationToken);

            _logger.LogInformation("POST /api/clients → creado Id={ClientId}.", id);

            return CreatedAtAction(
                nameof(GetClientById),
                new { id },
                created is null ? null : ClientDetailDto.FromListItem(created));
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(ToModelState(ex));
        }
    }

    /// <summary>PUT /api/clients/{id}</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClient(
        int id,
        [FromBody] UpdateClientDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _clientService.UpdateAsync(id, dto.ToEditModel(), cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (ValidationException ex)
        {
            return ValidationProblem(ToModelState(ex));
        }
    }

    /// <summary>DELETE /api/clients/{id} (borrado lógico)</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClient(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _clientService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static ModelStateDictionary ToModelState(ValidationException exception)
    {
        var modelState = new ModelStateDictionary();

        foreach (var error in exception.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return modelState;
    }
}
