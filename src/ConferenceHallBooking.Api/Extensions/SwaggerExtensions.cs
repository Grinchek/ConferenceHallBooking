using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConferenceHallBooking.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Conference Hall Booking API",
                Version = "v1",
                Description = """
                    API для управління конференц-залами, бронюваннями та розрахунку вартості оренди.

                    **Тарифікація залу:**
                    - Ранкові години (06:00–09:00): знижка 10%
                    - Стандартні години (09:00–18:00): базова вартість
                    - Пікові години (12:00–14:00): націнка 15%
                    - Вечірні години (18:00–23:00): знижка 20%

                    Автентифікація: передайте API-ключ у заголовку `X-Api-Key`.
                    """
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Description = "API Key у заголовку X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Api-Key",
                In = ParameterLocation.Header
            });

            options.AddSecurityRequirement(document =>
            {
                var schemeRef = new OpenApiSecuritySchemeReference("ApiKey", document);
                return new OpenApiSecurityRequirement
                {
                    [schemeRef] = []
                };
            });

            // XML-коментарі контролерів
            var xmlPath = Path.Combine(AppContext.BaseDirectory, "ConferenceHallBooking.Api.xml");
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }
}
