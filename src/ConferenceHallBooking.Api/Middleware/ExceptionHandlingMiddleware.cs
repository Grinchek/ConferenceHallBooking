using System.Net;
using ConferenceHallBooking.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ConferenceHallBooking.Api.Middleware;

/// <summary>
/// Централізована обробка винятків — єдиний формат помилок для клієнтів API.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IProblemDetailsService problemDetailsService)
    {
        _next = next;
        _logger = logger;
        _problemDetailsService = problemDetailsService;
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
        var mapping = MapException(exception);

        if (mapping.Status == HttpStatusCode.InternalServerError)
            _logger.LogError(exception, "Unhandled exception");
        else
            _logger.LogWarning(exception, "Handled business exception: {Title}", mapping.Title);

        context.Response.StatusCode = (int)mapping.Status;

        var problemDetails = new ProblemDetails
        {
            Status = (int)mapping.Status,
            Title = mapping.Title,
            Detail = mapping.Detail,
            Instance = context.Request.Path
        };

        if (mapping.Errors is not null)
            problemDetails.Extensions["errors"] = mapping.Errors;

        await _problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    /// <summary>
    /// Визначає HTTP-статус і текст помилки для відомих типів винятків.
    /// </summary>
    private static ExceptionMapping MapException(Exception exception) =>
        exception switch
        {
            ValidationException validation => new(
                HttpStatusCode.BadRequest,
                "Помилка валідації",
                "Перевірте коректність вхідних даних.",
                validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            NotFoundException notFound => new(
                HttpStatusCode.NotFound,
                "Не знайдено",
                notFound.Message,
                null),

            ConflictException conflict => new(
                HttpStatusCode.Conflict,
                "Конфлікт",
                conflict.Message,
                null),

            BusinessRuleException business => new(
                HttpStatusCode.BadRequest,
                "Порушення бізнес-правила",
                business.Message,
                null),

            DomainException domain => new(
                HttpStatusCode.BadRequest,
                "Помилка домену",
                domain.Message,
                null),

            _ => new(
                HttpStatusCode.InternalServerError,
                "Внутрішня помилка сервера",
                "Сталася неочікувана помилка. Спробуйте пізніше.",
                null)
        };

    private sealed record ExceptionMapping(
        HttpStatusCode Status,
        string Title,
        string Detail,
        IDictionary<string, string[]>? Errors);
}
