using System.Threading.RateLimiting;
using ConferenceHallBooking.Api.Extensions;
using ConferenceHallBooking.Api.Middleware;
using ConferenceHallBooking.Application;
using ConferenceHallBooking.Infrastructure;
using ConferenceHallBooking.Infrastructure.Seed;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string 'Default' is missing. Set it in appsettings or user secrets.");

builder.Services.AddInfrastructure(connectionString);

builder.Services.AddControllers();
builder.Services.AddApiSwagger();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConferenceHallBooking.Infrastructure.Persistence.AppDbContext>();

// Захист від зловживань: rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("default", limiter =>
    {
        limiter.PermitLimit = 2000;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2000,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
        policy.AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")
              .SetIsOriginAllowed(_ => !builder.Environment.IsProduction()));
});

var app = builder.Build();

await DatabaseSeeder.SeedAsync(app.Services);

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

app.UseHttpsRedirection();
app.UseCors("Default");
app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("default");

app.Run();

/// <summary>Для інтеграційних тестів.</summary>
public partial class Program;
