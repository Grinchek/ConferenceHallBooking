namespace ConferenceHallBooking.Domain.Entities;

/// <summary>
/// Бронювання конференц-залу на визначений період.
/// </summary>
public class Booking
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid HallId { get; private set; }

    public Hall? Hall { get; private set; }

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

    public Booking(
        Guid hallId,
        DateTime startUtc,
        DateTime endUtc,
        decimal durationHours,
        decimal hallRentalCost,
        IEnumerable<BookingServiceItem> selectedServices,
        string? customerName = null)
    {
        if (endUtc <= startUtc)
            throw new ArgumentException("Час завершення має бути пізніше за час початку.");

        if (durationHours <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationHours));

        HallId = hallId;
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
        StartUtc < endUtc && EndUtc > startUtc;

    public void Cancel()
    {
        IsCancelled = true;
    }
}
