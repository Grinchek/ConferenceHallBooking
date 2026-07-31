using System.Net;
using System.Text.Json;
using ConferenceHallBooking.Application.DTOs.Common;
using ConferenceHallBooking.Domain.Exceptions;
using FluentValidation;

namespace ConferenceHallBooking.Api.Middleware;

/// <summary>
/// Централізована обробка винятків — єдиний формат помилок для клієнтів API.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, title, detail, errors) = exception switch
        {
            ValidationException validation => (
                HttpStatusCode.BadRequest,
                "Помилка валідації",
                "Перевірте коректність вхідних даних.",
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                    as IDictionary<string, string[]>),

            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                "Не знайдено",
                notFound.Message,
                null),

            ConflictException conflict => (
                HttpStatusCode.Conflict,
                "Конфлікт",
                conflict.Message,
                null),

            BusinessRuleException business => (
                HttpStatusCode.BadRequest,
                "Порушення бізнес-правила",
                business.Message,
                null),

            DomainException domain => (
                HttpStatusCode.BadRequest,
                "Помилка домену",
                domain.Message,
                null),

            _ => (
                HttpStatusCode.InternalServerError,
                "Внутрішня помилка сервера",
                "Сталася неочікувана помилка. Спробуйте пізніше.",
                null)
        };

        if (status == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning(exception, "Handled business exception: {Title}", title);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)status;

        var payload = new ErrorResponse(title, (int)status, detail, errors);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
