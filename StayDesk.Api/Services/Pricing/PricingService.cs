using StayDesk.Api.Domain.Entities;

namespace StayDesk.Api.Services.Pricing;

public class PricingService : IPricingService
{
    public decimal CalculateTotalPrice(RoomType roomType, IReadOnlyCollection<SeasonalRate> seasonalRates, DateOnly checkIn, DateOnly checkOut)
    {
        decimal total = 0m;

        for (var date = checkIn; date < checkOut; date = date.AddDays(1))
        {
            total += CalculateNightlyPrice(roomType, seasonalRates, date);
        }

        return decimal.Round(total, 2);
    }

    private static decimal CalculateNightlyPrice(RoomType roomType, IReadOnlyCollection<SeasonalRate> seasonalRates, DateOnly date)
    {
        var seasonal = seasonalRates.FirstOrDefault(r => date >= r.StartDate && date <= r.EndDate);
        if (seasonal is not null)
        {
            return seasonal.PricePerNight;
        }

        var isWeekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        if (!isWeekend)
        {
            return roomType.BasePricePerNight;
        }

        var surcharge = roomType.BasePricePerNight * (roomType.WeekendSurchargePercent / 100m);
        return roomType.BasePricePerNight + surcharge;
    }
}
