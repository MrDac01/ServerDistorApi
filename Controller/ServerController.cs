using Microsoft.AspNetCore.Mvc;
using ServerDistorApi.Contracts;
using ServerDistorApi.Model;
using ServerDistorApi.Services;

namespace ServerDistorApi.Controller;

[ApiController]
[Route("api")]
public class ServerController : ControllerBase
{
    private readonly IServerServices _serverServices;

    public ServerController(IServerServices serverServices)
    {
        _serverServices = serverServices;
    }

    [HttpGet("servers")]
    public async Task<ActionResult<IReadOnlyList<Server>>> GetServers(CancellationToken ct)
    {
        var servers = await _serverServices.GetServersAsync(ct);
        return Ok(servers);
    }

    [HttpPost("servers")]
    public async Task<ActionResult<Server>> AddServer([FromBody] CreateServerRequest request, CancellationToken ct)
    {
        var server = await _serverServices.AddServerAsync(request, ct);
        return CreatedAtAction(nameof(GetServerById), new { id = server.Id }, server);
    }

    [HttpGet("servers/{id:int}")]
    public async Task<ActionResult<Server>> GetServerById(int id, CancellationToken ct)
    {
        var server = await _serverServices.GetServerByIdAsync(id, ct);
        return server is null ? NotFound() : Ok(server);
    }

    [HttpGet("servers/search")]
    public async Task<ActionResult<IReadOnlyList<Server>>> SearchFreeServers(
        [FromQuery] string? os,
        [FromQuery] int? minHardwareGb,
        [FromQuery] int? minCoreCpu,
        CancellationToken ct)
    {
        var servers = await _serverServices.SearchFreeServersAsync(os, minHardwareGb, minCoreCpu, ct);
        return Ok(servers);
    }

    [HttpPost("rentals")]
    public async Task<ActionResult<Rental>> RentServer([FromBody] CreateRentalRequest request, CancellationToken ct)
    {
        var result = await _serverServices.RentServerAsync(request, ct);

        return result.Error switch
        {
            RentServerError.ServerNotFound => NotFound($"Server {request.ServerId} not found."),
            RentServerError.Busy => Conflict("Server is already busy."),
            RentServerError.StateChanged => Conflict("Server state changed, retry request."),
            _ => Created($"/api/rentals/{result.Rental!.Id}", result.Rental)
        };
    }

    [HttpGet("rentals/{rentalId:int}/readiness")]
    public async Task<ActionResult<RentalReadinessResponse>> GetReadiness(int rentalId, CancellationToken ct)
    {
        var response = await _serverServices.GetReadinessAsync(rentalId, ct);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("rentals/{rentalId:int}/release")]
    public async Task<ActionResult> ReleaseRental(int rentalId, CancellationToken ct)
    {
        var result = await _serverServices.ReleaseRentalAsync(rentalId, ct);

        return result.Error switch
        {
            ReleaseRentalError.RentalNotFound => NotFound(),
            _ => NoContent()
        };
    }
}