namespace ConferenceHallBooking.Domain.Enums;

/// <summary>
/// Тарифний період доби для розрахунку вартості оренди.
/// Пріоритет: Peak &gt; Evening / Morning &gt; Standard.
/// </summary>
public enum PricingPeriod
{
    /// <summary>06:00–09:00 — знижка 10%.</summary>
    Morning,

    /// <summary>09:00–18:00 (окрім піку) — базова ставка.</summary>
    Standard,

    /// <summary>12:00–14:00 — націнка 15%.</summary>
    Peak,

    /// <summary>18:00–23:00 — знижка 20%.</summary>
    Evening,

    /// <summary>Поза робочим вікном (23:00–06:00) — базова ставка без знижок.</summary>
    OffHours
}
