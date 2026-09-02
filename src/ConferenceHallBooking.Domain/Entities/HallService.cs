namespace ConferenceHallBooking.Domain.Entities;

/// <summary>
/// Додаткова послуга залу (проєктор, Wi-Fi тощо).
/// Вартість фіксована за одне бронювання.
/// </summary>
public class HallService
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid HallId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Фіксована вартість послуги за бронювання (грн).</summary>
    public decimal Price { get; private set; }

    private HallService()
    {
    }

    internal static HallService Restore(Guid id, Guid hallId, string name, decimal price) =>
        new()
        {
            Id = id,
            HallId = hallId,
            Name = name,
            Price = price
        };

    public HallService(string name, decimal price, Guid hallId = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва послуги є обов'язковою.", nameof(name));

        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "Вартість послуги не може бути від'ємною.");

        Name = name.Trim();
        Price = price;
        HallId = hallId;
    }
}
