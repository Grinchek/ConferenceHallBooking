namespace ConferenceHallBooking.Domain.Entities;

/// <summary>
/// Знімок обраної послуги на момент бронювання
/// (щоб історична вартість не змінювалась при оновленні залу).
/// </summary>
public class BookingServiceItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid BookingId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    private BookingServiceItem()
    {
    }

    internal static BookingServiceItem Restore(Guid id, Guid bookingId, string name, decimal price) =>
        new()
        {
            Id = id,
            BookingId = bookingId,
            Name = name,
            Price = price
        };

    public BookingServiceItem(string name, decimal price)
    {
        Name = name;
        Price = price;
    }
}
