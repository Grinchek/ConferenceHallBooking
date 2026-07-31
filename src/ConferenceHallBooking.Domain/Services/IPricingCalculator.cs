namespace ConferenceHallBooking.Domain.Services;

/// <summary>
/// Розрахунок вартості оренди залу з урахуванням тарифних періодів доби.
/// </summary>
public interface IPricingCalculator
{
    /// <summary>
    /// Обчислює вартість оренди залу за інтервал [start, end).
    /// Погодинна ставка застосовується пропорційно до хвилин у кожному тарифному періоді.
    /// </summary>
    PricingResult CalculateHallRental(decimal baseHourlyRate, DateTime start, DateTime end);
}

public sealed record PricingResult(
    decimal TotalHallCost,
    IReadOnlyList<PricingBreakdownItem> Breakdown);

public sealed record PricingBreakdownItem(
    DateTime SegmentStart,
    DateTime SegmentEnd,
    string PeriodName,
    decimal Multiplier,
    decimal Cost);
