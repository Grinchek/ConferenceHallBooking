using ConferenceHallBooking.Domain.Enums;

namespace ConferenceHallBooking.Domain.Services;

/// <summary>
/// Розраховує вартість оренди похвилинно з урахуванням тарифних періодів.
/// Тарифні вікна визначені в UTC (той самий контракт, що й StartUtc/EndUtc у бронюваннях).
/// <list type="bullet">
/// <item>Ранкові години (06:00–09:00 UTC): −10%</item>
/// <item>Стандартні години (09:00–18:00 UTC): базова ставка</item>
/// <item>Пікові години (12:00–14:00 UTC): +15% (мають пріоритет над стандартними)</item>
/// <item>Вечірні години (18:00–23:00 UTC): −20%</item>
/// </list>
/// </summary>
public sealed class PricingCalculator : IPricingCalculator
{
    private static readonly (TimeSpan From, TimeSpan To, PricingPeriod Period, decimal Multiplier)[] Rules =
    [
        (TimeSpan.FromHours(12), TimeSpan.FromHours(14), PricingPeriod.Peak, 1.15m),
        (TimeSpan.FromHours(6), TimeSpan.FromHours(9), PricingPeriod.Morning, 0.90m),
        (TimeSpan.FromHours(18), TimeSpan.FromHours(23), PricingPeriod.Evening, 0.80m),
        (TimeSpan.FromHours(9), TimeSpan.FromHours(18), PricingPeriod.Standard, 1.00m)
    ];

    public PricingResult CalculateHallRental(decimal baseHourlyRate, DateTime start, DateTime end)
    {
        start = EnsureUtc(start);
        end = EnsureUtc(end);

        if (end <= start)
            throw new ArgumentException("Інтервал бронювання має бути додатним.");

        if (baseHourlyRate < 0)
            throw new ArgumentOutOfRangeException(nameof(baseHourlyRate));

        var breakdown = new List<PricingBreakdownItem>();
        var cursor = start;

        while (cursor < end)
        {
            var (period, multiplier) = ResolvePeriod(cursor.TimeOfDay);
            var segmentEnd = Min(end, NextBoundary(cursor));
            var hours = (decimal)(segmentEnd - cursor).TotalHours;
            var cost = Math.Round(baseHourlyRate * multiplier * hours, 2, MidpointRounding.AwayFromZero);

            breakdown.Add(new PricingBreakdownItem(
                cursor,
                segmentEnd,
                period.ToString(),
                multiplier,
                cost));

            cursor = segmentEnd;
        }

        var total = breakdown.Sum(b => b.Cost);
        return new PricingResult(total, breakdown);
    }

    /// <summary>
    /// Визначає тарифний період для моменту часу (UTC). Peak має найвищий пріоритет.
    /// </summary>
    private static (PricingPeriod Period, decimal Multiplier) ResolvePeriod(TimeSpan timeOfDay)
    {
        foreach (var rule in Rules)
        {
            if (timeOfDay >= rule.From && timeOfDay < rule.To)
                return (rule.Period, rule.Multiplier);
        }

        return (PricingPeriod.OffHours, 1.00m);
    }

    /// <summary>
    /// Наступна межа тарифного сегмента (межі правил + північ UTC).
    /// </summary>
    private static DateTime NextBoundary(DateTime moment)
    {
        var day = moment.Date;
        var boundaries = new[]
        {
            day.AddHours(6),
            day.AddHours(9),
            day.AddHours(12),
            day.AddHours(14),
            day.AddHours(18),
            day.AddHours(23),
            day.AddDays(1)
        };

        return boundaries.First(b => b > moment);
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    /// <summary>
    /// Local → UTC; Unspecified трактуємо як уже UTC (контракт API з DateTimeOffset.UtcDateTime).
    /// </summary>
    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
