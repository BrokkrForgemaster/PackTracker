namespace PackTracker.Application.Admin.Common;

public sealed class AdminAuthorizationException : Exception
{
    public AdminAuthorizationException()
    {
    }

    public AdminAuthorizationException(string message) : base(message)
    {
    }

    public AdminAuthorizationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
