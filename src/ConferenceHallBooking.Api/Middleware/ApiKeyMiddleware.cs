using System.Security.Cryptography;
using System.Text;

namespace ConferenceHallBooking.Api.Middleware;

/// <summary>
/// Проста API Key автентифікація для захисту ендпоінтів від несанкціонованого доступу.
/// Ключ передається в заголовку <c>X-Api-Key</c>.
/// </summary>
public sealed class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";
    public const string ConfigSection = "Security:ApiKey";

    private readonly RequestDelegate _next;
    private readonly string _expectedKey;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedKey = configuration[ConfigSection]
            ?? throw new InvalidOperationException($"Не задано {ConfigSection} у конфігурації.");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Swagger UI та health-check залишаємо відкритими
        var path = context.Request.Path.Value ?? string.Empty;
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            !FixedTimeEquals(provided.ToString(), _expectedKey))
        {
            _logger.LogWarning("Відхилено запит без валідного API ключа з {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"title":"Unauthorized","status":401,"detail":"Потрібен валідний заголовок X-Api-Key."}""");
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path) =>
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Порівняння без timing-атак.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
