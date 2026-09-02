namespace ConferenceHallBooking.Domain.Entities;

/// <summary>
/// Конференц-зал, доступний для оренди.
/// </summary>
public class Hall
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Name { get; private set; } = string.Empty;

    /// <summary>Максимальна кількість осіб.</summary>
    public int Capacity { get; private set; }

    /// <summary>Базова вартість оренди за годину (грн).</summary>
    public decimal BaseHourlyRate { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; private set; }

    private readonly List<HallService> _services = [];
    public IReadOnlyCollection<HallService> Services => _services.AsReadOnly();

    private readonly List<Booking> _bookings = [];
    public IReadOnlyCollection<Booking> Bookings => _bookings.AsReadOnly();

    private Hall()
    {
    }

    internal static Hall Restore(
        Guid id,
        string name,
        int capacity,
        decimal baseHourlyRate,
        bool isDeleted,
        DateTime createdAtUtc,
        DateTime? updatedAtUtc)
    {
        return new Hall
        {
            Id = id,
            Name = name,
            Capacity = capacity,
            BaseHourlyRate = baseHourlyRate,
            IsDeleted = isDeleted,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    internal void RestoreServices(IEnumerable<HallService> services)
    {
        _services.Clear();
        _services.AddRange(services);
    }

    public Hall(string name, int capacity, decimal baseHourlyRate, IEnumerable<HallService>? services = null)
    {
        UpdateDetails(name, capacity, baseHourlyRate);
        if (services is not null)
        {
            ReplaceServices(services);
        }
    }

    public void UpdateDetails(string name, int capacity, decimal baseHourlyRate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва залу є обов'язковою.", nameof(name));

        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Місткість має бути більшою за 0.");

        if (baseHourlyRate < 0)
            throw new ArgumentOutOfRangeException(nameof(baseHourlyRate), "Вартість не може бути від'ємною.");

        Name = name.Trim();
        Capacity = capacity;
        BaseHourlyRate = baseHourlyRate;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void ReplaceServices(IEnumerable<HallService> services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var distinct = services
            .GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        // Видаляємо по одному, щоб EF Core коректно відстежив Deleted для кожного елемента.
        for (var i = _services.Count - 1; i >= 0; i--)
            _services.RemoveAt(i);

        foreach (var service in distinct)
            _services.Add(new HallService(service.Name, service.Price, Id));

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Перевіряє, чи зал вільний у вказаному інтервалі (без урахування скасованих бронювань).
    /// </summary>
    public bool IsAvailable(DateTime startUtc, DateTime endUtc, Guid? ignoreBookingId = null)
    {
        return !_bookings.Any(b =>
            !b.IsCancelled &&
            (ignoreBookingId is null || b.Id != ignoreBookingId) &&
            b.Overlaps(startUtc, endUtc));
    }
}
