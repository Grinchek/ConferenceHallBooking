namespace ConferenceHallBooking.Application.DTOs.Common;

/// <summary>Стандартна відповідь API з повідомленням.</summary>
public sealed record ApiMessageResponse(string Message, Guid? Id = null);

/// <summary>Обгортка помилки валідації.</summary>
public sealed record ErrorResponse(string Title, int Status, string Detail, IDictionary<string, string[]>? Errors = null);
