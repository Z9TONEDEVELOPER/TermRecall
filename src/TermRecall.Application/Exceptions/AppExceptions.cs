namespace TermRecall.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}
