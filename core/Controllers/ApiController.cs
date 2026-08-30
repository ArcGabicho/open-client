using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenClient.Api;
using OpenClient.Interfaces;
using OpenClient.Models.Api;

namespace OpenClient.Controllers.Api.V1;

[ApiController]
[Authorize(Policy = ApiV1.ReadPolicy)]
[Route(ApiV1.RoutePrefix + "/clients")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
public sealed class ClientsController : ControllerBase
{
    private readonly IApiClientService _service;

    public ClientsController(IApiClientService service)
    {
        _service = service;
    }

    [HttpGet(Name = "ApiV1_GetClients")]
    [ProducesResponseType(typeof(ApiPagedResponse<ApiClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetClients(
        [FromQuery] int page = ApiPaging.DefaultPage,
        [FromQuery] int pageSize = ApiPaging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!ApiPaging.TryValidate(page, pageSize, out var error))
        {
            return BadRequest(error);
        }

        var result = await _service.GetClientsAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("search", Name = "ApiV1_SearchClients")]
    [ProducesResponseType(typeof(ApiPagedResponse<ApiClientDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchClients(
        [FromQuery] ApiClientSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiPaging.TryValidate(request.Page, request.PageSize, out var error))
        {
            return BadRequest(error);
        }

        var result = await _service.SearchClientsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}", Name = "ApiV1_GetClientById")]
    [ProducesResponseType(typeof(ApiClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientById(
        int id,
        CancellationToken cancellationToken)
    {
        var client = await _service.GetClientByIdAsync(id, cancellationToken);

        return client is null
            ? NotFound(ApiErrorResponse.Create(
                "client_not_found",
                "The requested client was not found."))
            : Ok(client);
    }
}