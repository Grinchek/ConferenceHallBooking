using ConferenceHallBooking.Domain.Services;

namespace ConferenceHallBooking.Tests;

public class PricingCalculatorTests
{
    private readonly PricingCalculator _sut = new();

    [Fact]
    public void Standard_hours_use_base_rate()
    {
        // 10:00–11:00 — стандартний період
        var start = new DateTime(2024, 9, 1, 10, 0, 0);
        var end = new DateTime(2024, 9, 1, 11, 0, 0);

        var result = _sut.CalculateHallRental(2000m, start, end);

        Assert.Equal(2000m, result.TotalHallCost);
        Assert.Single(result.Breakdown);
        Assert.Equal("Standard", result.Breakdown[0].PeriodName);
    }

    [Fact]
    public void Morning_hours_apply_10_percent_discount()
    {
        // 07:00–08:00 — −10%
        var start = new DateTime(2024, 9, 1, 7, 0, 0);
        var end = new DateTime(2024, 9, 1, 8, 0, 0);

        var result = _sut.CalculateHallRental(2000m, start, end);

        Assert.Equal(1800m, result.TotalHallCost);
        Assert.Equal("Morning", result.Breakdown[0].PeriodName);
    }

    [Fact]
    public void Evening_hours_apply_20_percent_discount()
    {
        // 19:00–20:00 — −20%
        var start = new DateTime(2024, 9, 1, 19, 0, 0);
        var end = new DateTime(2024, 9, 1, 20, 0, 0);

        var result = _sut.CalculateHallRental(2000m, start, end);

        Assert.Equal(1600m, result.TotalHallCost);
        Assert.Equal("Evening", result.Breakdown[0].PeriodName);
    }

    [Fact]
    public void Peak_hours_apply_15_percent_markup()
    {
        // 12:00–13:00 — +15%
        var start = new DateTime(2024, 9, 1, 12, 0, 0);
        var end = new DateTime(2024, 9, 1, 13, 0, 0);

        var result = _sut.CalculateHallRental(2000m, start, end);

        Assert.Equal(2300m, result.TotalHallCost);
        Assert.Equal("Peak", result.Breakdown[0].PeriodName);
    }

    [Fact]
    public void Mixed_period_10_to_14_splits_standard_and_peak()
    {
        // 10:00–14:00: 2 год Standard + 2 год Peak
        // 2*2000 + 2*2000*1.15 = 4000 + 4600 = 8600
        var start = new DateTime(2024, 9, 1, 10, 0, 0);
        var end = new DateTime(2024, 9, 1, 14, 0, 0);

        var result = _sut.CalculateHallRental(2000m, start, end);

        Assert.Equal(8600m, result.TotalHallCost);
        Assert.Equal(2, result.Breakdown.Count);
        Assert.Equal("Standard", result.Breakdown[0].PeriodName);
        Assert.Equal("Peak", result.Breakdown[1].PeriodName);
    }

    [Fact]
    public void Peak_has_priority_over_standard()
    {
        var start = new DateTime(2024, 9, 1, 12, 30, 0);
        var end = new DateTime(2024, 9, 1, 13, 30, 0);

        var result = _sut.CalculateHallRental(1000m, start, end);

        Assert.All(result.Breakdown, item => Assert.Equal("Peak", item.PeriodName));
        Assert.Equal(1150m, result.TotalHallCost);
    }
}
