using ConferenceHallBooking.Application.Services;
using ConferenceHallBooking.Application.Validators;
using ConferenceHallBooking.Domain.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ConferenceHallBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateHallRequestValidator>();

        services.AddScoped<IPricingCalculator, PricingCalculator>();
        services.AddScoped<Interfaces.IHallService, HallManagementService>();
        services.AddScoped<Interfaces.IBookingService, BookingService>();
        services.AddScoped<Interfaces.IReportService, ReportService>();

        return services;
    }
}
