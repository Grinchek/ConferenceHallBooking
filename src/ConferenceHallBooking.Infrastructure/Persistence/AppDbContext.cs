using ConferenceHallBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ConferenceHallBooking.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<HallService> HallServices => Set<HallService>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingServiceItem> BookingServiceItems => Set<BookingServiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("IGrinSchema");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
