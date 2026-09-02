namespace ConferenceHallBooking.Domain.Entities;

/// <summary>
/// Бронювання конференц-залу на визначений період.
/// </summary>
public class Booking
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid HallId { get; private set; }

    public Hall? Hall { get; private set; }

    /// <summary>Знімок назви залу на момент створення (не залежить від подальшого видалення залу).</summary>
    public string HallName { get; private set; } = string.Empty;

    public DateTime StartUtc { get; private set; }

    public DateTime EndUtc { get; private set; }

    /// <summary>Тривалість у годинах (може бути дробовою).</summary>
    public decimal DurationHours { get; private set; }

    public string? CustomerName { get; private set; }

    public decimal HallRentalCost { get; private set; }

    public decimal ServicesCost { get; private set; }

    public decimal TotalCost { get; private set; }

    public bool IsCancelled { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private readonly List<BookingServiceItem> _selectedServices = [];
    public IReadOnlyCollection<BookingServiceItem> SelectedServices => _selectedServices.AsReadOnly();

    private Booking()
    {
    }

    internal static Booking Restore(
        Guid id,
        Guid hallId,
        string hallName,
        DateTime startUtc,
        DateTime endUtc,
        decimal durationHours,
        string? customerName,
        decimal hallRentalCost,
        decimal servicesCost,
        decimal totalCost,
        bool isCancelled,
        DateTime createdAtUtc)
    {
        return new Booking
        {
            Id = id,
            HallId = hallId,
            HallName = hallName,
            StartUtc = startUtc,
            EndUtc = endUtc,
            DurationHours = durationHours,
            CustomerName = customerName,
            HallRentalCost = hallRentalCost,
            ServicesCost = servicesCost,
            TotalCost = totalCost,
            IsCancelled = isCancelled,
            CreatedAtUtc = createdAtUtc
        };
    }

    internal void RestoreSelectedServices(IEnumerable<BookingServiceItem> services)
    {
        _selectedServices.Clear();
        _selectedServices.AddRange(services);
    }

    public Booking(
        Guid hallId,
        string hallName,
        DateTime startUtc,
        DateTime endUtc,
        decimal durationHours,
        decimal hallRentalCost,
        IEnumerable<BookingServiceItem> selectedServices,
        string? customerName = null)
    {
        if (string.IsNullOrWhiteSpace(hallName))
            throw new ArgumentException("Назва залу є обов'язковою.", nameof(hallName));

        startUtc = EnsureUtc(startUtc);
        endUtc = EnsureUtc(endUtc);

        if (endUtc <= startUtc)
            throw new ArgumentException("Час завершення має бути пізніше за час початку.");

        if (durationHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationHours));

        HallId = hallId;
        HallName = hallName.Trim();
        StartUtc = startUtc;
        EndUtc = endUtc;
        DurationHours = durationHours;
        HallRentalCost = hallRentalCost;
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();

        _selectedServices.AddRange(selectedServices);
        ServicesCost = _selectedServices.Sum(s => s.Price);
        TotalCost = HallRentalCost + ServicesCost;
    }

    public bool Overlaps(DateTime startUtc, DateTime endUtc) =>
        StartUtc < EnsureUtc(endUtc) && EndUtc > EnsureUtc(startUtc);

    public void Cancel()
    {
        IsCancelled = true;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
