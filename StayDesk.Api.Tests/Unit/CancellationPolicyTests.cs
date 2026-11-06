using StayDesk.Api.Services.Reservations;
using Xunit;

namespace StayDesk.Api.Tests.Unit;

public class CancellationPolicyTests
{
    private static readonly DateOnly Today = new(2026, 6, 10);

    [Fact]
    public void CalculatePenalty_ThreeOrMoreDaysBefore_IsFree()
    {
        var penalty = CancellationPolicy.CalculatePenalty(2000m, Today.AddDays(3), Today);
        Assert.Equal(0m, penalty);
    }

    [Fact]
    public void CalculatePenalty_WellInAdvance_IsFree()
    {
        var penalty = CancellationPolicy.CalculatePenalty(2000m, Today.AddDays(30), Today);
        Assert.Equal(0m, penalty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CalculatePenalty_OneOrTwoDaysBefore_IsHalfPrice(int daysUntilCheckIn)
    {
        var penalty = CancellationPolicy.CalculatePenalty(2000m, Today.AddDays(daysUntilCheckIn), Today);
        Assert.Equal(1000m, penalty);
    }

    [Fact]
    public void CalculatePenalty_SameDayAsCheckIn_IsFullPrice()
    {
        var penalty = CancellationPolicy.CalculatePenalty(2000m, Today, Today);
        Assert.Equal(2000m, penalty);
    }

    [Fact]
    public void CalculatePenalty_AfterCheckIn_IsFullPrice()
    {
        var penalty = CancellationPolicy.CalculatePenalty(2000m, Today.AddDays(-1), Today);
        Assert.Equal(2000m, penalty);
    }
}
