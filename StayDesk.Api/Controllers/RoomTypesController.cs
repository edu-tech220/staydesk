using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.RoomTypes;
using StayDesk.Api.Services.RoomTypes;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/room-types")]
public class RoomTypesController : ControllerBase
{
    private readonly IRoomTypeService _roomTypeService;

    public RoomTypesController(IRoomTypeService roomTypeService)
    {
        _roomTypeService = roomTypeService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<RoomTypeDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await _roomTypeService.GetAllAsync(page, pageSize, ct));
    }

    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<RoomTypeAvailabilityDto>>> SearchAvailability(
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] int guests = 1,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _roomTypeService.SearchAvailabilityAsync(checkIn, checkOut, guests, page, pageSize, ct));
    }

    [HttpGet("{roomTypeId:int}/seasonal-rates")]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<IReadOnlyList<SeasonalRateDto>>> GetSeasonalRates(int roomTypeId, CancellationToken ct)
    {
        return Ok(await _roomTypeService.GetSeasonalRatesAsync(roomTypeId, ct));
    }

    [HttpPost("{roomTypeId:int}/seasonal-rates")]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<SeasonalRateDto>> AddSeasonalRate(int roomTypeId, CreateSeasonalRateRequest request, CancellationToken ct)
    {
        var created = await _roomTypeService.AddSeasonalRateAsync(roomTypeId, request, ct);
        return CreatedAtAction(nameof(GetSeasonalRates), new { roomTypeId }, created);
    }
}
