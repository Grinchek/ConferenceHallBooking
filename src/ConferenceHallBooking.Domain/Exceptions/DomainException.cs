namespace ConferenceHallBooking.Domain.Exceptions;

/// <summary>
/// Базовий виняток доменної логіки.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}

/// <summary>
/// Сутність не знайдена.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

/// <summary>
/// Конфлікт стану (наприклад, зал уже зайнятий).
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}

/// <summary>
/// Порушення бізнес-правил валідації.
/// </summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
