using System.Threading.RateLimiting;
using ConferenceHallBooking.Api.Extensions;
using ConferenceHallBooking.Api.Middleware;
using ConferenceHallBooking.Application;
using ConferenceHallBooking.Infrastructure;
using ConferenceHallBooking.Infrastructure.Migrations;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is missing. Set it in appsettings or user secrets.");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddApiSwagger();

builder.Services.AddHealthChecks()
    .AddDatabaseHealthCheck();

// Rate limiting: у Development вищий ліміт під порівняльні load-прогони (3×1000 + cleanup).
var rateLimitPerMinute = builder.Environment.IsDevelopment() ? 20_000 : 2_000;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = rateLimitPerMinute;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return RateLimitPartition.GetNoLimiter("health");

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .SetIsOriginAllowed(_ => !builder.Environment.IsProduction()));
});

var app = builder.Build();

DatabaseMigrator.Migrate(connectionString);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Demo"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Conference Hall Booking API v1");
        options.DocumentTitle = "Conference Hall Booking API";
    });
}

// У Development залишаємо чистий HTTP (профіль http / load testing без SSL-редіректу).
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors("Default");
app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("default");

app.Run();

/// <summary>Для інтеграційних тестів.</summary>
public partial class Program;
