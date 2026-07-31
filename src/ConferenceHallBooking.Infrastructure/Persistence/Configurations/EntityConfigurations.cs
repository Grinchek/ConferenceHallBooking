using ConferenceHallBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ConferenceHallBooking.Infrastructure.Persistence.Configurations;

public sealed class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        builder.ToTable("Halls");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(h => h.BaseHourlyRate)
            .HasPrecision(18, 2);

        // Унікальність назви перевіряється в Application-шарі (з урахуванням soft-delete).
        builder.HasIndex(h => h.Name);

        builder.HasMany(h => h.Services)
            .WithOne()
            .HasForeignKey(s => s.HallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(h => h.Bookings)
            .WithOne(b => b.Hall)
            .HasForeignKey(b => b.HallId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(h => h.Services)
            .HasField("_services")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(h => h.Bookings)
            .HasField("_bookings")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasQueryFilter(h => !h.IsDeleted);
    }
}

public sealed class HallServiceConfiguration : IEntityTypeConfiguration<HallService>
{
    public void Configure(EntityTypeBuilder<HallService> builder)
    {
        builder.ToTable("HallServices");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Price)
            .HasPrecision(18, 2);
    }
}

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.CustomerName).HasMaxLength(200);
        builder.Property(b => b.DurationHours).HasPrecision(18, 2);
        builder.Property(b => b.HallRentalCost).HasPrecision(18, 2);
        builder.Property(b => b.ServicesCost).HasPrecision(18, 2);
        builder.Property(b => b.TotalCost).HasPrecision(18, 2);

        builder.HasIndex(b => new { b.HallId, b.StartUtc, b.EndUtc });

        builder.HasMany(b => b.SelectedServices)
            .WithOne()
            .HasForeignKey(s => s.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.SelectedServices)
            .HasField("_selectedServices")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(b => b.Hall)
            .UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}

public sealed class BookingServiceItemConfiguration : IEntityTypeConfiguration<BookingServiceItem>
{
    public void Configure(EntityTypeBuilder<BookingServiceItem> builder)
    {
        builder.ToTable("BookingServiceItems");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Price).HasPrecision(18, 2);
    }
}
