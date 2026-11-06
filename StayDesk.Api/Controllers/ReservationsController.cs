using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StayDesk.Api.Common;
using StayDesk.Api.Domain.Enums;
using StayDesk.Api.Dtos.Common;
using StayDesk.Api.Dtos.Reservations;
using StayDesk.Api.Services.Reservations;

namespace StayDesk.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Reservations)]
    public async Task<ActionResult<ReservationDto>> Create(CreateReservationRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _reservationService.CreateAsync(userId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<ReservationDto>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        return Ok(await _reservationService.GetMineAsync(userId, page, pageSize, ct));
    }

    [HttpGet]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<PagedResult<ReservationDto>>> GetAll(
        [FromQuery] ReservationStatus? status,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _reservationService.GetAllAsync(status, from, to, page, pageSize, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationDto>> GetById(int id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var role = User.IsInRole("FrontDesk") ? UserRole.FrontDesk : UserRole.Guest;
        return Ok(await _reservationService.GetByIdAsync(id, userId, role, ct));
    }

    [HttpPut("{id:int}/confirm")]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<ReservationDto>> Confirm(int id, CancellationToken ct)
    {
        return Ok(await _reservationService.ConfirmAsync(id, ct));
    }

    [HttpPut("{id:int}/check-in")]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<ReservationDto>> CheckIn(int id, CancellationToken ct)
    {
        return Ok(await _reservationService.CheckInAsync(id, ct));
    }

    [HttpPut("{id:int}/check-out")]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<ReservationDto>> CheckOut(int id, CancellationToken ct)
    {
        return Ok(await _reservationService.CheckOutAsync(id, ct));
    }

    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(int id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var role = User.IsInRole("FrontDesk") ? UserRole.FrontDesk : UserRole.Guest;
        return Ok(await _reservationService.CancelAsync(id, userId, role, ct));
    }

    [HttpPut("{id:int}/no-show")]
    [Authorize(Roles = "FrontDesk")]
    public async Task<ActionResult<ReservationDto>> MarkNoShow(int id, CancellationToken ct)
    {
        return Ok(await _reservationService.MarkNoShowAsync(id, ct));
    }
}
