namespace StayDesk.Api.Common.Exceptions;

/// <summary>Excepcion base para errores de reglas de negocio que se traducen a una respuesta HTTP via ProblemDetails.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }

    public abstract int StatusCode { get; }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status404NotFound;
}

public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status409Conflict;
}

public class ValidationFailedException : DomainException
{
    public ValidationFailedException(string message) : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status400BadRequest;
}

public class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
    {
    }

    public override int StatusCode => StatusCodes.Status403Forbidden;
}
