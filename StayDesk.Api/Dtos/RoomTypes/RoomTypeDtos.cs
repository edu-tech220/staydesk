using System.ComponentModel.DataAnnotations;

namespace StayDesk.Api.Dtos.RoomTypes;

public record RoomTypeDto(
    int Id,
    string Name,
    string Description,
    int Capacity,
    int TotalRooms,
    decimal BasePricePerNight,
    decimal WeekendSurchargePercent);

public record RoomTypeAvailabilityDto(
    int RoomTypeId,
    string Name,
    string Description,
    int Capacity,
    int TotalRooms,
    int RoomsAvailable,
    int Nights,
    decimal TotalPrice);

public record SeasonalRateDto(int Id, int RoomTypeId, string Name, DateOnly StartDate, DateOnly EndDate, decimal PricePerNight);

public record CreateSeasonalRateRequest(
    [Required, MaxLength(100)] string Name,
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    [Range(0.01, 1_000_000)] decimal PricePerNight);
