using System.Collections;
using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace PackTracker.Logging.Destructuring;

public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly string[] SensitiveNames =
    [
        "password",
        "passwd",
        "pwd",
        "token",
        "accesstoken",
        "refreshtoken",
        "apikey",
        "api_key",
        "secret",
        "clientsecret",
        "authorization",
        "cookie",
        "jwt",
        "bearer"
    ];

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        result = null;

        if (value is null)
            return false;

        var type = value.GetType();

        if (IsPrimitiveLike(type))
            return false;

        if (value is IEnumerable && value is not string)
            return false;

        var props = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Take(50);

        var eventProps = new List<LogEventProperty>();

        foreach (var prop in props)
        {
            object? raw;
            try
            {
                raw = prop.GetValue(value);
            }
            catch
            {
                raw = "[Unreadable]";
            }

            var safeValue = IsSensitive(prop.Name)
                ? "***REDACTED***"
                : raw;

            eventProps.Add(new LogEventProperty(
                prop.Name,
                propertyValueFactory.CreatePropertyValue(safeValue, destructureObjects: false)));
        }

        result = new StructureValue(eventProps, type.Name);
        return true;
    }

    private static bool IsSensitive(string propertyName) =>
        SensitiveNames.Any(x => propertyName.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static bool IsPrimitiveLike(Type type) =>
        type.IsPrimitive ||
        type.IsEnum ||
        type == typeof(string) ||
        type == typeof(decimal) ||
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) ||
        type == typeof(Guid);
}