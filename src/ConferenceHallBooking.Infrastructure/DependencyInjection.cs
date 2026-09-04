using ConferenceHallBooking.Application.Interfaces;
using ConferenceHallBooking.Infrastructure.Data;
using ConferenceHallBooking.Infrastructure.Health;
using ConferenceHallBooking.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConferenceHallBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

        services.AddScoped<IHallRepository, HallRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();

        return services;
    }

    public static IHealthChecksBuilder AddDatabaseHealthCheck(this IHealthChecksBuilder builder) =>
        builder.AddCheck<SqlConnectionHealthCheck>("sqlserver");
}
